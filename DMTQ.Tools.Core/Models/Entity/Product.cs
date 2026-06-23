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

    [SetsRequiredMembers]
    public Product() { Id = ""; }
}
