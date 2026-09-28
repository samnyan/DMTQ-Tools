using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>Store product (SKU) entity built from product_product table.</summary>
public sealed class Product
{
    [JsonInclude]
    public required string Id { get; init; }

    // ── product_product fields ──
    public string ItemId { get; set; } = string.Empty;
    public string PlatformProductId { get; set; } = string.Empty;
    public string StoreProductId { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string CostGamePoint { get; set; } = string.Empty;
    public string CostGameCash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SaleStartDate { get; set; } = string.Empty;
    public string SaleEndDate { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;

    // ── category_categoryproduct ──
    public List<string> CategoryIds { get; set; } = [];
    public List<CategoryProductLinkMetadata> CategoryLinkMetadata { get; set; } = [];

    /// <summary>Adds a category-product CSV row and its non-key column values.</summary>
    public void AddCategoryId(string categoryId, string displayOrder = "0", string update = "0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        while (CategoryLinkMetadata.Count < CategoryIds.Count)
            CategoryLinkMetadata.Add(new CategoryProductLinkMetadata("0", "0"));
        CategoryIds.Add(categoryId);
        CategoryLinkMetadata.Add(new CategoryProductLinkMetadata(displayOrder, update));
    }

    /// <summary>Removes every row linking this product to the specified category.</summary>
    public void RemoveCategoryIds(string categoryId)
    {
        for (var index = CategoryIds.Count - 1; index >= 0; index--)
        {
            if (!CategoryIds[index].Equals(categoryId, StringComparison.OrdinalIgnoreCase)) continue;
            CategoryIds.RemoveAt(index);
            if (index < CategoryLinkMetadata.Count)
                CategoryLinkMetadata.RemoveAt(index);
        }
    }

    [SetsRequiredMembers]
    public Product() { Id = ""; }
}

/// <summary>Non-key values stored beside one row in category_categoryproduct.csv.</summary>
public sealed record CategoryProductLinkMetadata(string DisplayOrder, string Update);
