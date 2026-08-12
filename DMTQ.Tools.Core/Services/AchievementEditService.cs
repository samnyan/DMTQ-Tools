using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached achievement drafts and applies CRUD operations.</summary>
public sealed class AchievementEditService
{
    public Achievement CreateDraft(Achievement source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var draft = new Achievement { Id = id ?? source.Id };
        Copy(source, draft);
        return draft;
    }

    public void AddAchievement(PatchPackage package, Achievement achievement, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(achievement);
        var achievements = package.GetPlatformTables(platform).Achievements;
        if (achievements.Any(item => item.Id.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Achievement '{achievement.Id}' already exists.");
        achievements.Add(CreateDraft(achievement));
    }

    public void UpdateAchievement(PatchPackage package, Achievement achievement, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(achievement);
        var existing = Find(package, achievement.Id, platform);
        Copy(achievement, existing);
    }

    public void RemoveAchievement(PatchPackage package, string id, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        package.GetPlatformTables(platform).Achievements.Remove(Find(package, id, platform));
    }

    private static Achievement Find(PatchPackage package, string id, string? platform)
        => package.GetPlatformTables(platform).Achievements.FirstOrDefault(item =>
               item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Achievement '{id}' was not found.");

    private static void Copy(Achievement source, Achievement target)
    {
        target.ConditionType = source.ConditionType;
        target.ConditionValue = source.ConditionValue;
        target.ConditionCount = source.ConditionCount;
        target.ConditionSpecial = source.ConditionSpecial;
        target.ImgUrl = source.ImgUrl;
        target.AchievementTier = source.AchievementTier;
        target.ObtainPoint = source.ObtainPoint;
        target.Name = source.Name;
        target.PreDescription = source.PreDescription;
        target.AfterDescription = source.AfterDescription;
        target.Update = source.Update;
        CopyDictionary(source.NamesByLanguage, target.NamesByLanguage);
        CopyDictionary(source.PreDescriptionsByLanguage, target.PreDescriptionsByLanguage);
        CopyDictionary(source.AfterDescriptionsByLanguage, target.AfterDescriptionsByLanguage);
    }

    private static void CopyDictionary(IReadOnlyDictionary<string, string> source, Dictionary<string, string> target)
    {
        target.Clear();
        foreach (var (key, value) in source)
            target[key] = value;
    }
}
