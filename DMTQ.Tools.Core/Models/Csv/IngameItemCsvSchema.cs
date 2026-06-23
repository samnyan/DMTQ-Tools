using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the ingameitem_ingameitem table.
/// The composite key (item_type + "_" + item_level) is computed in OnAfterRead.</summary>
public sealed class IngameItemCsvSchema : CsvSchema<IngameItem>
{
    public override string TableName => "ingameitem_ingameitem";

    private static readonly PropertyInfo IdProperty =
        typeof(IngameItem).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<IngameItem>> Columns { get; } =
    [
        new CsvColumn<IngameItem>("item_type",  0, ii => ii.ItemType,   (ii, v) => ii.ItemType = v),
        new CsvColumn<IngameItem>("item_level", 1, ii => ii.ItemLevel,  (ii, v) => ii.ItemLevel = v),
        new CsvColumn<IngameItem>("product_id", 2, ii => ii.ProductId,  (ii, v) => ii.ProductId = v),
        new CsvColumn<IngameItem>("update",     3, ii => ii.Update,     (ii, v) => ii.Update = v),
    ];

    protected override void OnAfterRead(IngameItem entity)
    {
        // Composite key: item_type + "_" + item_level
        var compositeId = entity.ItemType + "_" + entity.ItemLevel;
        IdProperty.SetValue(entity, compositeId);
    }
}
