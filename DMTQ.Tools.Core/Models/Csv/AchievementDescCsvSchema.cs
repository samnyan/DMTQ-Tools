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
}
