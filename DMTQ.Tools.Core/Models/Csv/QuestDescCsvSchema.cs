using System.Reflection;

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
}
