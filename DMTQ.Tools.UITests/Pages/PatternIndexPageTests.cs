using DMTQ_Tools.Components.Pages;
using FluentAssertions;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Text.Json;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class PatternIndexPageTests : BlazorUITestBase
{
    [TestMethod]
    public void ShowsRefreshActionWithoutOfferingPatternImport()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<PatternIndex>();

        cut.Markup.Should().Contain("Refresh file index");
        cut.Markup.Should().NotContain("Import pattern");
        cut.FindComponents<FluentButton>().Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task ShowsStandardAndHeadphoneSizesOnOneLineWithoutPathColumn()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-index-ui-").FullName;
        try
        {
            var index = new
            {
                SchemaVersion = 2,
                LastIndexedAtUtc = DateTimeOffset.UtcNow,
                Patterns = new Dictionary<string, object>
                {
                    ["42"] = new
                    {
                        HasHeadphone = true,
                        Standard = new { RelativePath = "Patterns/42", FileSize = 123L, LastWriteTimeUtc = DateTimeOffset.UtcNow, TrackCount = 1, SoundCount = 2, EventCount = 3 },
                        Headphone = new { RelativePath = "Patterns/42_EARPHONE", FileSize = 456L, LastWriteTimeUtc = DateTimeOffset.UtcNow, TrackCount = 1, SoundCount = 2, EventCount = 3 }
                    }
                }
            };
            await File.WriteAllTextAsync(Path.Combine(root, "patterns.json"), JsonSerializer.Serialize(index));

            var state = CreateStateWithEmptyPackage();
            state.SetProjectRoot(root);
            RegisterAllServices(state);

            var cut = RenderWithProviders<PatternIndex>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("123b (H) 456b"));
            cut.Markup.Should().NotContain("File path");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SyncsSongPatternRowsIntoIndexAndShowsSongIdentityAndSeparateSearches()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-index-song-ui-").FullName;
        try
        {
            var state = CreateStateWithEmptyPackage();
            state.SetProjectRoot(root);
            var package = new DMTQ.Tools.Core.Models.Project.PatchPackage
            {
                ProjectInfo = new DMTQ.Tools.Core.Models.Project.ProjectInfo(root, null, null, null)
            };
            var song = new DMTQ.Tools.Core.Models.Entity.Song { Id = 1001, Name = "Example Song" };
            song.Patterns.Add(new DMTQ.Tools.Core.Models.Entity.SongPattern
            {
                PatternId = 42,
                SongId = song.Id,
                Line = 3,
                Signature = 2,
                Difficulty = 12
            });
            package.Songs.Add(song);
            state.SetPackage(package);
            RegisterAllServices(state);

            var cut = RenderWithProviders<PatternIndex>();
            cut.WaitForAssertion(() =>
            {
                cut.Markup.Should().Contain("1001");
                cut.Markup.Should().Contain("Example Song");
                cut.Markup.Should().Contain("42");
            });
            cut.FindAll("fluent-text-input").Should().HaveCount(3);
            cut.Markup.Should().Contain("Pattern table").And.Contain("Configured");
            cut.Markup.Should().Contain("Pattern file").And.Contain("All");
            cut.Markup.Should().Contain("Chart line count").And.Contain("Chart difficulty").And.Contain("Star rating");
            cut.Markup.Should().Contain("12");

            using var savedIndex = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "patterns.json")));
            savedIndex.RootElement.GetProperty("Patterns").GetProperty("42")
                .GetProperty("SongPatterns")[0].GetProperty("SongName").GetString().Should().Be("Example Song");
            savedIndex.RootElement.GetProperty("Patterns").GetProperty("42")
                .GetProperty("SongPatterns")[0].GetProperty("Line").GetInt32().Should().Be(3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
