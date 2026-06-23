using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the quest_achievement table.</summary>
public sealed class AchievementCsvSchema : CsvSchema<Achievement>
{
    public override string TableName => "quest_achievement";

    private static readonly PropertyInfo IdProperty =
        typeof(Achievement).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<Achievement>> Columns { get; } =
    [
        new CsvColumn<Achievement>("achievement_id",     0,  a => a.Id,                (a, v) => IdProperty.SetValue(a, v)),
        new CsvColumn<Achievement>("condition_type",     1,  a => a.ConditionType,     (a, v) => a.ConditionType = v),
        new CsvColumn<Achievement>("condition_value",    2,  a => a.ConditionValue,    (a, v) => a.ConditionValue = v),
        new CsvColumn<Achievement>("condition_count",    3,  a => a.ConditionCount,    (a, v) => a.ConditionCount = v),
        new CsvColumn<Achievement>("condition_special",  4,  a => a.ConditionSpecial,  (a, v) => a.ConditionSpecial = v),
        new CsvColumn<Achievement>("img_url",            5,  a => a.ImgUrl,            (a, v) => a.ImgUrl = v),
        new CsvColumn<Achievement>("achievement_tier",   6,  a => a.AchievementTier,   (a, v) => a.AchievementTier = v),
        new CsvColumn<Achievement>("obtain_point",       7,  a => a.ObtainPoint,       (a, v) => a.ObtainPoint = v),
        new CsvColumn<Achievement>("name",               8,  a => a.Name,              (a, v) => a.Name = v),
        new CsvColumn<Achievement>("pre_description",    9,  a => a.PreDescription,    (a, v) => a.PreDescription = v),
        new CsvColumn<Achievement>("after_description",  10, a => a.AfterDescription,  (a, v) => a.AfterDescription = v),
        new CsvColumn<Achievement>("update",             11, a => a.Update,            (a, v) => a.Update = v),
    ];
}
