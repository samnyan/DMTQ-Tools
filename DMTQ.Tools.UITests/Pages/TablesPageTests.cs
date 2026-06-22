using DMTQ.Tools.Core.Models;
using DMTQ_Tools.Services;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class TablesPageTests : BlazorUITestBase
{
    [TestMethod]
    public void RendersHintWhenNoPackageLoaded()
    {
        var state = new GameTableManagerState();
        RegisterAllServices(state);

        var cut = Render<Tables>();

        cut.Markup.Should().Contain("Import or open a project");
    }

    [TestMethod]
    public void ShowsLogicalTablesWhenPackageLoaded()
    {
        var state = new GameTableManagerState();
        state.SetPackage(CreatePackageWithSongTable());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Tables>();

        cut.Markup.Should().Contain("song_song");
        cut.Markup.Should().Contain("Advanced raw CSV files");
        cut.Markup.Should().Contain("table/us/song_song.csv");
    }

    private static PatchPackage CreatePackageWithSongTable()
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
        songTable.Columns.Add(new GameTableColumn("name", 1));
        var row = new GameTableRow { Order = 0 };
        row.Cells.Add(new GameTableCell("song_id", "1001"));
        row.Cells.Add(new GameTableCell("name", "TestSong"));
        songTable.Rows.Add(row);
        package.Tables.Tables.Add(songTable);

        return package;
    }
}
