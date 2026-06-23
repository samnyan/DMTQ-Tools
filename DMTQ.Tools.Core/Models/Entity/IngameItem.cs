using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>In-game power-up item entity built from ingameitem_ingameitem.
/// Key is the composite of item_type + "_" + item_level.</summary>
public sealed class IngameItem
{
    [JsonInclude]
    public required string Id { get; init; }

    // ── ingameitem_ingameitem fields ──
    public string ItemType { get; set; } = string.Empty;
    public string ItemLevel { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;

    [SetsRequiredMembers]
    public IngameItem() { Id = ""; }
}
