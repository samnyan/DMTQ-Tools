using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Models.Csv;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageImporter(
    CsvTableReader tableReader)
{
    public async Task<PatchPackage> ImportAsync(
        string packageRoot,
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var manifestPath = Path.Combine(packageRoot, "patch_new.csv.lz4");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Could not find patch_new.csv.lz4.", manifestPath);
        }

        Directory.CreateDirectory(projectRoot);
        var tempRoot = Path.Combine(projectRoot, "temp", "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestCsvPath = Path.Combine(tempRoot, "patch_new.csv");
            await FileUtility.DecompressFileAsync(manifestPath, manifestCsvPath, cancellationToken).ConfigureAwait(false);

            await using var manifestStream = File.OpenRead(manifestCsvPath);
            var manifest = await PatchManifestIO.ReadAsync(manifestStream, cancellationToken).ConfigureAwait(false);

            var package = new PatchPackage
            {
                ProjectInfo = new ProjectInfo(projectRoot, packageRoot, TryGetVersion(packageRoot), TryGetPlatform(packageRoot))
            };

            var csvEntries = new List<CsvImportEntry>();

            foreach (var entry in manifest.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = FileUtility.NormalizePackageRelativePath(entry.FileName);
                var sourcePath = ResolveSourcePath(packageRoot, relativePath, entry.Compressed);

                if (FileUtility.IsCsvTable(relativePath))
                {
                    // slang is a shared resource, not an entity-backed table
                    if (relativePath.Equals("table/slang/slang.csv", StringComparison.OrdinalIgnoreCase))
                    {
                        ImportSharedResource(package, projectRoot, relativePath, entry, sourcePath, cancellationToken);
                        continue;
                    }

                    var csvPath = await EnsureCsvFileAsync(sourcePath, tempRoot, relativePath, entry.Compressed, cancellationToken)
                        .ConfigureAwait(false);

                    var tableName = GetTableName(relativePath);
                    var languageCode = GetLanguageCode(relativePath)
                                       ?? ExtractLanguageSuffix(tableName);

                    csvEntries.Add(new CsvImportEntry(csvPath, tableName, languageCode, relativePath));
                }
                else
                {
                    var category = FileUtility.ResourceCategory(relativePath);
                    var projectRelativePath = Path.Combine("resources", relativePath).Replace('\\', '/');
                    var archivedPath = Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(archivedPath) ?? projectRoot);

                    if (entry.Compressed)
                    {
                        await FileUtility.DecompressFileAsync(sourcePath, archivedPath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await using var source = File.OpenRead(sourcePath);
                        await using var destination = File.Create(archivedPath);
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    }

                    // Create ResourceFile with PlatformManifestEntry for the package's platform
                    var resourceFile = new ResourceFile
                    {
                        FileName = relativePath,
                        Category = category,
                        Compressed = entry.Compressed,
                        AcquireOnDemand = entry.AcquireOnDemand,
                        PlatformManifest =
                        {
                            new PlatformManifestEntry
                            {
                                Platform = package.ProjectInfo.Platform ?? "share",
                                Exist = true,
                                SourceFileSize = entry.FileSize,
                                SourceChecksum = entry.Checksum,
                                SourceCompressedFileSize = entry.CompressedFileSize,
                                SourceCompressedChecksum = entry.CompressedChecksum,
                                Checksum = string.Empty
                            }
                        }
                    };
                    package.Resources.Add(resourceFile);
                }
            }

            // ── Phase 1: import standalone entity tables ──
            ImportEntityTablesPhase1(package, csvEntries, cancellationToken);

            // ── Phase 2: import dependent entity tables (patterns, song localizations) ──
            ImportEntityTablesPhase2(package, csvEntries, cancellationToken);

            // ── Phase 3: import lookup tables (localized descriptions, category links) ──
            ImportLookupTables(package, csvEntries, cancellationToken);

            // ── Phase 4: cross-entity links (song↔product, song↔item, previews) ──
            BuildPreviewLinks(package);

            // ── Import any remaining non-entity tables as raw GameTables (for legacy/unknown tables) ──
            var entityTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "song_song", "song_songPattern",
                "quest_achievement", "product_product", "category_categoryproduct",
                "product_item", "ingameitem_ingameitem", "ingameitem_itemeffect"
            };
            foreach (var entry in csvEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entityTableNames.Contains(entry.TableName))
                    continue;
                if (IsLocalizedTable(entry.TableName, "song_desc")
                    || IsLocalizedTable(entry.TableName, "acievement_desc")
                    || IsLocalizedTable(entry.TableName, "quest_desc")
                    || IsLocalizedTable(entry.TableName, "quest_mission_desc")
                    || IsLocalizedTable(entry.TableName, "item_desc"))
                    continue;

                // Non-entity table: read as raw GameTable
                await using var csvStream = File.OpenRead(entry.FilePath);
                var table = await tableReader.ReadAsync(csvStream, entry.RelativePath, cancellationToken).ConfigureAwait(false);
                package.Tables.Tables.Add(table);
            }

            return package;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    // ── Phase 1: standalone entity tables ──

    private static void ImportEntityTablesPhase1(
        PatchPackage package,
        List<CsvImportEntry> entries,
        CancellationToken cancellationToken)
    {
        var existingSongIds = package.Songs.Select(s => s.Id)
            .ToHashSet();
        var existingAchievementIds = package.Achievements.Select(a => a.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingProductIds = package.Products.Select(p => p.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingItemIds = package.Items.Select(i => i.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIngameItemIds = package.IngameItems.Select(i => i.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEffectIds = package.IngameItemEffects.Select(e => e.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (entry.TableName)
            {
                case "song_song":
                {
                    var songs = ReadWithSchema<Song, SongCsvSchema>(entry.FilePath);
                    foreach (var song in songs)
                    {
                        if (existingSongIds.Add(song.Id))
                            package.Songs.Add(song);
                    }
                    break;
                }
                case "quest_achievement":
                {
                    var achievements = ReadWithSchema<Achievement, AchievementCsvSchema>(entry.FilePath);
                    foreach (var achievement in achievements)
                    {
                        if (existingAchievementIds.Add(achievement.Id))
                            package.Achievements.Add(achievement);
                    }
                    break;
                }
                case "product_product":
                {
                    var products = ReadWithSchema<Product, ProductCsvSchema>(entry.FilePath);
                    foreach (var product in products)
                    {
                        if (existingProductIds.Add(product.Id))
                            package.Products.Add(product);
                    }
                    break;
                }
                case "product_item":
                {
                    var items = ReadWithSchema<Item, ItemCsvSchema>(entry.FilePath);
                    foreach (var item in items)
                    {
                        if (existingItemIds.Add(item.Id))
                            package.Items.Add(item);
                    }
                    break;
                }
                case "ingameitem_ingameitem":
                {
                    var ingameItems = ReadWithSchema<IngameItem, IngameItemCsvSchema>(entry.FilePath);
                    foreach (var ingameItem in ingameItems)
                    {
                        if (existingIngameItemIds.Add(ingameItem.Id))
                            package.IngameItems.Add(ingameItem);
                    }
                    break;
                }
                case "ingameitem_itemeffect":
                {
                    var effects = ReadWithSchema<IngameItemEffect, IngameItemEffectCsvSchema>(entry.FilePath);
                    foreach (var effect in effects)
                    {
                        if (existingEffectIds.Add(effect.Id))
                            package.IngameItemEffects.Add(effect);
                    }
                    break;
                }
            }
        }
    }

    // ── Phase 2: dependent entity tables (patterns, song localizations) ──

    private static void ImportEntityTablesPhase2(
        PatchPackage package,
        List<CsvImportEntry> entries,
        CancellationToken cancellationToken)
    {
        var songDict = package.Songs.ToDictionary(s => s.Id);
        var hasPatterns = false;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.TableName == "song_songPattern")
            {
                var patterns = ReadWithSchema<SongPattern, PatternCsvSchema>(entry.FilePath);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pattern in patterns)
                {
                    if (!songDict.TryGetValue(pattern.SongId, out var song))
                        continue;

                    var key = $"{pattern.SongId}::{pattern.PatternId}";
                    if (!seen.Add(key))
                        continue;

                    song.Patterns.Add(pattern);
                    hasPatterns = true;
                }
            }
            else if (IsLocalizedTable(entry.TableName, "song_desc"))
            {
                var lang = entry.LanguageCode ?? ExtractLanguageSuffix(entry.TableName);
                if (string.IsNullOrWhiteSpace(lang))
                    continue;

                var schema = new SongDescCsvSchema(lang);
                using var stream = File.OpenRead(entry.FilePath);
                var localizations = schema.ReadCsv(stream, throwOnMissingColumn: false);

                foreach (var loc in localizations)
                {
                    if (!songDict.TryGetValue(loc.SongId, out var song))
                        continue;

                    if (!string.IsNullOrWhiteSpace(loc.FullName)
                        || !string.IsNullOrWhiteSpace(loc.Genre)
                        || !string.IsNullOrWhiteSpace(loc.ArtistName)
                        || !string.IsNullOrWhiteSpace(loc.ComposedBy)
                        || !string.IsNullOrWhiteSpace(loc.Singer)
                        || !string.IsNullOrWhiteSpace(loc.FeatBy)
                        || !string.IsNullOrWhiteSpace(loc.ArrangedBy)
                        || !string.IsNullOrWhiteSpace(loc.VisualizedBy))
                    {
                        song.Localizations[lang] = loc;
                    }
                }
            }
        }

        if (hasPatterns)
        {
            foreach (var song in songDict.Values)
            {
                song.Patterns.Sort((left, right) =>
                {
                    var lineCmp = left.Line.CompareTo(right.Line);
                    return lineCmp != 0
                        ? lineCmp
                        : left.Signature.CompareTo(right.Signature);
                });
            }
        }
    }

    // ── Phase 3: lookup tables ──

    private static void ImportLookupTables(
        PatchPackage package,
        List<CsvImportEntry> entries,
        CancellationToken cancellationToken)
    {
        var achievementDict = package.Achievements.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var questDict = package.Quests.ToDictionary(q => q.Id, StringComparer.OrdinalIgnoreCase);
        var productDict = package.Products.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var itemDict = package.Items.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Sub-pass 3a: entity-creating lookups (quest_desc) and independent lookups
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsLocalizedTable(entry.TableName, "acievement_desc"))
            {
                var lang = entry.LanguageCode ?? ExtractLanguageSuffix(entry.TableName);
                if (string.IsNullOrWhiteSpace(lang)) continue;
                var schema = new AchievementDescCsvSchema(lang);
                using var stream = File.OpenRead(entry.FilePath);
                schema.ReadCsv(stream, achievementDict);
            }
            else if (IsLocalizedTable(entry.TableName, "quest_desc"))
            {
                var lang = entry.LanguageCode ?? ExtractLanguageSuffix(entry.TableName);
                if (string.IsNullOrWhiteSpace(lang)) continue;
                var schema = new QuestDescCsvSchema(lang);
                using var stream = File.OpenRead(entry.FilePath);
                schema.ReadCsv(stream, questDict);

                foreach (var (id, quest) in questDict)
                {
                    if (!package.Quests.Any(q => q.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                        package.Quests.Add(quest);
                }
            }
            else if (entry.TableName == "category_categoryproduct")
            {
                var schema = new CategoryProductCsvSchema();
                using var stream = File.OpenRead(entry.FilePath);
                schema.ReadCsv(stream, productDict);
            }
            else if (IsLocalizedTable(entry.TableName, "item_desc"))
            {
                var lang = entry.LanguageCode ?? ExtractLanguageSuffix(entry.TableName);
                if (string.IsNullOrWhiteSpace(lang)) continue;
                var schema = new ItemDescCsvSchema(lang);
                using var stream = File.OpenRead(entry.FilePath);
                schema.ReadCsv(stream, itemDict);
            }
        }

        // Sub-pass 3b: quest_mission_desc (depends on quests existing from 3a)
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsLocalizedTable(entry.TableName, "quest_mission_desc"))
            {
                var lang = entry.LanguageCode ?? ExtractLanguageSuffix(entry.TableName);
                if (string.IsNullOrWhiteSpace(lang)) continue;
                var schema = new QuestMissionDescCsvSchema(lang);
                using var stream = File.OpenRead(entry.FilePath);
                schema.ReadCsv(stream, questDict);
            }
        }
    }

    // ── Preview links ──

    private static void BuildPreviewLinks(PatchPackage package)
    {
        var previewPaths = package.Resources
            .Where(resource => resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
            .Select(resource => resource.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var song in package.Songs)
        {
            if (!string.IsNullOrWhiteSpace(song.Name) && previewPaths.Contains(song.Name))
            {
                song.PreviewPackageRelativePath = song.Name;
                continue;
            }

            var songIdMatch = previewPaths.FirstOrDefault(path =>
                path.Contains(song.Id.ToString(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(songIdMatch))
            {
                song.PreviewPackageRelativePath = songIdMatch;
            }
        }
    }

    // ── Schema helpers ──

    private static List<T> ReadWithSchema<T, TSchema>(string filePath)
        where TSchema : CsvSchema<T>, new()
    {
        using var stream = File.OpenRead(filePath);
        return new TSchema().ReadCsv(stream, throwOnMissingColumn: false);
    }

    // ── Path helpers ──

    private static string GetTableName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    private static string? GetLanguageCode(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[0].Equals("table", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }

    private static string? ExtractLanguageSuffix(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? null : tableName[(index + 1)..];
    }

    private static bool IsLocalizedTable(string tableName, string logicalName)
        => tableName.StartsWith(logicalName + "_", StringComparison.OrdinalIgnoreCase);

    // ── Existing helpers ──

    private async Task<string> EnsureCsvFileAsync(
        string sourcePath,
        string tempRoot,
        string relativePath,
        bool compressed,
        CancellationToken cancellationToken)
    {
        if (!compressed)
        {
            return sourcePath;
        }

        var destinationPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? tempRoot);
        await FileUtility.DecompressFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        return destinationPath;
    }

    private static string ResolveSourcePath(string packageRoot, string relativePath, bool compressed)
    {
        var uncompressedPath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var compressedPath = uncompressedPath + ".lz4";
        if (compressed && File.Exists(compressedPath))
        {
            return compressedPath;
        }

        if (File.Exists(uncompressedPath))
        {
            return uncompressedPath;
        }

        if (File.Exists(compressedPath))
        {
            return compressedPath;
        }

        throw new FileNotFoundException($"Could not find package file '{relativePath}'.", uncompressedPath);
    }

    // ── Shared resource import (slang etc.) ──

    private static void ImportSharedResource(
        PatchPackage package,
        string projectRoot,
        string relativePath,
        PatchFileEntry entry,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var archivedPath = Path.Combine(projectRoot, "resources", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(archivedPath) ?? projectRoot);

        if (sourcePath is not null)
        {
            if (entry.Compressed)
            {
                FileUtility.DecompressFileAsync(sourcePath, archivedPath, cancellationToken)
                    .GetAwaiter().GetResult();
            }
            else
            {
                using var source = File.OpenRead(sourcePath);
                using var dest = File.Create(archivedPath);
                source.CopyTo(dest);
            }
        }

        var resourceFile = package.Resources.FirstOrDefault(r =>
            r.FileName.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

        if (resourceFile is null)
        {
            resourceFile = new ResourceFile
            {
                FileName = relativePath,
                Category = "slang",
                Compressed = entry.Compressed,
                AcquireOnDemand = entry.AcquireOnDemand
            };
            package.Resources.Add(resourceFile);
        }

        var existing = resourceFile.PlatformManifest
            .FirstOrDefault(m => m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            resourceFile.PlatformManifest.Add(new PlatformManifestEntry
            {
                Platform = "share",
                Exist = sourcePath is not null,
                SourceFileSize = entry.FileSize,
                SourceChecksum = entry.Checksum,
                SourceCompressedFileSize = entry.CompressedFileSize,
                SourceCompressedChecksum = entry.CompressedChecksum,
                Checksum = string.Empty
            });
        }
    }

    private static string? TryGetVersion(string packageRoot)
    {
        var parent = Directory.GetParent(packageRoot);
        return parent?.Name.Contains('.', StringComparison.Ordinal) == true ? parent.Name : null;
    }

    private static string? TryGetPlatform(string packageRoot)
        => new DirectoryInfo(packageRoot).Name;

    // ── Nested types ──

    private sealed record CsvImportEntry(string FilePath, string TableName, string? LanguageCode, string RelativePath);
}
