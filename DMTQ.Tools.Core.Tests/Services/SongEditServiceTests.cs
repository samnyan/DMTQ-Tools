using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongEditServiceTests
{
    [TestMethod]
    public void UpdateSong_UpdatesSharedSongDescriptionsAndPatterns()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview", "asset_id"],
            ["1001", "preview/old.opus", "asset_old"]));
        package.Tables.Tables.Add(CreateTable("table/jp/song_song.csv", "song_song", "jp",
            ["song_id", "preview", "asset_id"],
            ["1001", "preview/old.opus", "asset_old"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title", "description"],
            ["1001", "Old US", "Old US description"]));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp",
            ["song_id", "title", "description"],
            ["1001", "Old JP", "Old JP description"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty", "level"],
            ["9001", "1001", "hard", "10"]));

        var request = new SongEditRequest
        {
            SongId = "1001",
            PreviewPackageRelativePath = "preview/new.opus"
        };
        request.SourceFields["asset_id"] = "asset_new";
        request.TitlesByLanguage["us"] = "New US";
        request.TitlesByLanguage["jp"] = "New JP";
        request.DescriptionsByLanguage["us"] = "New US description";
        request.PatternDifficultyByPatternId["9001"] = "expert";
        request.PatternLevelByPatternId["9001"] = "12";

        new SongEditService().UpdateSong(package, request);

        package.Tables.Tables.Where(t => t.TableName == "song_song")
            .Select(t => Cell(t.Rows[0], "asset_id"))
            .Should().OnlyContain(value => value == "asset_new");
        package.Tables.Tables.Where(t => t.TableName == "song_song")
            .Select(t => Cell(t.Rows[0], "preview"))
            .Should().OnlyContain(value => value == "preview/new.opus");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[0], "title").Should().Be("New US");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_jp").Rows[0], "title").Should().Be("New JP");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[0], "description").Should().Be("New US description");
        var pattern = package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[0];
        Cell(pattern, "difficulty").Should().Be("expert");
        Cell(pattern, "level").Should().Be("12");
    }

    [TestMethod]
    public void UpdateSong_ThrowsWhenSongDoesNotExist()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"],
            ["1001", "preview/old.opus"]));

        var action = () => new SongEditService().UpdateSong(package, new SongEditRequest { SongId = "missing" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Song 'missing' was not found.");
    }

    private static PatchPackage CreatePackage()
        => new()
        {
            ProjectInfo = new ProjectInfo("project", null, "1.003.005", null)
        };

    private static GameTable CreateTable(string path, string tableName, string languageCode, string[] columns, params string[][] rows)
    {
        var table = new GameTable
        {
            PackageRelativePath = path,
            TableName = tableName,
            LanguageCode = languageCode
        };
        for (var i = 0; i < columns.Length; i++)
        {
            table.Columns.Add(new GameTableColumn(columns[i], i));
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = new GameTableRow { Order = rowIndex };
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                row.Cells.Add(new GameTableCell(columns[columnIndex], rows[rowIndex][columnIndex]));
            }
            table.Rows.Add(row);
        }

        return table;
    }

    private static string Cell(GameTableRow row, string columnName)
        => row.Cells.Single(cell => cell.ColumnName == columnName).Value;
}
