using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class RelatedTableEditServiceTests
{
    [TestMethod]
    public void AchievementDraft_DeepCopiesAllLocalizedTables()
    {
        var source = new Achievement { Id = "1", Name = "base" };
        source.NamesByLanguage["CN"] = "名称";
        source.PreDescriptionsByLanguage["JP"] = "前";
        source.AfterDescriptionsByLanguage["US"] = "after";

        var draft = new AchievementEditService().CreateDraft(source);
        draft.NamesByLanguage["CN"] = "新名称";

        source.NamesByLanguage["CN"].Should().Be("名称");
        draft.PreDescriptionsByLanguage["JP"].Should().Be("前");
        draft.AfterDescriptionsByLanguage["US"].Should().Be("after");
    }

    [TestMethod]
    public void QuestDraft_DeepCopiesAndReordersLocalizedMissions()
    {
        var source = new Quest { Id = "10" };
        source.Missions.Add(new QuestMission { Index = 0, DescriptionsByLanguage = new() { ["CN"] = "一" } });
        source.Missions.Add(new QuestMission { Index = 1, DescriptionsByLanguage = new() { ["CN"] = "二" } });
        var service = new QuestEditService();
        var draft = service.CreateDraft(source);

        service.MoveMission(draft, draft.Missions[1], -1);
        draft.Missions[0].DescriptionsByLanguage["CN"] = "新的二";

        draft.Missions.Select(mission => mission.Index).Should().Equal(0, 1);
        source.Missions[1].DescriptionsByLanguage["CN"].Should().Be("二");
        draft.Missions[0].DescriptionsByLanguage["CN"].Should().Be("新的二");
    }

    [TestMethod]
    public void Editors_TargetOnlyTheSelectedPlatform()
    {
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("project", null, null, null) };
        package.GetOrCreatePlatformTables("android").Achievements.Add(new Achievement { Id = "1", Name = "Android" });
        package.GetOrCreatePlatformTables("ios").Achievements.Add(new Achievement { Id = "1", Name = "iOS" });
        var draft = new AchievementEditService().CreateDraft(package.GetPlatformTables("ios").Achievements[0]);
        draft.Name = "Edited iOS";

        new AchievementEditService().UpdateAchievement(package, draft, "ios");

        package.GetPlatformTables("android").Achievements[0].Name.Should().Be("Android");
        package.GetPlatformTables("ios").Achievements[0].Name.Should().Be("Edited iOS");
    }
}
