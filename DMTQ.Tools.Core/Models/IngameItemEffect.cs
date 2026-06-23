namespace DMTQ.Tools.Core.Models;

/// <summary>In-game item effect entity built from ingameitem_itemeffect.</summary>
public sealed class IngameItemEffect
{
    public required string Id { get; init; }

    // ── ingameitem_itemeffect fields ──
    public string EffectType { get; set; } = string.Empty;
    public string EffectPoint { get; set; } = string.Empty;
    public string EffectCount { get; set; } = string.Empty;
    public string EffectSpecial { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;
}
