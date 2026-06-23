using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class SongCatalogService
{
    public IReadOnlyList<Song> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return package.Songs
            .OrderBy(song => song.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song.Id)
            .ToArray();
    }

    /// <summary>Returns true when the table carries data that is owned by Song entities
    /// and should be removed from raw GameTable storage after entity extraction.</summary>
    public static bool IsSongRelatedTable(string tableName)
        => tableName.Equals("song_song", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("song_songPattern", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("song_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Product data
    /// (product_product + category_categoryproduct).</summary>
    public static bool IsProductRelatedTable(string tableName)
        => tableName.Equals("product_product", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("category_categoryproduct", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Item data
    /// (product_item + item_desc_&lt;lang&gt;).</summary>
    public static bool IsItemRelatedTable(string tableName)
        => tableName.Equals("product_item", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("item_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries IngameItem data
    /// (ingameitem_ingameitem + ingameitem_itemeffect).</summary>
    public static bool IsIngameItemRelatedTable(string tableName)
        => tableName.Equals("ingameitem_ingameitem", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("ingameitem_itemeffect", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Achievement data
    /// (quest_achievement + acievement_desc_&lt;lang&gt;).</summary>
    public static bool IsAchievementRelatedTable(string tableName)
        => tableName.Equals("quest_achievement", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("acievement_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Quest data
    /// (quest_desc_&lt;lang&gt; + quest_mission_desc_&lt;lang&gt;).</summary>
    public static bool IsQuestRelatedTable(string tableName)
        => tableName.StartsWith("quest_desc_", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("quest_mission_desc_", StringComparison.OrdinalIgnoreCase);
}
