using System.Globalization;
using System.Text.Json;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Pattern;

namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>Loads and refreshes a project's persisted pattern-file index.</summary>
public sealed class PatternIndexService(PatternBinarySerializer serializer)
{
    private const string PatternDirectoryName = "Patterns";
    private const string IndexFileName = "patterns.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Loads a project's pattern index, returning an empty index when none exists yet.</summary>
    public async Task<PatternIndexDocument> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var indexPath = Path.Combine(projectRoot, IndexFileName);
        if (!File.Exists(indexPath))
            return new PatternIndexDocument();

        await using var stream = File.OpenRead(indexPath);
        using var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var patternsProperty = jsonDocument.RootElement.EnumerateObject()
            .FirstOrDefault(property => property.Name.Equals("Patterns", StringComparison.OrdinalIgnoreCase));
        if (patternsProperty.Value.ValueKind == JsonValueKind.Array)
            return MigrateLegacyIndex(jsonDocument.RootElement);

        return jsonDocument.RootElement.Deserialize<PatternIndexDocument>(JsonOptions)
               ?? throw new InvalidDataException("The pattern index file is empty or invalid.");
    }

    /// <summary>Scans all supported pattern filenames, reads each file as bytes format, and saves the index.</summary>
    public async Task<PatternIndexDocument> RefreshAsync(
        string projectRoot,
        IProgress<PatternIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var fullRoot = Path.GetFullPath(projectRoot);
        Directory.CreateDirectory(fullRoot);
        PatternIndexDocument previousIndex;
        try
        {
            previousIndex = await LoadAsync(fullRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            previousIndex = new PatternIndexDocument();
        }
        var patternRoot = Path.Combine(fullRoot, PatternDirectoryName);
        var files = Directory.Exists(patternRoot)
            ? Directory.EnumerateFiles(patternRoot, "*", SearchOption.AllDirectories)
                .Select(file => (File: file, Identity: ParseFileIdentity(Path.GetFileName(file))))
                .Where(item => item.Identity is not null)
                .OrderBy(item => item.Identity!.Value.PatternId)
                .ThenBy(item => item.Identity!.Value.IsEarphoneVariant)
                .ThenBy(item => item.File, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var document = new PatternIndexDocument();
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (file, identity) = files[index];
            var info = new FileInfo(file);
            var fileIndex = new PatternFileIndex
            {
                RelativePath = Path.GetRelativePath(fullRoot, file).Replace(Path.DirectorySeparatorChar, '/'),
                FileSize = info.Length,
                LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
            };

            try
            {
                await using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var pattern = await serializer.DeserializeAsync(stream, PatternFormat.Bytes, cancellationToken)
                    .ConfigureAwait(false);
                fileIndex.TrackCount = pattern.Tracks.Count;
                fileIndex.SoundCount = pattern.Sounds.Count;
                fileIndex.EventCount = pattern.CommandCount;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                fileIndex.ParseError = exception.Message;
            }

            var patternId = identity!.Value.PatternId;
            if (!document.Patterns.TryGetValue(patternId, out var entry))
            {
                entry = new PatternIndexEntry();
                document.Patterns.Add(patternId, entry);
            }

            if (identity.Value.IsEarphoneVariant)
            {
                entry.HasHeadphone = true;
                entry.Headphone = fileIndex;
            }
            else
            {
                entry.Standard = fileIndex;
            }

            progress?.Report(new PatternIndexProgress(index + 1, files.Length, fileIndex.RelativePath));
        }

        foreach (var (patternId, previousEntry) in previousIndex.Patterns)
        {
            if (previousEntry.SongPatterns.Count == 0)
                continue;

            if (!document.Patterns.TryGetValue(patternId, out var refreshedEntry))
            {
                refreshedEntry = new PatternIndexEntry();
                document.Patterns.Add(patternId, refreshedEntry);
            }

            refreshedEntry.SongPatterns = [.. previousEntry.SongPatterns];
        }

        document.LastIndexedAtUtc = DateTimeOffset.UtcNow;
        await SaveAsync(fullRoot, document, cancellationToken).ConfigureAwait(false);
        return document;
    }

    /// <summary>Synchronizes the index with current songPattern rows while retaining file-only entries.</summary>
    public async Task<PatternIndexDocument> SyncSongPatternsAsync(
        string projectRoot,
        IEnumerable<PatternSongReference> songPatterns,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(songPatterns);

        var fullRoot = Path.GetFullPath(projectRoot);
        Directory.CreateDirectory(fullRoot);
        PatternIndexDocument document;
        try
        {
            document = await LoadAsync(fullRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            // A broken file index must not prevent songPattern rows from being represented.
            document = new PatternIndexDocument();
        }
        var groupedSongPatterns = songPatterns
            .GroupBy(reference => reference.PatternId)
            .ToDictionary(group => group.Key, group => group
                .GroupBy(reference => reference.SongId)
                .Select(song => song.First())
                .OrderBy(reference => reference.SongId)
                .ToList());
        var changed = document.SchemaVersion < 3;
        document.SchemaVersion = 3;

        foreach (var (patternId, entry) in document.Patterns)
        {
            var nextReferences = groupedSongPatterns.GetValueOrDefault(patternId, []);
            if (!SameSongPatterns(entry.SongPatterns, nextReferences))
            {
                entry.SongPatterns = nextReferences;
                changed = true;
            }
        }

        foreach (var (patternId, references) in groupedSongPatterns)
        {
            if (!document.Patterns.TryGetValue(patternId, out var entry))
            {
                entry = new PatternIndexEntry { SongPatterns = references };
                document.Patterns.Add(patternId, entry);
                changed = true;
            }
        }

        foreach (var patternId in document.Patterns
                     .Where(pair => pair.Value.SongPatterns.Count == 0
                         && pair.Value.Standard is null && pair.Value.Headphone is null)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            document.Patterns.Remove(patternId);
            changed = true;
        }

        if (changed)
            await SaveAsync(fullRoot, document, cancellationToken).ConfigureAwait(false);

        return document;
    }

    private static bool SameSongPatterns(
        IReadOnlyList<PatternSongReference> current,
        IReadOnlyList<PatternSongReference> next)
        => current.Count == next.Count
           && current.Zip(next).All(pair => pair.First.SongId == pair.Second.SongId
               && string.Equals(pair.First.SongName, pair.Second.SongName, StringComparison.Ordinal)
               && pair.First.Line == pair.Second.Line
               && pair.First.Signature == pair.Second.Signature
               && pair.First.Difficulty == pair.Second.Difficulty);

    private static async Task SaveAsync(
        string projectRoot,
        PatternIndexDocument document,
        CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(projectRoot, IndexFileName);
        var temporaryPath = Path.Combine(projectRoot, $".{IndexFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, indexPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static (int PatternId, bool IsEarphoneVariant)? ParseFileIdentity(string fileName)
    {
        var isEarphone = fileName.EndsWith("_EARPHONE", StringComparison.OrdinalIgnoreCase);
        var idText = isEarphone ? fileName[..^"_EARPHONE".Length] : fileName;
        return int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var patternId)
            ? (patternId, isEarphone)
            : null;
    }

    private static PatternIndexDocument MigrateLegacyIndex(JsonElement root)
    {
        var legacy = root.Deserialize<LegacyPatternIndexDocument>(JsonOptions)
                     ?? throw new InvalidDataException("The pattern index file is empty or invalid.");
        var migrated = new PatternIndexDocument { LastIndexedAtUtc = legacy.LastIndexedAtUtc };
        foreach (var item in legacy.Patterns)
        {
            if (!migrated.Patterns.TryGetValue(item.PatternId, out var entry))
            {
                entry = new PatternIndexEntry();
                migrated.Patterns.Add(item.PatternId, entry);
            }

            var file = new PatternFileIndex
            {
                RelativePath = item.RelativePath,
                FileSize = item.FileSize,
                LastWriteTimeUtc = item.LastWriteTimeUtc,
                TrackCount = item.TrackCount,
                SoundCount = item.SoundCount,
                EventCount = item.EventCount,
                ParseError = item.ParseError
            };
            if (item.IsEarphoneVariant)
            {
                entry.HasHeadphone = true;
                entry.Headphone = file;
            }
            else
            {
                entry.Standard = file;
            }
        }

        return migrated;
    }

    private sealed class LegacyPatternIndexDocument
    {
        public DateTimeOffset? LastIndexedAtUtc { get; set; }
        public List<LegacyPatternIndexEntry> Patterns { get; set; } = [];
    }

    private sealed class LegacyPatternIndexEntry
    {
        public int PatternId { get; set; }
        public bool IsEarphoneVariant { get; set; }
        public string RelativePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTimeOffset LastWriteTimeUtc { get; set; }
        public int? TrackCount { get; set; }
        public int? SoundCount { get; set; }
        public int? EventCount { get; set; }
        public string? ParseError { get; set; }
    }
}
