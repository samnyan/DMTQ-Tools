using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class SongsPageTests : BlazorUITestBase
{
    [TestMethod]
    public void RendersSongListWhenPackageIsLoaded()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("Songs");
        cut.Markup.Should().Contain("1001");
    }

    [TestMethod]
    public void ShowsFormFieldsWhenSongSelected()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("Item ID");
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("Genre");
        cut.Markup.Should().Contain("TestSong");
    }

    [TestMethod]
    public void ShowsPatternsTableWhenSongSelected()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("Song Patterns");
        cut.Markup.Should().Contain("9001");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("test-project", null, "1.0", null)
        };

        var songTable = new GameTable { PackageRelativePath = "table/us/song_song.csv", TableName = "song_song", LanguageCode = "us" };
        songTable.Columns.Add(new GameTableColumn("song_id", 0));
        songTable.Columns.Add(new GameTableColumn("item_id", 1));
        songTable.Columns.Add(new GameTableColumn("name", 2));
        songTable.Columns.Add(new GameTableColumn("genre", 3));
        songTable.Columns.Add(new GameTableColumn("artist_name", 4));
        var songRow = new GameTableRow { Order = 0 };
        songRow.Cells.Add(new GameTableCell("song_id", "1001"));
        songRow.Cells.Add(new GameTableCell("item_id", "5001"));
        songRow.Cells.Add(new GameTableCell("name", "TestSong"));
        songRow.Cells.Add(new GameTableCell("genre", "Electronic"));
        songRow.Cells.Add(new GameTableCell("artist_name", "TestArtist"));
        songTable.Rows.Add(songRow);
        package.Tables.Tables.Add(songTable);

        var patternTable = new GameTable { PackageRelativePath = "table/us/song_songPattern.csv", TableName = "song_songPattern", LanguageCode = "us" };
        patternTable.Columns.Add(new GameTableColumn("pattern_id", 0));
        patternTable.Columns.Add(new GameTableColumn("song_id", 1));
        patternTable.Columns.Add(new GameTableColumn("signature", 2));
        patternTable.Columns.Add(new GameTableColumn("line", 3));
        patternTable.Columns.Add(new GameTableColumn("difficulty", 4));
        patternTable.Columns.Add(new GameTableColumn("level", 5));
        var patternRow = new GameTableRow { Order = 0 };
        patternRow.Cells.Add(new GameTableCell("pattern_id", "9001"));
        patternRow.Cells.Add(new GameTableCell("song_id", "1001"));
        patternRow.Cells.Add(new GameTableCell("signature", "1"));
        patternRow.Cells.Add(new GameTableCell("line", "2Line"));
        patternRow.Cells.Add(new GameTableCell("difficulty", "easy"));
        patternRow.Cells.Add(new GameTableCell("level", "5"));
        patternTable.Rows.Add(patternRow);
        package.Tables.Tables.Add(patternTable);

        return package;
    }
}
