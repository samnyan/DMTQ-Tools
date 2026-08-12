using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Project;

/// <summary>All entity-backed CSV data imported for one client platform.</summary>
public sealed class PlatformTableData
{
    public List<Song> Songs { get; set; } = [];
    public List<Achievement> Achievements { get; set; } = [];
    public List<Quest> Quests { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Item> Items { get; set; } = [];
    public List<IngameItem> IngameItems { get; set; } = [];
    public List<IngameItemEffect> IngameItemEffects { get; set; } = [];
}

/// <summary>
/// A mutable view used by catalogs, editors, importers, and exporters without
/// duplicating platform-selection logic.
/// </summary>
public sealed class PlatformTableDataView
{
    internal PlatformTableDataView(PlatformTableData data)
    {
        Songs = data.Songs;
        Achievements = data.Achievements;
        Quests = data.Quests;
        Products = data.Products;
        Items = data.Items;
        IngameItems = data.IngameItems;
        IngameItemEffects = data.IngameItemEffects;
    }

    internal PlatformTableDataView(PatchPackage package)
    {
        Songs = package.Songs;
        Achievements = package.Achievements;
        Quests = package.Quests;
        Products = package.Products;
        Items = package.Items;
        IngameItems = package.IngameItems;
        IngameItemEffects = package.IngameItemEffects;
    }

    public List<Song> Songs { get; }
    public List<Achievement> Achievements { get; }
    public List<Quest> Quests { get; }
    public List<Product> Products { get; }
    public List<Item> Items { get; }
    public List<IngameItem> IngameItems { get; }
    public List<IngameItemEffect> IngameItemEffects { get; }
}
