using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongCatalogServiceTests
{
    [TestMethod]
    public void BuildCatalog_MapsFlatSongFields()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_song.csv", "song_song", "us",
            ["song_id", "item_id", "name", "full_name", "genre", "artist_name", "preview"],
            ["1001", "5001", "Oblivion", "Oblivion Full", "Electronic", "ArtistX", "preview/song_1001.p.opus"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "signature", "line", "difficulty", "level"],
            ["9001", "1001", "1", "2Line", "expert", "12"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title", "description"], ["1001", "Oblivion", "US description"]));
        package.Resources.Add(new ResourceFile(
            "preview/song_1001.p.opus", "resources/shared/preview/song_1001.p.opus",
            "preview", false, null, null, ["android", "ios"]));

        var catalog = new SongCatalogService().BuildCatalog(package);

        catalog.Should().ContainSingle();
        var song = catalog[0];
        song.Id.Should().Be("1001");
        song.ItemId.Should().Be("5001");
        song.Name.Should().Be("Oblivion");
        song.FullName.Should().Be("Oblivion Full");
        song.Genre.Should().Be("Electronic");
        song.ArtistName.Should().Be("ArtistX");
        song.GetTitle("us").Should().Be("Oblivion");
        song.GetDescription("us").Should().Be("US description");
        song.Patterns.Should().ContainSingle();
        song.Patterns[0].PatternId.Should().Be("9001");
        song.Patterns[0].Signature.Should().Be("1");
        song.Patterns[0].Line.Should().Be("2Line");
        song.Patterns[0].Difficulty.Should().Be("expert");
        song.Patterns[0].Level.Should().Be("12");
        song.HasPreview.Should().BeTrue();
        song.PreviewPackageRelativePath.Should().Be("preview/song_1001.p.opus");
    }

    [TestMethod]
    public void BuildCatalog_LinksProductsItemsCategories()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "name"], ["1001", "TestSong"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_product.csv", "product_product", "us",
            ["product_id", "song_id"], ["5001", "1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_item.csv", "product_item", "us",
            ["product_id", "item_id"], ["5001", "7001"]));
        package.Tables.Tables.Add(CreateTable("table/us/category_categoryproduct.csv", "category_categoryproduct", "us",
            ["category_id", "product_id"], ["3001", "5001"]));
        package.Tables.Tables.Add(CreateTable("table/us/item_desc_us.csv", "item_desc_us", "us",
            ["item_id", "name"], ["7001", "Item Name"]));

        var catalog = new SongCatalogService().BuildCatalog(package);

        var song = catalog.Single();
        song.ProductIds.Should().Equal("5001");
        song.ItemIds.Should().Equal("7001");
        song.CategoryIds.Should().Equal("3001");
        song.GetItemName("us").Should().Be("Item Name");
    }

    [TestMethod]
    public void BuildCatalog_ReturnsEmptyWhenSongTableMissing()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title"], ["1001", "Orphan"]));
        new SongCatalogService().BuildCatalog(package).Should().BeEmpty();
    }

    [TestMethod]
    public void BuildCatalog_DeduplicatesPatternsAcrossMultipleLanguageTables()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_song.csv", "song_song", "us",
            ["song_id", "name"], ["1001", "TestSong"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/song_songPattern.csv", "song_songPattern", "us",
            ["pattern_id", "song_id", "signature", "line", "difficulty", "level"],
            ["9001", "1001", "1", "2Line", "expert", "12"]));
        package.Tables.Tables.Add(CreateTable(
            "table/cn/song_songPattern.csv", "song_songPattern", "cn",
            ["pattern_id", "song_id", "signature", "line", "difficulty", "level"],
            ["9001", "1001", "1", "2Line", "expert", "12"]));

        var catalog = new SongCatalogService().BuildCatalog(package);

        var song = catalog.Single();
        song.Patterns.Should().ContainSingle();
        song.Patterns[0].PatternId.Should().Be("9001");
    }

    [TestMethod]
    public void BuildAchievementCatalog_MapsQuestAchievementAndLocalizedDescs()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/quest_achievement.csv", "quest_achievement", "us",
            ["achievement_id", "condition_type", "condition_value", "condition_count", "condition_special", "img_url", "achievement_tier", "obtain_point", "name", "pre_description", "after_description", "update"],
            ["1", "QUEST", "10", "1", "", "a0_lv10", "1", "10", "Default DJ", "Reach Lv.10", "Done Lv.10", "0"]));
        package.Tables.Tables.Add(CreateTable(
            "table/cn/acievement_desc_cn.csv", "acievement_desc_cn", "cn",
            ["achievement_id", "achievement_name", "pre_description", "after_description"],
            ["1", "见习DJ", "达成Lv.10", "完成Lv.10"]));

        var catalog = new SongCatalogService().BuildAchievementCatalog(package);

        catalog.Should().ContainSingle();
        var a = catalog[0];
        a.Id.Should().Be("1");
        a.ConditionType.Should().Be("QUEST");
        a.Name.Should().Be("Default DJ");
        a.NamesByLanguage["cn"].Should().Be("见习DJ");
        a.PreDescriptionsByLanguage["cn"].Should().Be("达成Lv.10");
    }

    [TestMethod]
    public void BuildAchievementCatalog_ReturnsEmptyWhenNoTable()
    {
        var package = CreatePackage();
        new SongCatalogService().BuildAchievementCatalog(package).Should().BeEmpty();
    }

    [TestMethod]
    public void BuildQuestCatalog_MapsQuestsAndMissionsAcrossLanguages()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/quest_desc_us.csv", "quest_desc_us", "us",
            ["quest_id", "quest_name", "description"],
            ["1", "Weekly Quest", "Challenge weekly!"],
            ["2", "Game Basics", "Welcome!"]));
        package.Tables.Tables.Add(CreateTable(
            "table/cn/quest_desc_cn.csv", "quest_desc_cn", "cn",
            ["quest_id", "quest_name", "description"],
            ["1", "每周任务", "挑战每周更新的任务!"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/quest_mission_desc_us.csv", "quest_mission_desc_us", "us",
            ["quest_mission_id", "description"],
            ["1", "Mission A"],
            ["1", "Mission B"],
            ["2", "Mission X"]));
        package.Tables.Tables.Add(CreateTable(
            "table/cn/quest_mission_desc_cn.csv", "quest_mission_desc_cn", "cn",
            ["quest_mission_id", "description"],
            ["1", "课题 A"],
            ["1", "课题 B"],
            ["2", "课题 X"]));

        var catalog = new SongCatalogService().BuildQuestCatalog(package);

        catalog.Should().HaveCount(2);
        var q1 = catalog.Single(q => q.Id == "1");
        q1.NamesByLanguage["us"].Should().Be("Weekly Quest");
        q1.NamesByLanguage["cn"].Should().Be("每周任务");
        q1.Missions.Should().HaveCount(2);
        q1.Missions[0].DescriptionsByLanguage["us"].Should().Be("Mission A");
        q1.Missions[0].DescriptionsByLanguage["cn"].Should().Be("课题 A");
        q1.Missions[1].DescriptionsByLanguage["us"].Should().Be("Mission B");
        q1.Missions[1].DescriptionsByLanguage["cn"].Should().Be("课题 B");

        var q2 = catalog.Single(q => q.Id == "2");
        q2.Missions.Should().HaveCount(1);
        q2.Missions[0].DescriptionsByLanguage["us"].Should().Be("Mission X");
    }

    [TestMethod]
    public void BuildQuestCatalog_ReturnsEmptyWhenNoTables()
    {
        var package = CreatePackage();
        new SongCatalogService().BuildQuestCatalog(package).Should().BeEmpty();
    }

    [TestMethod]
    public void BuildProductCatalog_MapsProductsAndCategories()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/product_product.csv", "product_product", "us",
            ["product_id", "item_id", "platform_product_id", "product_type", "cost_game_cash", "status"],
            ["1", "8", "781", "I", "20", "N"],
            ["2", "9", "782", "I", "25", "N"]));
        package.Tables.Tables.Add(CreateTable(
            "table/us/category_categoryproduct.csv", "category_categoryproduct", "us",
            ["category_id", "product_id", "display_order"],
            ["1", "1", "1"],
            ["2", "1", "0"],
            ["1", "2", "1"]));

        var catalog = new SongCatalogService().BuildProductCatalog(package);

        catalog.Should().HaveCount(2);
        var p1 = catalog.Single(p => p.Id == "1");
        p1.ItemId.Should().Be("8");
        p1.PlatformProductId.Should().Be("781");
        p1.CostGameCash.Should().Be("20");
        p1.CategoryIds.Should().Equal("1", "2");

        var p2 = catalog.Single(p => p.Id == "2");
        p2.CategoryIds.Should().Equal("1");
    }

    [TestMethod]
    public void BuildItemCatalog_MapsItemsAndLocalizedDescs()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable(
            "table/us/product_item.csv", "product_item", "us",
            ["item_id", "item_name", "item_type", "buy_limit_type", "summary"],
            ["1", "oblivion", "S", "F", "기본곡"],
            ["2", "raisemeup", "S", "F", "기본곡"]));
        package.Tables.Tables.Add(CreateTable(
            "table/cn/item_desc_cn.csv", "item_desc_cn", "cn",
            ["item_id", "name", "description", "summary"],
            ["1", "OBLIVION", "Dramatic Trance", "韩国原曲"]));

        var catalog = new SongCatalogService().BuildItemCatalog(package);

        catalog.Should().HaveCount(2);
        var i1 = catalog.Single(i => i.Id == "1");
        i1.ItemName.Should().Be("oblivion");
        i1.ItemType.Should().Be("S");
        i1.Summary.Should().Be("기본곡");
        i1.NamesByLanguage["cn"].Should().Be("OBLIVION");
        i1.DescriptionsByLanguage["cn"].Should().Be("Dramatic Trance");
    }

    private static PatchPackage CreatePackage()
        => new() { ProjectInfo = new ProjectInfo("project", null, "1.003.005", null) };

    private static GameTable CreateTable(string path, string tableName, string languageCode, string[] columns, params string[][] rows)
    {
        var table = new GameTable { PackageRelativePath = path, TableName = tableName, LanguageCode = languageCode };
        for (var i = 0; i < columns.Length; i++) table.Columns.Add(new GameTableColumn(columns[i], i));
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = new GameTableRow { Order = rowIndex };
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                row.Cells.Add(new GameTableCell(columns[columnIndex], rows[rowIndex][columnIndex]));
            table.Rows.Add(row);
        }
        return table;
    }
}
