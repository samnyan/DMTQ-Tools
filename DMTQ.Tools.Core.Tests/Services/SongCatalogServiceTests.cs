using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongCatalogServiceTests
{
    [TestMethod]
    public void BuildCatalog_ReturnsSongsSortedByTitleThenId()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, null, null)
        };
        var song1 = new Song { Id = 2, Name = "Beta" };
        var song2 = new Song { Id = 1, Name = "Alpha" };
        var song3 = new Song { Id = 3, Name = "Alpha" };
        package.Songs.Add(song1);
        package.Songs.Add(song2);
        package.Songs.Add(song3);

        var catalog = new SongCatalogService().BuildCatalog(package);

        catalog.Should().HaveCount(3);
        catalog[0].Id.Should().Be(1); // Alpha, id=1
        catalog[1].Id.Should().Be(3); // Alpha, id=3
        catalog[2].Id.Should().Be(2); // Beta
    }

    [TestMethod]
    public void BuildCatalog_ReturnsEmptyWhenNoSongs()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, null, null)
        };

        var catalog = new SongCatalogService().BuildCatalog(package);

        catalog.Should().BeEmpty();
    }

    [TestMethod]
    public void IsSongRelatedTable_MatchesSongTables()
    {
        SongCatalogService.IsSongRelatedTable("song_song").Should().BeTrue();
        SongCatalogService.IsSongRelatedTable("song_songPattern").Should().BeTrue();
        SongCatalogService.IsSongRelatedTable("song_desc_us").Should().BeTrue();
        SongCatalogService.IsSongRelatedTable("song_desc_jp").Should().BeTrue();
        SongCatalogService.IsSongRelatedTable("product_product").Should().BeFalse();
    }

    [TestMethod]
    public void IsAchievementRelatedTable_MatchesAchievementTables()
    {
        SongCatalogService.IsAchievementRelatedTable("quest_achievement").Should().BeTrue();
        SongCatalogService.IsAchievementRelatedTable("acievement_desc_us").Should().BeTrue();
        SongCatalogService.IsAchievementRelatedTable("song_song").Should().BeFalse();
    }

    [TestMethod]
    public void IsQuestRelatedTable_MatchesQuestTables()
    {
        SongCatalogService.IsQuestRelatedTable("quest_desc_us").Should().BeTrue();
        SongCatalogService.IsQuestRelatedTable("quest_mission_desc_us").Should().BeTrue();
        SongCatalogService.IsQuestRelatedTable("quest_achievement").Should().BeFalse();
    }

    [TestMethod]
    public void IsProductRelatedTable_MatchesProductTables()
    {
        SongCatalogService.IsProductRelatedTable("product_product").Should().BeTrue();
        SongCatalogService.IsProductRelatedTable("category_categoryproduct").Should().BeTrue();
        SongCatalogService.IsProductRelatedTable("product_item").Should().BeFalse();
    }

    [TestMethod]
    public void IsItemRelatedTable_MatchesItemTables()
    {
        SongCatalogService.IsItemRelatedTable("product_item").Should().BeTrue();
        SongCatalogService.IsItemRelatedTable("item_desc_us").Should().BeTrue();
        SongCatalogService.IsItemRelatedTable("ingameitem_ingameitem").Should().BeFalse();
    }

    [TestMethod]
    public void IsIngameItemRelatedTable_MatchesIngameItemTables()
    {
        SongCatalogService.IsIngameItemRelatedTable("ingameitem_ingameitem").Should().BeTrue();
        SongCatalogService.IsIngameItemRelatedTable("ingameitem_itemeffect").Should().BeTrue();
        SongCatalogService.IsIngameItemRelatedTable("product_item").Should().BeFalse();
    }
}
