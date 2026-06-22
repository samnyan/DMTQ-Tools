using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

/// <summary>Edits songs directly on the entity model stored in <see cref="PatchPackage.Songs"/>.</summary>
public sealed class SongEditService
{
    public void UpdateSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(song.Id);

        if (!package.Songs.Any(s => s.Id.Equals(song.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Song '{song.Id}' was not found.");
    }

    public void AddSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(song.Id);

        if (package.Songs.Any(s => s.Id.Equals(song.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Song '{song.Id}' already exists.");

        package.Songs.Add(song);
    }

    public void UpdatePattern(PatchPackage package, string songId, string patternId,
        SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var song = package.Songs.FirstOrDefault(
            s => s.Id.Equals(songId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        if (!song.Patterns.Any(p => p.PatternId.Equals(patternId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"Pattern '{patternId}' for song '{songId}' was not found.");
    }

    public void AddPattern(PatchPackage package, string songId, SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var song = package.Songs.FirstOrDefault(
            s => s.Id.Equals(songId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Song '{songId}' does not exist.");

        if (song.Patterns.Any(p =>
                p.PatternId.Equals(pattern.PatternId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"Pattern '{pattern.PatternId}' already exists for song '{songId}'.");

        song.Patterns.Add(pattern);
    }

    public void RemoveSong(PatchPackage package, string songId)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(songId);

        var song = package.Songs.FirstOrDefault(
            s => s.Id.Equals(songId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Song '{songId}' was not found.");

        package.Songs.Remove(song);
    }
}
