using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>Quest entity built from quest_desc_&lt;lang&gt; (main) + quest_mission_desc_&lt;lang&gt; (missions).</summary>
public sealed class Quest
{
    [JsonInclude]
    public required string Id { get; init; }

    // ── quest_desc_&lt;lang&gt; localized fields ──
    private Dictionary<string, string> _namesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> NamesByLanguage
    {
        get => _namesByLanguage;
        set => _namesByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> _descriptionsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage
    {
        get => _descriptionsByLanguage;
        set => _descriptionsByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    // ── quest_mission_desc_&lt;lang&gt; — ordered child rows ──
    public List<QuestMission> Missions { get; set; } = [];

    [SetsRequiredMembers]
    public Quest() { Id = ""; }
}

/// <summary>A single mission row within a quest. Missions are ordered by CSV row order.</summary>
public sealed class QuestMission
{
    /// <summary>The 0-based index of this mission within its parent quest.</summary>
    [JsonInclude]
    public int Index { get; set; }

    /// <summary>Per-language mission description text.</summary>
    private Dictionary<string, string> _descriptionsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage
    {
        get => _descriptionsByLanguage;
        set => _descriptionsByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }
}
