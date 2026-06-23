using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

/// <summary>Quest entity built from quest_desc_&lt;lang&gt; (main) + quest_mission_desc_&lt;lang&gt; (missions).</summary>
public sealed class Quest
{
    [JsonInclude]
    public required string Id { get; init; }

    // ── quest_desc_&lt;lang&gt; localized fields ──
    public Dictionary<string, string> NamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── quest_mission_desc_&lt;lang&gt; — ordered child rows ──
    public List<QuestMission> Missions { get; set; } = [];

    [SetsRequiredMembers]
    public Quest() { Id = ""; }
}

/// <summary>A single mission row within a quest. Missions are ordered by CSV row order.</summary>
public sealed class QuestMission
{
    /// <summary>Per-language mission description text.</summary>
    public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
