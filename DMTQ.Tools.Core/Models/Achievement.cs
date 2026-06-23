using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

/// <summary>Achievement entity built from quest_achievement (main) + acievement_desc_&lt;lang&gt; (localized).</summary>
public sealed class Achievement
{
    [JsonInclude]
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

    [SetsRequiredMembers]
    public Achievement() { Id = ""; }

    // ── acievement_desc_&lt;lang&gt; localized fields ──
    public Dictionary<string, string> NamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PreDescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AfterDescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
