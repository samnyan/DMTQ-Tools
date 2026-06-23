using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>In-game item entity built from product_item + item_desc_&lt;lang&gt;.</summary>
public sealed class Item
{
    [JsonInclude]
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
    private Dictionary<string, string> _namesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> NamesByLanguage
    {
        get => _namesByLanguage;
        set => _namesByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> _descriptionsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage
    {
        get => _descriptionsByLanguage;
        set => _descriptionsByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> _summariesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SummariesByLanguage
    {
        get => _summariesByLanguage;
        set => _summariesByLanguage = value is not null
            ? new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    [SetsRequiredMembers]
    public Item() { Id = ""; }
}
