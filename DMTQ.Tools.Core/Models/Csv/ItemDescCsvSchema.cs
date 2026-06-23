namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the item_desc_&lt;lang&gt; localized tables.
/// Mutates existing Item entities via a dictionary lookup.</summary>
public sealed class ItemDescCsvSchema : CsvLookupSchema<Item>
{
    public override string TableName => "item_desc";

    private readonly string _languageCode;

    public ItemDescCsvSchema(string languageCode)
    {
        _languageCode = languageCode;
    }

    public override string? LanguageCode => _languageCode;

    protected override void ApplyRow(
        Dictionary<string, Item> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex)
    {
        var itemId = fields.GetValueOrDefault("item_id", string.Empty);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!lookup.TryGetValue(itemId, out var item))
            return;

        var lang = _languageCode;
        if (fields.TryGetValue("name", out var name))
            item.NamesByLanguage[lang] = name;
        if (fields.TryGetValue("description", out var desc))
            item.DescriptionsByLanguage[lang] = desc;
        if (fields.TryGetValue("summary", out var summary))
            item.SummariesByLanguage[lang] = summary;
    }
}
