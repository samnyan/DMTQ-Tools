namespace DMTQ.Tools.Core.Models;

/// <summary>Achievement entity built from quest_achievement (main) + acievement_desc_&lt;lang&gt; (localized).</summary>
public sealed class Achievement
{
    public required string Id { get; init; }

    // ── quest_achievement fields ──
    public string ConditionType { get; set; } = string.Empty;
    public string ConditionValue { get; set; } = string.Empty;
    public string ConditionCount { get; set; } = string.Empty;
    public string ConditionSpecial { get; set; } = string.Empty;
    public string ImgUrl { get; set; } = string.Empty;
    public string AchievementTier { get; set; } = string.Empty;
    public string ObtainPoint { get; set; } = string.Empty;

    /// <summary>Default name (from quest_achievement.name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Default pre-description (from quest_achievement.pre_description).</summary>
    public string PreDescription { get; set; } = string.Empty;

    /// <summary>Default after-description (from quest_achievement.after_description).</summary>
    public string AfterDescription { get; set; } = string.Empty;

    public string Update { get; set; } = string.Empty;

    // ── acievement_desc_&lt;lang&gt; localized fields ──
    public Dictionary<string, string> NamesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PreDescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AfterDescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
}
