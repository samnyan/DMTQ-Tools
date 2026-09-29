using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatternCompatibilityValidatorTests
{
    private readonly PatternCompatibilityValidator _validator = new();

    [TestMethod]
    public void BytesValidationFindsGameRelevantTrackSoundAndEventIssues()
    {
        var pattern = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        pattern.Header.DeclaredCommandCount = 2;
        pattern.Sounds.Add(new PatternSound { Id = 4, FileName = "first.ogg" });
        pattern.Sounds.Add(new PatternSound { Id = 4, FileName = "duplicate.ogg" });
        pattern.Sounds.Add(new PatternSound { Id = 1024, FileName = "out-of-range.ogg" });
        var track = new PatternTrack();
        track.Commands.Add(PatternCommand.CreateNote(0, 9, 100, 64, 0, 1));
        pattern.Tracks.Add(track);

        var issues = _validator.Validate(pattern);

        issues.Select(issue => issue.ResourceKey).Should().Contain([
            "Pattern.Validation.EventCount",
            "Pattern.Validation.DuplicateSound",
            "Pattern.Validation.SoundLimit",
            "Pattern.Validation.MissingSound"]);
    }

    [TestMethod]
    public void NonBytesFormatsAreExplicitlyMarkedAsNotRuntimeChecked()
    {
        var pattern = new PatternDocument { SourceFormat = PatternFormat.Pt };

        var issues = _validator.Validate(pattern);

        issues.Should().ContainSingle()
            .Which.ResourceKey.Should().Be("Pattern.Validation.BytesOnly");
    }

    [TestMethod]
    public void BytesValidationTreatsSoundIdZeroAsASilentNote()
    {
        var pattern = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        var track = new PatternTrack();
        track.Commands.Add(PatternCommand.CreateNote(120, 0, 127, 64, 0, 6));
        pattern.Tracks.Add(track);

        var issues = _validator.Validate(pattern);

        issues.Should().NotContain(issue => issue.ResourceKey == "Pattern.Validation.MissingSound");
    }
}
