using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class SongEditorPageTests : BlazorUITestBase
{
    [TestMethod]
    public void RendersCreateFormForNewSong()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "new"));

        cut.Markup.Should().Contain("New Song");
        cut.Markup.Should().Contain("Song ID");
        cut.Markup.Should().Contain("Save Song");
    }

    [TestMethod]
    public void RendersEditFormForExistingSong()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        cut.Markup.Should().Contain("Edit: 1001");
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("Genre");
        cut.Markup.Should().Contain("Save Song");
        cut.Markup.Should().Contain("Patterns");
    }

    [TestMethod]
    public void ShowsNoPackageWarningWhenPackageMissing()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        cut.Markup.Should().Contain("Open or import a project before editing songs");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test", null, "1.0", null) };
        var st = new GameTable { PackageRelativePath = "table/us/song_song.csv", TableName = "song_song", LanguageCode = "us" };
        st.Columns.Add(new GameTableColumn("song_id", 0)); st.Columns.Add(new GameTableColumn("name", 1)); st.Columns.Add(new GameTableColumn("genre", 2)); st.Columns.Add(new GameTableColumn("artist_name", 3));
        var sr = new GameTableRow { Order = 0 }; sr.Cells.Add(new GameTableCell("song_id", "1001")); sr.Cells.Add(new GameTableCell("name", "T")); sr.Cells.Add(new GameTableCell("genre", "G")); sr.Cells.Add(new GameTableCell("artist_name", "A"));
        st.Rows.Add(sr); package.Tables.Tables.Add(st);

        var pt = new GameTable { PackageRelativePath = "table/us/song_songPattern.csv", TableName = "song_songPattern", LanguageCode = "us" };
        pt.Columns.Add(new GameTableColumn("pattern_id", 0)); pt.Columns.Add(new GameTableColumn("song_id", 1)); pt.Columns.Add(new GameTableColumn("line", 2)); pt.Columns.Add(new GameTableColumn("difficulty", 3));
        var pr = new GameTableRow { Order = 0 }; pr.Cells.Add(new GameTableCell("pattern_id", "9001")); pr.Cells.Add(new GameTableCell("song_id", "1001")); pr.Cells.Add(new GameTableCell("line", "2")); pr.Cells.Add(new GameTableCell("difficulty", "1"));
        pt.Rows.Add(pr); package.Tables.Tables.Add(pt);

        // Also add entity Song so BuildCatalog can find it
        var song = new Song { Id = 1001, Name = "T", Genre = "G", ArtistName = "A" };
        song.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001, Line = 2, Difficulty = 1 });
        package.Songs.Add(song);

        return package;
    }
}
