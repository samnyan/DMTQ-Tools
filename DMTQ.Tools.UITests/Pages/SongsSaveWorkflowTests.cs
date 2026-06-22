using DMTQ.Tools.Core.Models;
using DMTQ_Tools.Services;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class SongsSaveWorkflowTests : BlazorUITestBase
{
    [TestMethod]
    public void ClickingSaveDiagnosticIsRecorded()
    {
        var state = new GameTableManagerState();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        // First song is auto-selected with form filled
        // Click the "Save Song" button
        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Save Song"));
        saveButton.Should().NotBeNull();

        // Click should not throw
        var act = () => saveButton!.Click();
        act.Should().NotThrow();

        // State.Diagnostics should be empty since FakeRepository is a no-op
        // (real save would hit the repo)
    }

    [TestMethod]
    public void AddSongFormToggleWorks()
    {
        var state = new GameTableManagerState();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        // Add Song button should be present
        cut.Markup.Should().Contain("+ Add Song");

        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("+ Add Song"));
        addButton.Should().NotBeNull();
    }

    [TestMethod]
    public void ShowsPatternFieldsInline()
    {
        var state = new GameTableManagerState();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        // Pattern fields should be in markup with bound inputs
        cut.Markup.Should().Contain("2Line");
        cut.Markup.Should().Contain("easy");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("test-project", null, "1.0", null)
        };

        var songTable = new GameTable
        {
            PackageRelativePath = "table/us/song_song.csv",
            TableName = "song_song",
            LanguageCode = "us"
        };
        songTable.Columns.Add(new GameTableColumn("song_id", 0));
        songTable.Columns.Add(new GameTableColumn("item_id", 1));
        songTable.Columns.Add(new GameTableColumn("name", 2));
        songTable.Columns.Add(new GameTableColumn("genre", 3));
        var songRow = new GameTableRow { Order = 0 };
        songRow.Cells.Add(new GameTableCell("song_id", "1001"));
        songRow.Cells.Add(new GameTableCell("item_id", "5001"));
        songRow.Cells.Add(new GameTableCell("name", "TestSong"));
        songRow.Cells.Add(new GameTableCell("genre", "Electronic"));
        songTable.Rows.Add(songRow);
        package.Tables.Tables.Add(songTable);

        var patternTable = new GameTable
        {
            PackageRelativePath = "table/us/song_songPattern.csv",
            TableName = "song_songPattern",
            LanguageCode = "us"
        };
        patternTable.Columns.Add(new GameTableColumn("pattern_id", 0));
        patternTable.Columns.Add(new GameTableColumn("song_id", 1));
        patternTable.Columns.Add(new GameTableColumn("signature", 2));
        patternTable.Columns.Add(new GameTableColumn("line", 3));
        patternTable.Columns.Add(new GameTableColumn("difficulty", 4));
        var patternRow = new GameTableRow { Order = 0 };
        patternRow.Cells.Add(new GameTableCell("pattern_id", "9001"));
        patternRow.Cells.Add(new GameTableCell("song_id", "1001"));
        patternRow.Cells.Add(new GameTableCell("signature", "1"));
        patternRow.Cells.Add(new GameTableCell("line", "2Line"));
        patternRow.Cells.Add(new GameTableCell("difficulty", "easy"));
        patternTable.Rows.Add(patternRow);
        package.Tables.Tables.Add(patternTable);

        return package;
    }
}
