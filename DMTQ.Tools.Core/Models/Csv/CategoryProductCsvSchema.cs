using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using DMTQ.Tools.Core.Models.Entity;

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

    /// <summary>Writes the category-product join rows from all products.</summary>
    public void WriteCsv(Stream stream, IEnumerable<Product> products)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(products);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        // Header
        csv.WriteField("category_id");
        csv.WriteField("product_id");
        csv.NextRecord();

        foreach (var product in products)
        {
            foreach (var categoryId in product.CategoryIds)
            {
                csv.WriteField(categoryId);
                csv.WriteField(product.Id);
                csv.NextRecord();
            }
        }

        writer.Flush();
    }
}
