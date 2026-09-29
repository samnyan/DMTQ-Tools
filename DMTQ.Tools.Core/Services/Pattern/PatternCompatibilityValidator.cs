using DMTQ.Tools.Core.Models.Pattern;

namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Checks pattern data against limits and lookups used by the Unity Bytes loader.
/// </summary>
public sealed class PatternCompatibilityValidator
{
    private const int UnityTrackLimit = 64;
    private const int UnitySoundLimit = 1024;

    public IReadOnlyList<PatternValidationIssue> Validate(PatternDocument pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var issues = new List<PatternValidationIssue>();
        if (pattern.SourceFormat != PatternFormat.Bytes)
        {
            issues.Add(new(PatternValidationSeverity.Info, "Pattern.Validation.BytesOnly"));
            return issues;
        }

        if (pattern.Tracks.Count > UnityTrackLimit)
        {
            issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.TrackLimit", UnityTrackLimit));
        }

        if (pattern.Header.DeclaredCommandCount > 0 && pattern.Header.DeclaredCommandCount != pattern.CommandCount)
        {
            issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.EventCount", pattern.Header.DeclaredCommandCount));
        }

        var soundsById = pattern.Sounds.GroupBy(sound => sound.Id).ToDictionary(group => group.Key, group => group.Count());
        foreach (var duplicate in soundsById.Where(group => group.Value > 1))
        {
            issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.DuplicateSound", duplicate.Key));
        }

        foreach (var sound in pattern.Sounds.Where(sound => sound.Id >= UnitySoundLimit))
        {
            issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.SoundLimit", sound.Id));
        }

        var soundIds = soundsById.Keys.ToHashSet();
        foreach (var (track, trackIndex) in pattern.Tracks.Select((track, index) => (track, index)))
        {
            foreach (var command in track.Commands)
            {
                if (command.Position < 0)
                {
                    issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.NegativeTick", trackIndex));
                }

                // Charts commonly use ID 0 for silent notes. When it is not loaded, the game's GetSound(0)
                // lookup returns null and playback skips the sound; that should not be reported as a broken reference.
                if (command.KnownType == PatternCommandType.Note
                    && command.SoundIndex != 0
                    && !soundIds.Contains(command.SoundIndex))
                {
                    issues.Add(new(PatternValidationSeverity.Warning, "Pattern.Validation.MissingSound", command.SoundIndex));
                }
            }
        }

        return issues;
    }
}

public enum PatternValidationSeverity
{
    Info,
    Warning
}

public sealed record PatternValidationIssue(
    PatternValidationSeverity Severity,
    string ResourceKey,
    object? Argument = null);
