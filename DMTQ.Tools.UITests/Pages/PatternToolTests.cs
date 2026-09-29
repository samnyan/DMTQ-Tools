using System.Text;
using System.Reflection;
using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;
using FluentAssertions;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class PatternToolTests : BlazorUITestBase
{
    [TestMethod]
    public void ShowsEmptyStateAndPatternActions()
    {
        var state = CreateStateWithEmptyPackage();
        RegisterAllServices(state);

        var cut = RenderWithProviders<PatternTool>();

        cut.Find("h1").TextContent.Should().NotBeNullOrWhiteSpace();
        cut.FindAll("fluent-button").Should().HaveCount(2);
        cut.Markup.Should().NotContain("fluent-data-grid");
    }

    [TestMethod]
    public async Task LoadsPatternAndShowsMetadataAndLists()
    {
        var source = CreatePattern();
        var path = Path.Combine(Path.GetTempPath(), "pattern-tool-test.bytes");
        File.WriteAllBytes(path, new PatternBinarySerializer().Serialize(source, PatternFormat.Bytes));

        try
        {
            var state = CreateStateWithEmptyPackage();
            RegisterAllServices(state);
            FilePicker.PickResult = path;

            var cut = RenderWithProviders<PatternTool>();
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()[0].Instance.OnClick.InvokeAsync(new()));
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("fluent-data-grid"));

            cut.Markup.Should().Contain("kick.ogg");
            cut.Markup.Should().Contain("Tempo (3)");
            cut.Markup.Should().Contain("Dur=6");
            cut.Markup.Should().Contain("Volume=120");
            cut.Markup.Should().Contain("Tempo=140");
            cut.Markup.Should().Contain("Compatibility checks");
            var eventRows = (System.Collections.IEnumerable)cut.Instance.GetType()
                .GetProperty("EventItems", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(cut.Instance)!;
            eventRows.Cast<object>()
                .Select(row => row.GetType().GetProperty("Track")!.GetValue(row))
                .Should().Contain(1);

            cut.Instance.GetType()
                .GetField("targetFormat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, PatternFormat.Pt);
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
                .Single(button => button.Markup.Contains("Save As", StringComparison.Ordinal))
                .Instance.OnClick.InvokeAsync(new()));
            cut.WaitForAssertion(() => FileSaver.SavedContent.Should().NotBeNull());
            FileSaver.SuggestedFileName.Should().EndWith(".pt");
            new PatternBinarySerializer()
                .Deserialize(FileSaver.SavedContent!, PatternFormat.Pt)
                .CommandCount.Should().Be(5);

            cut.Instance.GetType()
                .GetField("targetFormat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, PatternFormat.Text);
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
                .Single(button => button.Markup.Contains("Save As", StringComparison.Ordinal))
                .Instance.OnClick.InvokeAsync(new()));
            cut.WaitForAssertion(() => FileSaver.SuggestedFileName.Should().EndWith(".txt"));
            new PatternTextSerializer()
                .Deserialize(Encoding.UTF8.GetString(FileSaver.SavedContent!))
                .CommandCount.Should().Be(5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadsExtensionlessPatternAsBytesWhenEmbedded()
    {
        var source = CreatePattern();
        var path = Path.Combine(Path.GetTempPath(), $"pattern-tool-test-{Guid.NewGuid():N}");
        File.WriteAllBytes(path, new PatternBinarySerializer().Serialize(source, PatternFormat.Bytes));

        try
        {
            var state = CreateStateWithEmptyPackage();
            RegisterAllServices(state);
            var fragment = Render(builder =>
            {
                builder.OpenComponent<FluentProviders>(0);
                builder.CloseComponent();
                builder.OpenComponent<PatternTool>(1);
                builder.AddAttribute(2, nameof(PatternTool.InitialFilePath), path);
                builder.AddAttribute(3, nameof(PatternTool.Embedded), true);
                builder.CloseComponent();
            });
            var cut = fragment.FindComponent<PatternTool>();

            cut.WaitForAssertion(() =>
            {
                cut.Markup.Should().Contain("bytes");
                cut.Markup.Should().Contain("kick.ogg");
                cut.Markup.Should().NotContain("Open pattern file");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PatternDocument CreatePattern()
    {
        var pattern = new PatternDocument();
        pattern.Header.PositionsPerMeasure = 192;
        pattern.Header.InitialBpm = 128;
        pattern.Header.EndPosition = 3840;
        pattern.Sounds.Add(new PatternSound { Id = 1, FileName = "kick.ogg" });
        var track = new PatternTrack { Id = 0, Name = "Main", EndPosition = 3840 };
        track.Commands.Add(PatternCommand.CreateNote(192, 1, 127, 64, 5, 6));
        track.Commands.Add(PatternCommand.CreateVolume(0, 120));
        track.Commands.Add(PatternCommand.CreateVolume(0, 120));
        track.Commands.Add(PatternCommand.CreateBpmChange(768, 140));
        pattern.Tracks.Add(track);
        var secondTrack = new PatternTrack { Id = 7, Name = "Secondary", EndPosition = 3840 };
        secondTrack.Commands.Add(PatternCommand.CreateNote(192, 1, 100, 64, 0, 4));
        pattern.Tracks.Add(secondTrack);
        return pattern;
    }
}
