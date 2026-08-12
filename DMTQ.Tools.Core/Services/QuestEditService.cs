using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached quest drafts, including ordered localized missions.</summary>
public sealed class QuestEditService
{
    public Quest CreateDraft(Quest source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var draft = new Quest { Id = id ?? source.Id };
        Copy(source, draft);
        return draft;
    }

    public void AddQuest(PatchPackage package, Quest quest, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(quest);
        var quests = package.GetPlatformTables(platform).Quests;
        if (quests.Any(item => item.Id.Equals(quest.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Quest '{quest.Id}' already exists.");
        quests.Add(CreateDraft(quest));
    }

    public void UpdateQuest(PatchPackage package, Quest quest, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(quest);
        Copy(quest, Find(package, quest.Id, platform));
    }

    public void RemoveQuest(PatchPackage package, string id, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        package.GetPlatformTables(platform).Quests.Remove(Find(package, id, platform));
    }

    public void AddMission(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        quest.Missions.Add(new QuestMission { Index = quest.Missions.Count });
    }

    public void RemoveMission(Quest quest, QuestMission mission)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(mission);
        quest.Missions.Remove(mission);
        Reindex(quest);
    }

    public void MoveMission(Quest quest, QuestMission mission, int offset)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(mission);
        var oldIndex = quest.Missions.IndexOf(mission);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= quest.Missions.Count)
            return;
        quest.Missions.RemoveAt(oldIndex);
        quest.Missions.Insert(newIndex, mission);
        Reindex(quest);
    }

    private static Quest Find(PatchPackage package, string id, string? platform)
        => package.GetPlatformTables(platform).Quests.FirstOrDefault(item =>
               item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Quest '{id}' was not found.");

    private static void Copy(Quest source, Quest target)
    {
        CopyDictionary(source.NamesByLanguage, target.NamesByLanguage);
        CopyDictionary(source.DescriptionsByLanguage, target.DescriptionsByLanguage);
        target.Missions.Clear();
        foreach (var sourceMission in source.Missions)
        {
            var mission = new QuestMission { Index = target.Missions.Count };
            CopyDictionary(sourceMission.DescriptionsByLanguage, mission.DescriptionsByLanguage);
            target.Missions.Add(mission);
        }
    }

    private static void CopyDictionary(IReadOnlyDictionary<string, string> source, Dictionary<string, string> target)
    {
        target.Clear();
        foreach (var (key, value) in source)
            target[key] = value;
    }

    private static void Reindex(Quest quest)
    {
        for (var index = 0; index < quest.Missions.Count; index++)
            quest.Missions[index].Index = index;
    }
}
