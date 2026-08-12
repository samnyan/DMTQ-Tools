using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Edits songs directly on the entity model stored in <see cref="PatchPackage.Songs"/>.</summary>
public sealed class SongEditService
{
    /// <summary>Creates a detached editable copy of a song.</summary>
    /// <param name="source">The persisted song to copy.</param>
    /// <param name="id">Optional replacement ID for a new song.</param>
    /// <returns>A detached song draft with copied localizations and patterns.</returns>
    public Song CreateDraft(Song source, int? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var draft = new Song { Id = id ?? source.Id };
        CopySongData(source, draft);
        return draft;
    }

    public void UpdateSong(PatchPackage package, Song song, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);

        var songs = package.GetPlatformTables(platform).Songs;
        var existing = songs.FirstOrDefault(s => s.Id == song.Id)
            ?? throw new InvalidOperationException($"Song '{song.Id}' was not found.");

        CopySongData(song, existing);
    }

    public void AddSong(PatchPackage package, Song song, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);

        var songs = package.GetPlatformTables(platform).Songs;
        if (songs.Any(s => s.Id == song.Id))
            throw new InvalidOperationException($"Song '{song.Id}' already exists.");

        songs.Add(CreateDraft(song));
    }

    public void UpdatePattern(PatchPackage package, int songId, int patternId,
        SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var song = package.Songs.FirstOrDefault(
            s => s.Id == songId)
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        UpdatePattern(song, patternId, pattern);
    }

    /// <summary>Updates a pattern in a detached song draft.</summary>
    /// <param name="song">The detached song draft.</param>
    /// <param name="patternId">The existing pattern ID.</param>
    /// <param name="pattern">The edited pattern values.</param>
    public void UpdatePattern(Song song, int patternId, SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(pattern);

        var existing = song.Patterns.FirstOrDefault(p => p.PatternId == patternId)
            ?? throw new InvalidOperationException(
                $"Pattern '{patternId}' for song '{song.Id}' was not found.");

        CopyPatternData(pattern, existing);
    }

    public void AddPattern(PatchPackage package, int songId, SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var song = package.Songs.FirstOrDefault(
            s => s.Id == songId)
            ?? throw new InvalidOperationException($"Song '{songId}' does not exist.");

        if (song.Patterns.Any(p =>
                p.PatternId == pattern.PatternId))
            throw new InvalidOperationException(
                $"Pattern '{pattern.PatternId}' already exists for song '{songId}'.");

        AddPattern(song, pattern);
    }

    /// <summary>Adds a pattern to a detached song draft.</summary>
    /// <param name="song">The detached song draft.</param>
    /// <param name="pattern">The pattern to add.</param>
    public void AddPattern(Song song, SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(pattern);

        if (song.Patterns.Any(p => p.PatternId == pattern.PatternId))
            throw new InvalidOperationException(
                $"Pattern '{pattern.PatternId}' already exists for song '{song.Id}'.");

        song.Patterns.Add(ClonePattern(pattern));
    }

    /// <summary>Removes a pattern from a detached song draft.</summary>
    /// <param name="song">The detached song draft.</param>
    /// <param name="patternId">The pattern ID to remove.</param>
    public void RemovePattern(Song song, int patternId)
    {
        ArgumentNullException.ThrowIfNull(song);

        var pattern = song.Patterns.FirstOrDefault(p => p.PatternId == patternId)
            ?? throw new InvalidOperationException(
                $"Pattern '{patternId}' for song '{song.Id}' was not found.");

        song.Patterns.Remove(pattern);
    }

    public void RemoveSong(PatchPackage package, int songId, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        var songs = package.GetPlatformTables(platform).Songs;
        var song = songs.FirstOrDefault(
            s => s.Id == songId)
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        songs.Remove(song);
    }

    private static void CopySongData(Song source, Song target)
    {
        target.ItemId = source.ItemId;
        target.Name = source.Name;
        target.FullName = source.FullName;
        target.Genre = source.Genre;
        target.ArtistName = source.ArtistName;
        target.OriginalBgaYn = source.OriginalBgaYn;
        target.LoopBgaYn = source.LoopBgaYn;
        target.ComposedBy = source.ComposedBy;
        target.Singer = source.Singer;
        target.FeatBy = source.FeatBy;
        target.ArrangedBy = source.ArrangedBy;
        target.VisualizedBy = source.VisualizedBy;
        target.CostGamePoint = source.CostGamePoint;
        target.CostGameCash = source.CostGameCash;
        target.Flag = source.Flag;
        target.Status = source.Status;
        target.FreeYn = source.FreeYn;
        target.HiddenYn = source.HiddenYn;
        target.OpenYn = source.OpenYn;
        target.TrackId = source.TrackId;
        target.ModDate = source.ModDate;
        target.Update = source.Update;
        target.PreviewPackageRelativePath = source.PreviewPackageRelativePath;

        target.Localizations.Clear();
        foreach (var (language, localization) in source.Localizations)
        {
            target.Localizations[language] = new SongLocalization
            {
                SongId = target.Id,
                FullName = localization.FullName,
                Genre = localization.Genre,
                ArtistName = localization.ArtistName,
                ComposedBy = localization.ComposedBy,
                Singer = localization.Singer,
                FeatBy = localization.FeatBy,
                ArrangedBy = localization.ArrangedBy,
                VisualizedBy = localization.VisualizedBy
            };
        }

        target.Patterns.Clear();
        target.Patterns.AddRange(source.Patterns.Select(ClonePattern));
    }

    private static SongPattern ClonePattern(SongPattern source)
        => new()
        {
            PatternId = source.PatternId,
            SongId = source.SongId,
            Name = source.Name,
            Line = source.Line,
            Signature = source.Signature,
            Difficulty = source.Difficulty,
            PointType = source.PointType,
            PointValue = source.PointValue,
            Flg = source.Flg,
            Update = source.Update
        };

    private static void CopyPatternData(SongPattern source, SongPattern target)
    {
        target.Name = source.Name;
        target.Line = source.Line;
        target.Signature = source.Signature;
        target.Difficulty = source.Difficulty;
        target.PointType = source.PointType;
        target.PointValue = source.PointValue;
        target.Flg = source.Flg;
        target.Update = source.Update;
    }
}
