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

    [TestMethod]
    public void AddSong_AppendsRequiredRowsAcrossSongTables()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview", "asset_id"],
            ["1001", "preview/old.opus", "asset_old"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty", "level"],
            ["9001", "1001", "hard", "10"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title", "description"],
            ["1001", "Old US", "Old description"]));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp",
            ["song_id", "title", "description"],
            ["1001", "Old JP", "Old description"]));
        package.Tables.Tables.Add(CreateTable("table/us/item_desc_us.csv", "item_desc_us", "us",
            ["item_id", "name", "description"],
            ["7001", "Old Item", "Old item description"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_product.csv", "product_product", "us",
            ["product_id", "song_id"],
            ["5001", "1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_item.csv", "product_item", "us",
            ["product_id", "item_id"],
            ["5001", "7001"]));
        package.Tables.Tables.Add(CreateTable("table/us/category_categoryproduct.csv", "category_categoryproduct", "us",
            ["category_id", "product_id"],
            ["3001", "5001"]));

        var request = new AddSongRequest
        {
            SongId = "1002",
            ProductId = "5002",
            ItemId = "7002",
            CategoryId = "3001",
            PreviewPackageRelativePath = "preview/new_song.p.opus"
        };
        request.SongFields["asset_id"] = "asset_1002";
        request.TitlesByLanguage["us"] = "New Song";
        request.TitlesByLanguage["jp"] = "新曲";
        request.DescriptionsByLanguage["us"] = "New description";
        request.ItemNamesByLanguage["us"] = "New Song Item";
        request.ItemDescriptionsByLanguage["us"] = "New item description";
        request.Patterns.Add(new AddSongPatternRequest
        {
            PatternId = "9002",
            Difficulty = "expert",
            Level = "13"
        });

        new SongEditService().AddSong(package, request);

        var songRows = package.Tables.Tables.Single(t => t.TableName == "song_song").Rows;
        songRows.Should().HaveCount(2);
        Cell(songRows[1], "song_id").Should().Be("1002");
        Cell(songRows[1], "preview").Should().Be("preview/new_song.p.opus");
        Cell(songRows[1], "asset_id").Should().Be("asset_1002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[1], "title").Should().Be("New Song");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_jp").Rows[1], "title").Should().Be("新曲");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[1], "pattern_id").Should().Be("9002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "product_product").Rows[1], "product_id").Should().Be("5002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "product_item").Rows[1], "item_id").Should().Be("7002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "category_categoryproduct").Rows[1], "category_id").Should().Be("3001");
    }

    [TestMethod]
    public void AddSong_ThrowsWhenIdsAlreadyExist()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"],
            ["1001", "preview/old.opus"]));

        var request = new AddSongRequest
        {
            SongId = "1001",
            ProductId = "5002",
            ItemId = "7002",
            CategoryId = "3001"
        };

        var action = () => new SongEditService().AddSong(package, request);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Song '1001' already exists.");
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
