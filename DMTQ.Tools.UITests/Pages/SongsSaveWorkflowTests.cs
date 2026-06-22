using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class SongsSaveWorkflowTests : BlazorUITestBase
{
    [TestMethod]
    public void SaveSongButtonExists()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("Save Song");
    }

    [TestMethod]
    public void AddSongButtonExists()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("+ Add Song");
    }

    [TestMethod]
    public void PatternFieldsBoundWithInlineInputs()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Songs>();

        cut.Markup.Should().Contain("2Line");
        cut.Markup.Should().Contain("easy");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test", null, "1.0", null) };
        var st = new GameTable { PackageRelativePath = "table/us/song_song.csv", TableName = "song_song", LanguageCode = "us" };
        st.Columns.Add(new GameTableColumn("song_id", 0)); st.Columns.Add(new GameTableColumn("name", 1)); st.Columns.Add(new GameTableColumn("genre", 2));
        var sr = new GameTableRow { Order = 0 }; sr.Cells.Add(new GameTableCell("song_id", "1001")); sr.Cells.Add(new GameTableCell("name", "T")); sr.Cells.Add(new GameTableCell("genre", "G"));
        st.Rows.Add(sr); package.Tables.Tables.Add(st);

        var pt = new GameTable { PackageRelativePath = "table/us/song_songPattern.csv", TableName = "song_songPattern", LanguageCode = "us" };
        pt.Columns.Add(new GameTableColumn("pattern_id", 0)); pt.Columns.Add(new GameTableColumn("song_id", 1)); pt.Columns.Add(new GameTableColumn("line", 2)); pt.Columns.Add(new GameTableColumn("difficulty", 3));
        var pr = new GameTableRow { Order = 0 }; pr.Cells.Add(new GameTableCell("pattern_id", "9001")); pr.Cells.Add(new GameTableCell("song_id", "1001")); pr.Cells.Add(new GameTableCell("line", "2Line")); pr.Cells.Add(new GameTableCell("difficulty", "easy"));
        pt.Rows.Add(pr); package.Tables.Tables.Add(pt);

        return package;
    }
}
