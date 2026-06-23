using System.Globalization;
using System.Reflection;
using CsvHelper;
using CsvHelper.Configuration;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the quest_desc_&lt;lang&gt; localized tables.
/// Creates or mutates Quest entities via a dictionary lookup.</summary>
public sealed class QuestDescCsvSchema : CsvLookupSchema<Quest>
{
    public override string TableName => "quest_desc";

    private readonly string _languageCode;
    private static readonly PropertyInfo IdProperty =
        typeof(Quest).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public QuestDescCsvSchema(string languageCode)
    {
        _languageCode = languageCode;
    }

    public override string? LanguageCode => _languageCode;

    protected override void ApplyRow(
        Dictionary<string, Quest> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex)
    {
        var questId = fields.GetValueOrDefault("quest_id", string.Empty);
        if (string.IsNullOrWhiteSpace(questId))
            return;

        if (!lookup.TryGetValue(questId, out var quest))
        {
            quest = Activator.CreateInstance<Quest>();
            IdProperty.SetValue(quest, questId);
            lookup[questId] = quest;
        }

        var lang = _languageCode;
        if (fields.TryGetValue("quest_name", out var name))
            quest.NamesByLanguage[lang] = name;
        if (fields.TryGetValue("description", out var desc))
            quest.DescriptionsByLanguage[lang] = desc;
    }

    /// <summary>Writes the localized quest description rows for the schema's language.</summary>
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
        csv.WriteField("quest_id");
        csv.WriteField("quest_name");
        csv.WriteField("description");
        csv.NextRecord();

        var lang = _languageCode;
        foreach (var q in quests)
        {
            var hasName = q.NamesByLanguage.TryGetValue(lang, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasDesc = q.DescriptionsByLanguage.TryGetValue(lang, out var desc) && !string.IsNullOrWhiteSpace(desc);
            if (!hasName && !hasDesc) continue;

            csv.WriteField(q.Id);
            csv.WriteField(name ?? string.Empty);
            csv.WriteField(desc ?? string.Empty);
            csv.NextRecord();
        }

        writer.Flush();
    }
}
