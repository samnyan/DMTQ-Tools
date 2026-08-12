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

    /// <summary>
    /// Gets table-backed entity data keyed by client platform. New platform imports
    /// are stored here so Android and iOS CSV differences are not collapsed.
    /// </summary>
    private Dictionary<string, PlatformTableData> _platformTables =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, PlatformTableData> PlatformTables
    {
        get => _platformTables;
        set => _platformTables = value is not null
            ? new Dictionary<string, PlatformTableData>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets editable entries from the shared table/slang/slang.csv table.</summary>
    public List<SlangEntry> SlangEntries { get; } = [];

    /// <summary>Integrity errors found during import (decompressed checksums not matching manifest).</summary>
    public List<string> IntegrityErrors { get; } = [];

    /// <summary>Gets or creates the table data belonging to a client platform.</summary>
    /// <param name="platform">Client platform such as android or ios.</param>
    /// <returns>The platform-specific table data.</returns>
    public PlatformTableData GetOrCreatePlatformTables(string platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        if (!PlatformTables.TryGetValue(platform, out var tables))
        {
            tables = new PlatformTableData();
            PlatformTables[platform] = tables;
        }

        return tables;
    }

    /// <summary>Resolves table data for a platform, falling back to legacy root lists.</summary>
    /// <param name="platform">Optional platform key.</param>
    /// <returns>A view over the requested or legacy table data.</returns>
    public PlatformTableDataView GetPlatformTables(string? platform)
    {
        if (!string.IsNullOrWhiteSpace(platform)
            && PlatformTables.TryGetValue(platform, out var tables))
        {
            return new PlatformTableDataView(tables);
        }

        return new PlatformTableDataView(this);
    }
}
