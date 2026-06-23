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
