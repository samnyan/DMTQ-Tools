using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using DMTQ.Tools.Core.Models.Entity;

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

    /// <summary>Writes the localized item description rows for the schema's language.</summary>
    public void WriteCsv(Stream stream, IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(items);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        // Header
        csv.WriteField("item_id");
        csv.WriteField("name");
        csv.WriteField("description");
        csv.WriteField("summary");
        csv.NextRecord();

        var lang = _languageCode;
        foreach (var item in items)
        {
            var hasName = item.NamesByLanguage.TryGetValue(lang, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasDesc = item.DescriptionsByLanguage.TryGetValue(lang, out var desc) && !string.IsNullOrWhiteSpace(desc);
            var hasSummary = item.SummariesByLanguage.TryGetValue(lang, out var summary) && !string.IsNullOrWhiteSpace(summary);
            if (!hasName && !hasDesc && !hasSummary) continue;

            csv.WriteField(item.Id);
            csv.WriteField(name ?? string.Empty);
            csv.WriteField(desc ?? string.Empty);
            csv.WriteField(summary ?? string.Empty);
            csv.NextRecord();
        }

        writer.Flush();
    }
}
