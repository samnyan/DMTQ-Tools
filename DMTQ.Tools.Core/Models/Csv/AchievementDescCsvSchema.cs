using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the acievement_desc_&lt;lang&gt; localized tables.
/// Mutates existing Achievement entities via a dictionary lookup.</summary>
public sealed class AchievementDescCsvSchema : CsvLookupSchema<Achievement>
{
    public override string TableName => "acievement_desc";

    private readonly string _languageCode;

    public AchievementDescCsvSchema(string languageCode)
    {
        _languageCode = languageCode;
    }

    public override string? LanguageCode => _languageCode;

    protected override void ApplyRow(
        Dictionary<string, Achievement> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex)
    {
        var achievementId = fields.GetValueOrDefault("achievement_id", string.Empty);
        if (string.IsNullOrWhiteSpace(achievementId))
            return;

        if (!lookup.TryGetValue(achievementId, out var achievement))
            return;

        var lang = _languageCode;
        if (fields.TryGetValue("achievement_name", out var name))
            achievement.NamesByLanguage[lang] = name;
        if (fields.TryGetValue("pre_description", out var preDesc))
            achievement.PreDescriptionsByLanguage[lang] = preDesc;
        if (fields.TryGetValue("after_description", out var afterDesc))
            achievement.AfterDescriptionsByLanguage[lang] = afterDesc;
    }

    /// <summary>Writes the localized achievement description rows for the schema's language.</summary>
    public void WriteCsv(Stream stream, IEnumerable<Achievement> achievements)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(achievements);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        // Header
        csv.WriteField("achievement_id");
        csv.WriteField("achievement_name");
        csv.WriteField("pre_description");
        csv.WriteField("after_description");
        csv.NextRecord();

        var lang = _languageCode;
        foreach (var a in achievements)
        {
            var hasName = a.NamesByLanguage.TryGetValue(lang, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasPre = a.PreDescriptionsByLanguage.TryGetValue(lang, out var pre) && !string.IsNullOrWhiteSpace(pre);
            var hasAfter = a.AfterDescriptionsByLanguage.TryGetValue(lang, out var after) && !string.IsNullOrWhiteSpace(after);
            if (!hasName && !hasPre && !hasAfter) continue;

            csv.WriteField(a.Id);
            csv.WriteField(name ?? string.Empty);
            csv.WriteField(pre ?? string.Empty);
            csv.WriteField(after ?? string.Empty);
            csv.NextRecord();
        }

        writer.Flush();
    }
}
