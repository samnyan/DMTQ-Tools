namespace DMTQ.Tools.Core.Models;

public sealed class PatchPackage
{
    public required ProjectInfo ProjectInfo { get; init; }
    public PatchManifest Manifest { get; } = new();
    public GameTableSet Tables { get; } = new();
    public List<ResourceFile> Resources { get; } = [];
    public List<PlatformPackageRecord> Platforms { get; } = [];

    /// <summary>Song entities with their patterns and localizations.
    /// Populated during import; used for editing and exported back to CSV tables.</summary>
    public List<Song> Songs { get; } = [];

    /// <summary>Achievement entities from quest_achievement + acievement_desc_&lt;lang&gt;.</summary>
    public List<Achievement> Achievements { get; } = [];

    /// <summary>Quest entities from quest_desc_&lt;lang&gt; + quest_mission_desc_&lt;lang&gt;.</summary>
    public List<Quest> Quests { get; } = [];

    /// <summary>Store product entities from product_product + category_categoryproduct.</summary>
    public List<Product> Products { get; } = [];

    /// <summary>In-game item entities from product_item + item_desc_&lt;lang&gt;.</summary>
    public List<Item> Items { get; } = [];
}
