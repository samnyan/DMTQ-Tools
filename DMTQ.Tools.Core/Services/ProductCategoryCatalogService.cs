using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Builds distinct product-category summaries from the category-product links.</summary>
public sealed class ProductCategoryCatalogService
{
    /// <summary>Builds the category IDs and their distinct associated products for one platform.</summary>
    public IReadOnlyList<ProductCategorySummary> BuildCatalog(PatchPackage package, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        var products = package.GetPlatformTables(platform).Products;
        var productIdsByCategory = products
            .SelectMany(product => product.CategoryIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => (CategoryId: id.Trim(), ProductId: product.Id)))
            .GroupBy(link => link.CategoryId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.ProductId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return package.ProductCategoryMappings.Keys
            .Concat(productIdsByCategory.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                package.ProductCategoryMappings.TryGetValue(id, out var mapping);
                productIdsByCategory.TryGetValue(id, out var associatedProducts);
                return new ProductCategorySummary(
                    id,
                    mapping?.Name ?? string.Empty,
                    mapping?.Notes,
                    associatedProducts ?? []);
            })
            .OrderBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
