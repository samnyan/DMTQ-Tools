namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>A local, non-exported description for a server-owned product category ID.</summary>
public sealed record ProductCategoryMapping(string Name, string? Notes = null);

/// <summary>Creates the built-in category descriptions inferred from product associations.</summary>
public static class ProductCategoryDefaults
{
    /// <summary>Creates a fresh case-insensitive mapping table for a new or legacy project.</summary>
    public static Dictionary<string, ProductCategoryMapping> Create() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new("礼包 / Starter packs"),
            ["101"] = new("礼包 / Starter packs", "关联商品内容与类别 1 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["2"] = new("Q Music Pack 曲包"),
            ["102"] = new("Q Music Pack 曲包", "关联商品内容与类别 2 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["3"] = new("单曲购买 / Single song purchases"),
            ["103"] = new("单曲购买 / Single song purchases", "关联商品内容与类别 3 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["4"] = new("Partner / 角色与特殊道具"),
            ["104"] = new("Partner / 角色与特殊道具", "关联商品内容与类别 4 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["5"] = new("Skin / Note Skin"),
            ["105"] = new("Skin / Note Skin", "关联商品内容与类别 5 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["6"] = new("MAX / Q Point 货币包"),
            ["106"] = new("MAX / Q Point 货币包", "关联商品内容与类别 6 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["21"] = new("等级解锁 / Level unlocks"),
            ["121"] = new("等级解锁 / Level unlocks", "关联商品内容与类别 21 对应，使用另一套商品 ID；原因未能从 CSV 确认。"),
            ["1004"] = new("Anti-Break 道具等级"),
            ["1005"] = new("Fever Plus 道具等级"),
            ["1006"] = new("Gauge Refill 道具等级"),
            ["10001"] = new("隐藏/赠送/非独立售卖过滤组 / hidden, gifted, non-store filter group", "Shop2SceneConfig 将此类别关联的商品加入 ignoreList 并从指定商店栏位排除；不是普通商店类别。")
        };
}
