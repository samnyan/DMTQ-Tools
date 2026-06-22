using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongCatalogServiceTests
{
    [TestMethod]
    public void BuildCatalog_CombinesSongDescriptionsAndPatterns()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_song.csv",
            "song_song",
            "us",
            ["song_id", "asset_id", "preview"],
            ["1001", "asset_1001", "preview/song_1001.p.opus"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_songPattern.csv",
            "song_songPattern",
            "us",
            ["pattern_id", "song_id", "difficulty", "level"],
            ["9001", "1001", "expert", "12"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_desc_us.csv",
            "song_desc_us",
            "us",
            ["song_id", "title", "description"],
            ["1001", "Oblivion", "US description"]));
        package.Tables.Tables.Add(CreateTable(
            "table/jp/song_desc_jp.csv",
            "song_desc_jp",
            "jp",
            ["song_id", "title", "description"],
            ["1001", "オブリビオン", "JP description"]));
        package.Resources.Add(new ResourceFile(
            "preview/song_1001.p.opus",
            "resources/shared/preview/song_1001.p.opus",
            "preview",
            false,
            null,
            null,
            ["android", "ios"]));

        var service = new SongCatalogService();

        var catalog = service.BuildCatalog(package);

        catalog.Should().ContainSingle();
        var song = catalog[0];
        song.SongId.Should().Be("1001");
        song.GetTitle("us").Should().Be("Oblivion");
        song.GetTitle("jp").Should().Be("オブリビオン");
        song.GetDescription("jp").Should().Be("JP description");
        song.Patterns.Should().ContainSingle();
        song.Patterns[0].PatternId.Should().Be("9001");
        song.Patterns[0].Difficulty.Should().Be("expert");
        song.Patterns[0].Level.Should().Be("12");
        song.HasPreview.Should().BeTrue();
        song.PreviewPackageRelativePath.Should().Be("preview/song_1001.p.opus");
    }

    [TestMethod]
    public void BuildCatalog_LinksProductsItemsCategoriesAndItemDescriptions()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_song.csv",
            "song_song",
            "us",
            ["song_id", "name"],
            ["1001", "internal_name"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/product_product.csv",
            "product_product",
            "us",
            ["product_id", "song_id"],
            ["5001", "1001"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/product_item.csv",
            "product_item",
            "us",
            ["product_id", "item_id"],
            ["5001", "7001"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/category_categoryproduct.csv",
            "category_categoryproduct",
            "us",
            ["category_id", "product_id"],
            ["3001", "5001"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/item_desc_us.csv",
            "item_desc_us",
            "us",
            ["item_id", "name", "description"],
            ["7001", "Item Name", "Item description"]));

        var service = new SongCatalogService();

        var catalog = service.BuildCatalog(package);

        var song = catalog.Single();
        song.ProductIds.Should().Equal("5001");
        song.ItemIds.Should().Equal("7001");
        song.CategoryIds.Should().Equal("3001");
        song.GetItemName("us").Should().Be("Item Name");
    }

    [TestMethod]
    public void BuildCatalog_ReturnsEmptyCatalogWhenSongTableIsMissing()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_desc_us.csv",
            "song_desc_us",
            "us",
            ["song_id", "title"],
            ["1001", "Orphan title"]));

        var service = new SongCatalogService();

        service.BuildCatalog(package).Should().BeEmpty();
    }

    private static PatchPackage CreatePackage()
        => new()
        {
            ProjectInfo = new ProjectInfo("project", null, "1.003.005", null)
        };

    private static GameTable CreateTable(
        string path,
        string tableName,
        string languageCode,
        string[] columns,
        params string[][] rows)
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
}
