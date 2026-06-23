using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;

namespace DMTQ.Tools.Core.Models.Project;

public sealed class PatchPackage
{
    public required ProjectInfo ProjectInfo { get; init; }
    public GameTableSet Tables { get; } = new();
    public List<ResourceFile> Resources { get; } = [];

    /// <summary>Song entities with their patterns and localizations.</summary>
    public List<Song> Songs { get; } = [];

    /// <summary>Achievement entities from quest_achievement + acievement_desc_&lt;lang&gt;.</summary>
    public List<Achievement> Achievements { get; } = [];

    /// <summary>Quest entities from quest_desc_&lt;lang&gt; + quest_mission_desc_&lt;lang&gt;.</summary>
    public List<Quest> Quests { get; } = [];

    /// <summary>Store product entities from product_product + category_categoryproduct.</summary>
    public List<Product> Products { get; } = [];

    /// <summary>In-game item entities from product_item + item_desc_&lt;lang&gt;.</summary>
    public List<Item> Items { get; } = [];

    /// <summary>Power-up ingame item entities from ingameitem_ingameitem.</summary>
    public List<IngameItem> IngameItems { get; } = [];

    /// <summary>Power-up item effect entities from ingameitem_itemeffect.</summary>
    public List<IngameItemEffect> IngameItemEffects { get; } = [];

    /// <summary>Integrity errors found during import (decompressed checksums not matching manifest).</summary>
    public List<string> IntegrityErrors { get; } = [];
}
