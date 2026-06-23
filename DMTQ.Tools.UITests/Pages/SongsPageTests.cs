using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;

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

        var cut = RenderWithProviders<Songs>();

        cut.Markup.Should().Contain("Songs");
        cut.Markup.Should().Contain("1001");
        cut.Markup.Should().Contain("Add Song");
    }

    [TestMethod]
    public void ShowsDataGridColumns()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Songs>();

        cut.Markup.Should().Contain("Song ID");
        cut.Markup.Should().Contain("Genre");
        cut.Markup.Should().Contain("Patterns");
    }

    [TestMethod]
    public void ShowsNoSongsMessageWhenEmpty()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(new PatchPackage { ProjectInfo = new ProjectInfo("test-project", null, "1.0", null) });
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Songs>();

        cut.Markup.Should().Contain("No songs found");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test-project", null, "1.0", null) };

        var songTable = new GameTable { PackageRelativePath = "table/us/song_song.csv", TableName = "song_song", LanguageCode = "us" };
        songTable.Columns.Add(new GameTableColumn("song_id", 0));
        songTable.Columns.Add(new GameTableColumn("name", 1));
        songTable.Columns.Add(new GameTableColumn("genre", 2));
        songTable.Columns.Add(new GameTableColumn("artist_name", 3));
        var songRow = new GameTableRow { Order = 0 };
        songRow.Cells.Add(new GameTableCell("song_id", "1001"));
        songRow.Cells.Add(new GameTableCell("name", "TestSong"));
        songRow.Cells.Add(new GameTableCell("genre", "Electronic"));
        songRow.Cells.Add(new GameTableCell("artist_name", "TestArtist"));
        songTable.Rows.Add(songRow);
        package.Tables.Tables.Add(songTable);

        var patternTable = new GameTable { PackageRelativePath = "table/us/song_songPattern.csv", TableName = "song_songPattern", LanguageCode = "us" };
        patternTable.Columns.Add(new GameTableColumn("pattern_id", 0));
        patternTable.Columns.Add(new GameTableColumn("song_id", 1));
        patternTable.Columns.Add(new GameTableColumn("line", 2));
        patternTable.Columns.Add(new GameTableColumn("difficulty", 3));
        var patternRow = new GameTableRow { Order = 0 };
        patternRow.Cells.Add(new GameTableCell("pattern_id", "9001"));
        patternRow.Cells.Add(new GameTableCell("song_id", "1001"));
        patternRow.Cells.Add(new GameTableCell("line", "2"));
        patternRow.Cells.Add(new GameTableCell("difficulty", "1"));
        patternTable.Rows.Add(patternRow);
        package.Tables.Tables.Add(patternTable);

        // Also add entity Song so BuildCatalog can find it
        var song = new Song { Id = 1001, Name = "TestSong", Genre = "Electronic", ArtistName = "TestArtist" };
        song.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001, Line = 2, Difficulty = 1 });
        package.Songs.Add(song);

        return package;
    }
}
