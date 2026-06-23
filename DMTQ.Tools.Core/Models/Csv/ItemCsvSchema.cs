using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the product_item table.</summary>
public sealed class ItemCsvSchema : CsvSchema<Item>
{
    public override string TableName => "product_item";

    private static readonly PropertyInfo IdProperty =
        typeof(Item).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<Item>> Columns { get; } =
    [
        new CsvColumn<Item>("item_id",         0,  i => i.Id,              (i, v) => IdProperty.SetValue(i, v)),
        new CsvColumn<Item>("item_name",       1,  i => i.ItemName,        (i, v) => i.ItemName = v),
        new CsvColumn<Item>("img_url_1",       2,  i => i.ImgUrl1,         (i, v) => i.ImgUrl1 = v),
        new CsvColumn<Item>("img_url_2",       3,  i => i.ImgUrl2,         (i, v) => i.ImgUrl2 = v),
        new CsvColumn<Item>("description",     4,  i => i.Description,     (i, v) => i.Description = v),
        new CsvColumn<Item>("repeat_count",    5,  i => i.RepeatCount,     (i, v) => i.RepeatCount = v),
        new CsvColumn<Item>("item_type",       6,  i => i.ItemType,        (i, v) => i.ItemType = v),
        new CsvColumn<Item>("limit_minute",    7,  i => i.LimitMinute,     (i, v) => i.LimitMinute = v),
        new CsvColumn<Item>("status",          8,  i => i.Status,          (i, v) => i.Status = v),
        new CsvColumn<Item>("buy_level",       9,  i => i.BuyLevel,        (i, v) => i.BuyLevel = v),
        new CsvColumn<Item>("buy_limit_count", 10, i => i.BuyLimitCount,   (i, v) => i.BuyLimitCount = v),
        new CsvColumn<Item>("buy_limit_type",  11, i => i.BuyLimitType,    (i, v) => i.BuyLimitType = v),
        new CsvColumn<Item>("summary",         12, i => i.Summary,         (i, v) => i.Summary = v),
        new CsvColumn<Item>("update",          13, i => i.Update,          (i, v) => i.Update = v),
    ];
}
