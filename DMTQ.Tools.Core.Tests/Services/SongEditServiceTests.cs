using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongEditServiceTests
{
    [TestMethod]
    public void UpdateSong_WritesSourceFieldsToAllSongInstances()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview", "asset_id"],
            ["1001", "preview/old.opus", "asset_old"]));

        var song = new Song { Id = "1001" };
        song.SourceFields["asset_id"] = "asset_new";
        song.PreviewPackageRelativePath = "preview/new.opus";

        new SongEditService().UpdateSong(package, song);

        Cell(package.Tables.Tables.Single(t => t.TableName == "song_song").Rows[0], "asset_id").Should().Be("asset_new");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_song").Rows[0], "preview").Should().Be("preview/new.opus");
    }

    [TestMethod]
    public void UpdateSong_WritesTitlesAndDescriptionsByLanguage()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"], ["1001", "preview.opus"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title", "description"], ["1001", "Old US", "Old desc"]));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp",
            ["song_id", "title", "description"], ["1001", "Old JP", "Old desc"]));

        var song = new Song { Id = "1001" };
        song.TitlesByLanguage["us"] = "New US";
        song.TitlesByLanguage["jp"] = "New JP";
        song.DescriptionsByLanguage["us"] = "New US desc";

        new SongEditService().UpdateSong(package, song);

        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[0], "title").Should().Be("New US");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_jp").Rows[0], "title").Should().Be("New JP");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[0], "description").Should().Be("New US desc");
    }

    [TestMethod]
    public void UpdateSong_WritesPatternFields()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"], ["1001", "preview.opus"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty", "level", "signature", "line"],
            ["9001", "1001", "hard", "10", "1", "2Line"]));

        var song = new Song { Id = "1001" };
        song.Patterns.Add(new SongPattern
        {
            PatternId = "9001",
            SongId = "1001"
        });
        song.Patterns[0].SourceFields["signature"] = "2";
        song.Patterns[0].SourceFields["line"] = "4Line";
        song.Patterns[0].SourceFields["difficulty"] = "expert";

        new SongEditService().UpdateSong(package, song);

        var pattern = package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[0];
        Cell(pattern, "signature").Should().Be("2");
        Cell(pattern, "line").Should().Be("4Line");
        Cell(pattern, "difficulty").Should().Be("expert");
    }

    [TestMethod]
    public void UpdateSong_ThrowsWhenSongDoesNotExist()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"], ["1001", "preview.opus"]));

        var action = () => new SongEditService().UpdateSong(package, new Song { Id = "missing" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Song 'missing' was not found.");
    }

    [TestMethod]
    public void AddSong_AppendsRequiredRowsAcrossSongTables()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview", "asset_id"], ["1001", "preview/old.opus", "asset_old"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty", "level"], ["9001", "1001", "hard", "10"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title", "description"], ["1001", "Old US", "Old desc"]));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp",
            ["song_id", "title", "description"], ["1001", "Old JP", "Old desc"]));
        package.Tables.Tables.Add(CreateTable("table/us/item_desc_us.csv", "item_desc_us", "us",
            ["item_id", "name", "description"], ["7001", "Old Item", "Old desc"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_product.csv", "product_product", "us",
            ["product_id", "song_id"], ["5001", "1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_item.csv", "product_item", "us",
            ["product_id", "item_id"], ["5001", "7001"]));
        package.Tables.Tables.Add(CreateTable("table/us/category_categoryproduct.csv", "category_categoryproduct", "us",
            ["category_id", "product_id"], ["3001", "5001"]));

        var song = new Song { Id = "1002" };
        song.SourceFields["asset_id"] = "asset_1002";
        song.PreviewPackageRelativePath = "preview/new_song.p.opus";
        song.TitlesByLanguage["us"] = "New Song";
        song.TitlesByLanguage["jp"] = "新曲";
        song.DescriptionsByLanguage["us"] = "New desc";
        song.ItemNamesByLanguage["us"] = "New Item";
        song.ProductIds.Add("5002");
        song.ItemIds.Add("7002");
        song.CategoryIds.Add("3001");
        song.Patterns.Add(new SongPattern
        {
            PatternId = "9002",
            SongId = "1002"
        });
        song.Patterns[0].SourceFields["difficulty"] = "expert";
        song.Patterns[0].SourceFields["level"] = "13";

        new SongEditService().AddSong(package, song);

        var songRows = package.Tables.Tables.Single(t => t.TableName == "song_song").Rows;
        songRows.Should().HaveCount(2);
        Cell(songRows[1], "song_id").Should().Be("1002");
        Cell(songRows[1], "preview").Should().Be("preview/new_song.p.opus");
        Cell(songRows[1], "asset_id").Should().Be("asset_1002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_us").Rows[1], "title").Should().Be("New Song");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_desc_jp").Rows[1], "title").Should().Be("新曲");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[1], "pattern_id").Should().Be("9002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[1], "level").Should().Be("13");
        Cell(package.Tables.Tables.Single(t => t.TableName == "product_product").Rows[1], "product_id").Should().Be("5002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "product_item").Rows[1], "item_id").Should().Be("7002");
        Cell(package.Tables.Tables.Single(t => t.TableName == "category_categoryproduct").Rows[1], "category_id").Should().Be("3001");
    }

    [TestMethod]
    public void AddSong_ThrowsWhenSongAlreadyExists()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"], ["1001", "preview.opus"]));

        var action = () => new SongEditService().AddSong(package, new Song { Id = "1001" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Song '1001' already exists.");
    }

    [TestMethod]
    public void AddPattern_AppendsPatternRowToAllInstances()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id"], ["1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty"],
            ["9001", "1001", "easy"]));

        var pattern = new SongPattern
        {
            PatternId = "9002",
            SongId = "1001"
        };
        pattern.SourceFields["signature"] = "3";
        pattern.SourceFields["line"] = "4Line";

        new SongEditService().AddPattern(package, "1001", pattern);

        var rows = package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows;
        rows.Should().HaveCount(2);
        Cell(rows[1], "pattern_id").Should().Be("9002");
        Cell(rows[1], "signature").Should().Be("3");
        Cell(rows[1], "line").Should().Be("4Line");
    }

    [TestMethod]
    public void AddPattern_ThrowsWhenPatternAlreadyExists()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id"], ["1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id"],
            ["9001", "1001"]));

        var action = () => new SongEditService().AddPattern(package, "1001",
            new SongPattern { PatternId = "9001", SongId = "1001" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Pattern '9001' already exists for song '1001'.");
    }

    [TestMethod]
    public void UpdatePattern_WritesFieldsToMatchingPattern()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id"], ["1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "difficulty", "line"],
            ["9001", "1001", "easy", "2Line"]));

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["difficulty"] = "expert",
            ["signature"] = "2"
        };

        new SongEditService().UpdatePattern(package, "1001", "9001", fields);

        var row = package.Tables.Tables.Single(t => t.TableName == "song_songPattern").Rows[0];
        Cell(row, "difficulty").Should().Be("expert");
        Cell(row, "signature").Should().Be("2");
        Cell(row, "line").Should().Be("2Line"); // unchanged
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
