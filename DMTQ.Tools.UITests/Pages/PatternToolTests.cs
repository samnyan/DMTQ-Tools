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
            cut.Markup.Should().Contain("BpmChange");

            cut.Instance.GetType()
                .GetField("targetFormat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, PatternFormat.Pt);
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()[1].Instance.OnClick.InvokeAsync(new()));
            cut.WaitForAssertion(() => FileSaver.SavedContent.Should().NotBeNull());
            FileSaver.SuggestedFileName.Should().EndWith(".pt");
            new PatternBinarySerializer()
                .Deserialize(FileSaver.SavedContent!, PatternFormat.Pt)
                .CommandCount.Should().Be(2);

            cut.Instance.GetType()
                .GetField("targetFormat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, PatternFormat.Text);
            await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()[1].Instance.OnClick.InvokeAsync(new()));
            cut.WaitForAssertion(() => FileSaver.SuggestedFileName.Should().EndWith(".txt"));
            new PatternTextSerializer()
                .Deserialize(Encoding.UTF8.GetString(FileSaver.SavedContent!))
                .CommandCount.Should().Be(2);
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
        track.Commands.Add(PatternCommand.CreateBpmChange(768, 140));
        pattern.Tracks.Add(track);
        return pattern;
    }
}
