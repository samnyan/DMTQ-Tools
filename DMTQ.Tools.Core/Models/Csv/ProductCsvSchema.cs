using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the product_product table.</summary>
public sealed class ProductCsvSchema : CsvSchema<Product>
{
    public override string TableName => "product_product";

    private static readonly PropertyInfo IdProperty =
        typeof(Product).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<Product>> Columns { get; } =
    [
        new CsvColumn<Product>("product_id",          0,  p => p.Id,                 (p, v) => IdProperty.SetValue(p, v)),
        new CsvColumn<Product>("item_id",             1,  p => p.ItemId,             (p, v) => p.ItemId = v),
        new CsvColumn<Product>("platform_product_id", 2,  p => p.PlatformProductId,  (p, v) => p.PlatformProductId = v),
        new CsvColumn<Product>("store_product_id",    3,  p => p.StoreProductId,     (p, v) => p.StoreProductId = v),
        new CsvColumn<Product>("product_type",        4,  p => p.ProductType,        (p, v) => p.ProductType = v),
        new CsvColumn<Product>("cost_game_point",     5,  p => p.CostGamePoint,      (p, v) => p.CostGamePoint = v),
        new CsvColumn<Product>("cost_game_cash",      6,  p => p.CostGameCash,       (p, v) => p.CostGameCash = v),
        new CsvColumn<Product>("status",              7,  p => p.Status,             (p, v) => p.Status = v),
        new CsvColumn<Product>("sale_start_date",     8,  p => p.SaleStartDate,      (p, v) => p.SaleStartDate = v),
        new CsvColumn<Product>("sale_end_date",       9,  p => p.SaleEndDate,        (p, v) => p.SaleEndDate = v),
        new CsvColumn<Product>("update",              10, p => p.Update,             (p, v) => p.Update = v),
    ];
}
