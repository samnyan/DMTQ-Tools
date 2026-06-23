using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Edits songs directly on the entity model stored in <see cref="PatchPackage.Songs"/>.</summary>
public sealed class SongEditService
{
    public void UpdateSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);

        if (!package.Songs.Any(s => s.Id == song.Id))
            throw new InvalidOperationException($"Song '{song.Id}' was not found.");
    }

    public void AddSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);

        if (package.Songs.Any(s => s.Id == song.Id))
            throw new InvalidOperationException($"Song '{song.Id}' already exists.");

        package.Songs.Add(song);
    }

    public void UpdatePattern(PatchPackage package, int songId, int patternId,
        SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var song = package.Songs.FirstOrDefault(
            s => s.Id == songId)
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        if (!song.Patterns.Any(p => p.PatternId == patternId))
            throw new InvalidOperationException(
                $"Pattern '{patternId}' for song '{songId}' was not found.");
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

        song.Patterns.Add(pattern);
    }

    public void RemoveSong(PatchPackage package, int songId)
    {
        ArgumentNullException.ThrowIfNull(package);

        var song = package.Songs.FirstOrDefault(
            s => s.Id == songId)
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        package.Songs.Remove(song);
    }
}
