using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the ingameitem_itemeffect table.</summary>
public sealed class IngameItemEffectCsvSchema : CsvSchema<IngameItemEffect>
{
    public override string TableName => "ingameitem_itemeffect";

    private static readonly PropertyInfo IdProperty =
        typeof(IngameItemEffect).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<IngameItemEffect>> Columns { get; } =
    [
        new CsvColumn<IngameItemEffect>("item_id",        0, iie => iie.Id,              (iie, v) => IdProperty.SetValue(iie, v)),
        new CsvColumn<IngameItemEffect>("effect_type",    1, iie => iie.EffectType,     (iie, v) => iie.EffectType = v),
        new CsvColumn<IngameItemEffect>("effect_point",   2, iie => iie.EffectPoint,    (iie, v) => iie.EffectPoint = v),
        new CsvColumn<IngameItemEffect>("effect_count",   3, iie => iie.EffectCount,    (iie, v) => iie.EffectCount = v),
        new CsvColumn<IngameItemEffect>("effect_special", 4, iie => iie.EffectSpecial,  (iie, v) => iie.EffectSpecial = v),
        new CsvColumn<IngameItemEffect>("update",         5, iie => iie.Update,         (iie, v) => iie.Update = v),
    ];
}
