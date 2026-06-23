namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the quest_mission_desc_&lt;lang&gt; localized tables.
/// Adds missions (ordered by CSV row order) to existing Quest entities via a dictionary lookup.</summary>
public sealed class QuestMissionDescCsvSchema : CsvLookupSchema<Quest>
{
    public override string TableName => "quest_mission_desc";

    private readonly string _languageCode;

    public QuestMissionDescCsvSchema(string languageCode)
    {
        _languageCode = languageCode;
    }

    public override string? LanguageCode => _languageCode;

    protected override void ApplyRow(
        Dictionary<string, Quest> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex)
    {
        var questId = fields.GetValueOrDefault("quest_mission_id", string.Empty);
        if (string.IsNullOrWhiteSpace(questId))
            return;

        if (!lookup.TryGetValue(questId, out var quest))
            return;

        // Missions are ordered by CSV row order. Ensure we have a mission at this index.
        while (quest.Missions.Count <= rowIndex)
            quest.Missions.Add(new QuestMission());

        var mission = quest.Missions[rowIndex];
        var lang = _languageCode;
        if (fields.TryGetValue("description", out var desc))
            mission.DescriptionsByLanguage[lang] = desc;
    }
}
