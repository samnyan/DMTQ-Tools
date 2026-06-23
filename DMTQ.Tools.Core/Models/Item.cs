namespace DMTQ.Tools.Core.Models;

/// <summary>In-game item entity built from product_item + item_desc_&lt;lang&gt;.</summary>
public sealed class Item
{
    public required string Id { get; init; }

    // ── product_item fields ──
    public string ItemName { get; set; } = string.Empty;
    public string ImgUrl1 { get; set; } = string.Empty;
    public string ImgUrl2 { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RepeatCount { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string LimitMinute { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BuyLevel { get; set; } = string.Empty;
    public string BuyLimitCount { get; set; } = string.Empty;
    public string BuyLimitType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;

    // ── item_desc_&lt;lang&gt; localized fields ──
    public Dictionary<string, string> NamesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SummariesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
}
