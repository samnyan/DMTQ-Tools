using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the quest_mission_desc_&lt;lang&gt; localized tables.
/// Adds missions (ordered by CSV row order) to existing Quest entities via a dictionary lookup.</summary>
public sealed class QuestMissionDescCsvSchema : CsvLookupSchema<Quest>
{
    public override string TableName => "quest_mission_desc";

    private readonly string _languageCode;
    private readonly Dictionary<string, int> _missionIndex = new(StringComparer.OrdinalIgnoreCase);

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

        // Per-quest index — each quest's missions are indexed independently.
        // The first language processed creates the mission shells; subsequent
        // languages populate descriptions into the same index.
        if (!_missionIndex.TryGetValue(questId, out var index))
            index = 0;

        QuestMission mission;
        if (index < quest.Missions.Count)
        {
            mission = quest.Missions[index];
        }
        else
        {
            mission = new QuestMission { Index = index };
            quest.Missions.Add(mission);
        }

        var lang = _languageCode;
        if (fields.TryGetValue("description", out var desc))
            mission.DescriptionsByLanguage[lang] = desc;

        _missionIndex[questId] = index + 1;
    }

    /// <summary>Writes the localized quest mission description rows for the schema's language.</summary>
    public void WriteCsv(Stream stream, IEnumerable<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(quests);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        // Header
        csv.WriteField("quest_mission_id");
        csv.WriteField("description");
        csv.NextRecord();

        var lang = _languageCode;
        foreach (var q in quests)
        {
            foreach (var mission in q.Missions)
            {
                if (!mission.DescriptionsByLanguage.TryGetValue(lang, out var desc) || string.IsNullOrWhiteSpace(desc))
                    continue;

                csv.WriteField(q.Id);
                csv.WriteField(desc);
                csv.NextRecord();
            }
        }

        writer.Flush();
    }
}
