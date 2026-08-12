using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class RelatedTablesPageTests : BlazorUITestBase
{
    [TestMethod]
    public void AchievementPages_RenderBaseAndFiveLanguageData()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreatePackage();
        var achievement = new Achievement { Id = "1", ConditionType = "QUEST", Name = "Base" };
        achievement.NamesByLanguage["CN"] = "成就";
        package.Achievements.Add(achievement);
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var list = RenderWithProviders<Achievements>();
        var editor = Render<AchievementEditor>(parameters => parameters.Add(p => p.AchievementId, "1"));

        list.Markup.Should().Contain("成就");
        editor.Markup.Should().Contain("Achievement rules");
        foreach (var language in new[] { "CN", "JP", "KR", "TW", "US" })
            editor.Markup.Should().Contain(language);
    }

    [TestMethod]
    public void QuestEditor_RendersOrderedLocalizedMissions()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreatePackage();
        var quest = new Quest { Id = "10" };
        quest.NamesByLanguage["CN"] = "每周任务";
        quest.Missions.Add(new QuestMission { Index = 0, DescriptionsByLanguage = new() { ["CN"] = "第一步" } });
        package.Quests.Add(quest);
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var editor = Render<QuestEditor>(parameters => parameters.Add(p => p.QuestId, "10"));

        editor.Markup.Should().Contain("Localized quest descriptions");
        editor.Markup.Should().Contain("Mission 1");
        editor.Markup.Should().Contain("第一步");
    }

    [TestMethod]
    public void SlangPages_RenderCurrentPatchEntries()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreatePackage();
        package.SlangEntries.Add(new SlangEntry { Id = "entry-1", Value = "blocked phrase" });
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var list = RenderWithProviders<SlangEntries>();
        var editor = Render<SlangEditor>(parameters => parameters.Add(p => p.EntryId, "entry-1"));

        list.Markup.Should().Contain("blocked phrase");
        editor.Markup.Should().Contain("trailing empty column");
    }

    private static PatchPackage CreatePackage()
        => new() { ProjectInfo = new ProjectInfo("test-project", null, "1.003.005", null) };
}
