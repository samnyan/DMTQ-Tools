using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Models.Csv;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Models.Csv;

[TestClass]
public sealed class LookupSchemaTests
{
    // ── AchievementDescCsvSchema ──

    [TestMethod]
    public void AchievementDescCsvSchema_PopulatesLocalizedFields()
    {
        // Arrange — pre-populate achievements
        var lookup = new Dictionary<string, Achievement>
        {
            ["ACH_001"] = new() { Id = "ACH_001" },
            ["ACH_002"] = new() { Id = "ACH_002" },
        };

        var schema = new AchievementDescCsvSchema("kr");
        var csv = "achievement_id,achievement_name,pre_description,after_description\r\n" +
                  "ACH_001,업적1,사전설명1,사후설명1\r\n" +
                  "ACH_002,업적2,사전설명2,사후설명2\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        schema.ReadCsv(stream, lookup);

        // Assert
        lookup["ACH_001"].NamesByLanguage["kr"].Should().Be("업적1");
        lookup["ACH_001"].PreDescriptionsByLanguage["kr"].Should().Be("사전설명1");
        lookup["ACH_001"].AfterDescriptionsByLanguage["kr"].Should().Be("사후설명1");
        lookup["ACH_002"].NamesByLanguage["kr"].Should().Be("업적2");
        lookup["ACH_002"].PreDescriptionsByLanguage["kr"].Should().Be("사전설명2");
        lookup["ACH_002"].AfterDescriptionsByLanguage["kr"].Should().Be("사후설명2");
    }

    [TestMethod]
    public void AchievementDescCsvSchema_IgnoresUnknownAchievementId()
    {
        var lookup = new Dictionary<string, Achievement>
        {
            ["ACH_001"] = new() { Id = "ACH_001" },
        };

        var schema = new AchievementDescCsvSchema("cn");
        var csv = "achievement_id,achievement_name,pre_description,after_description\r\n" +
                  "ACH_999,不存在,,,\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act — should not throw
        schema.ReadCsv(stream, lookup);

        // Assert — lookup unchanged for unknown id
        lookup.Should().HaveCount(1);
        lookup["ACH_001"].NamesByLanguage.Should().BeEmpty();
    }

    [TestMethod]
    public void AchievementDescCsvSchema_LanguageCode_IsSet()
    {
        var schema = new AchievementDescCsvSchema("jp");
        schema.LanguageCode.Should().Be("jp");
        schema.TableName.Should().Be("acievement_desc");
    }

    // ── QuestDescCsvSchema ──

    [TestMethod]
    public void QuestDescCsvSchema_CreatesNewQuestAndPopulatesFields()
    {
        var lookup = new Dictionary<string, Quest>();
        var schema = new QuestDescCsvSchema("cn");
        var csv = "quest_id,quest_name,description\r\n" +
                  "QUEST_001,任务1,这是第一个任务\r\n" +
                  "QUEST_002,任务2,这是第二个任务\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        schema.ReadCsv(stream, lookup);

        // Assert
        lookup.Should().HaveCount(2);
        lookup["QUEST_001"].Id.Should().Be("QUEST_001");
        lookup["QUEST_001"].NamesByLanguage["cn"].Should().Be("任务1");
        lookup["QUEST_001"].DescriptionsByLanguage["cn"].Should().Be("这是第一个任务");
        lookup["QUEST_002"].Id.Should().Be("QUEST_002");
        lookup["QUEST_002"].NamesByLanguage["cn"].Should().Be("任务2");
    }

    [TestMethod]
    public void QuestDescCsvSchema_MergesMultipleLanguages()
    {
        var lookup = new Dictionary<string, Quest>();

        // First language (CN)
        var cnSchema = new QuestDescCsvSchema("cn");
        var cnCsv = "quest_id,quest_name,description\r\nQUEST_001,任务1,描述1\r\n";
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cnCsv)))
            cnSchema.ReadCsv(stream, lookup);

        // Second language (JP)
        var jpSchema = new QuestDescCsvSchema("jp");
        var jpCsv = "quest_id,quest_name,description\r\nQUEST_001,クエスト1,説明1\r\n";
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jpCsv)))
            jpSchema.ReadCsv(stream, lookup);

        // Assert — both languages populated on same entity
        lookup.Should().HaveCount(1);
        lookup["QUEST_001"].NamesByLanguage["cn"].Should().Be("任务1");
        lookup["QUEST_001"].NamesByLanguage["jp"].Should().Be("クエスト1");
        lookup["QUEST_001"].DescriptionsByLanguage["cn"].Should().Be("描述1");
        lookup["QUEST_001"].DescriptionsByLanguage["jp"].Should().Be("説明1");
    }

    // ── QuestMissionDescCsvSchema ──

    [TestMethod]
    public void QuestMissionDescCsvSchema_AddsMissionsToQuest()
    {
        var lookup = new Dictionary<string, Quest>
        {
            ["QUEST_001"] = new() { Id = "QUEST_001" },
        };

        var schema = new QuestMissionDescCsvSchema("cn");
        var csv = "quest_mission_id,description\r\n" +
                  "QUEST_001,消灭10个敌人\r\n" +
                  "QUEST_001,收集5个道具\r\n" +
                  "QUEST_001,通关地下城\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        schema.ReadCsv(stream, lookup);

        // Assert
        var quest = lookup["QUEST_001"];
        quest.Missions.Should().HaveCount(3);
        quest.Missions[0].DescriptionsByLanguage["cn"].Should().Be("消灭10个敌人");
        quest.Missions[1].DescriptionsByLanguage["cn"].Should().Be("收集5个道具");
        quest.Missions[2].DescriptionsByLanguage["cn"].Should().Be("通关地下城");
    }

    [TestMethod]
    public void QuestMissionDescCsvSchema_MergesLanguagesByRowIndex()
    {
        var lookup = new Dictionary<string, Quest>
        {
            ["QUEST_001"] = new() { Id = "QUEST_001" },
        };

        // CN first (creates missions)
        var cnSchema = new QuestMissionDescCsvSchema("cn");
        var cnCsv = "quest_mission_id,description\r\nQUEST_001,消灭敌人\r\nQUEST_001,收集道具\r\n";
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cnCsv)))
            cnSchema.ReadCsv(stream, lookup);

        // JP second (fills descriptions by row index)
        var jpSchema = new QuestMissionDescCsvSchema("jp");
        var jpCsv = "quest_mission_id,description\r\nQUEST_001,敵を倒せ\r\nQUEST_001,アイテムを集めろ\r\n";
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jpCsv)))
            jpSchema.ReadCsv(stream, lookup);

        // Assert
        var quest = lookup["QUEST_001"];
        quest.Missions.Should().HaveCount(2);
        quest.Missions[0].DescriptionsByLanguage["cn"].Should().Be("消灭敌人");
        quest.Missions[0].DescriptionsByLanguage["jp"].Should().Be("敵を倒せ");
        quest.Missions[1].DescriptionsByLanguage["cn"].Should().Be("收集道具");
        quest.Missions[1].DescriptionsByLanguage["jp"].Should().Be("アイテムを集めろ");
    }

    // ── CategoryProductCsvSchema ──

    [TestMethod]
    public void CategoryProductCsvSchema_AddsCategoryIdsToProduct()
    {
        var lookup = new Dictionary<string, Product>
        {
            ["PROD_001"] = new() { Id = "PROD_001" },
            ["PROD_002"] = new() { Id = "PROD_002" },
        };

        var schema = new CategoryProductCsvSchema();
        var csv = "category_id,product_id\r\n" +
                  "CAT_SONG,PROD_001\r\n" +
                  "CAT_PREMIUM,PROD_001\r\n" +
                  "CAT_SONG,PROD_002\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        schema.ReadCsv(stream, lookup);

        // Assert
        lookup["PROD_001"].CategoryIds.Should().Equal("CAT_SONG", "CAT_PREMIUM");
        lookup["PROD_002"].CategoryIds.Should().Equal("CAT_SONG");
    }

    // ── ItemDescCsvSchema ──

    [TestMethod]
    public void ItemDescCsvSchema_PopulatesLocalizedFields()
    {
        var lookup = new Dictionary<string, Item>
        {
            ["ITEM_001"] = new() { Id = "ITEM_001" },
        };

        var schema = new ItemDescCsvSchema("cn");
        var csv = "item_id,name,description,summary\r\n" +
                  "ITEM_001,道具名,道具描述,道具摘要\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        schema.ReadCsv(stream, lookup);

        // Assert
        lookup["ITEM_001"].NamesByLanguage["cn"].Should().Be("道具名");
        lookup["ITEM_001"].DescriptionsByLanguage["cn"].Should().Be("道具描述");
        lookup["ITEM_001"].SummariesByLanguage["cn"].Should().Be("道具摘要");
    }

    // ── SongDescCsvSchema (regular schema, not lookup) ──

    [TestMethod]
    public void SongDescCsvSchema_ReadsLocalizationsWithLanguageCode()
    {
        var schema = new SongDescCsvSchema("kr");
        var csv = "song_id,fullname,genre,artist,composed_by,singer,feat_by,arranged_by,visualized_by\r\n" +
                  "1001,풀네임,장르,아티스트,작곡가,가수,피처링,편곡가,비주얼\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        var result = schema.ReadCsv(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].SongId.Should().Be(1001);
        result[0].FullName.Should().Be("풀네임");
        result[0].Genre.Should().Be("장르");
        result[0].ArtistName.Should().Be("아티스트");
        result[0].ComposedBy.Should().Be("작곡가");
        result[0].Singer.Should().Be("가수");
        result[0].FeatBy.Should().Be("피처링");
        result[0].ArrangedBy.Should().Be("편곡가");
        result[0].VisualizedBy.Should().Be("비주얼");
    }

    [TestMethod]
    public void SongDescCsvSchema_LanguageCode_IsSet()
    {
        var schema = new SongDescCsvSchema("jp");
        schema.LanguageCode.Should().Be("jp");
        schema.TableName.Should().Be("song_desc");
    }
}
