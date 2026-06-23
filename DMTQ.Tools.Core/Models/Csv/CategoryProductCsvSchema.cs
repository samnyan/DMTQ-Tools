namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the category_categoryproduct join table.
/// Adds category IDs to existing Product entities via a dictionary lookup.</summary>
public sealed class CategoryProductCsvSchema : CsvLookupSchema<Product>
{
    public override string TableName => "category_categoryproduct";

    protected override void ApplyRow(
        Dictionary<string, Product> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex)
    {
        var productId = fields.GetValueOrDefault("product_id", string.Empty);
        if (string.IsNullOrWhiteSpace(productId))
            return;

        if (!lookup.TryGetValue(productId, out var product))
            return;

        if (fields.TryGetValue("category_id", out var categoryId) && !string.IsNullOrWhiteSpace(categoryId))
            product.CategoryIds.Add(categoryId);
    }
}
