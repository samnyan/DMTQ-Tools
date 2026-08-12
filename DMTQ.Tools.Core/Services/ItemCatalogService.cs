using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Builds the editable Item catalog and resolves localized display values.</summary>
public sealed class ItemCatalogService
{
    public IReadOnlyList<Item> BuildCatalog(PatchPackage package, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        return package.GetPlatformTables(platform).Items
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetDisplayName(Item item, string? language)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!string.IsNullOrWhiteSpace(language)
            && item.NamesByLanguage.TryGetValue(language, out var localizedName)
            && !string.IsNullOrWhiteSpace(localizedName))
        {
            return localizedName;
        }

        return string.IsNullOrWhiteSpace(item.ItemName) ? item.Id : item.ItemName;
    }

    public string GetLocalizedSummary(Item item, string? language)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!string.IsNullOrWhiteSpace(language)
            && item.SummariesByLanguage.TryGetValue(language, out var localizedSummary)
            && !string.IsNullOrWhiteSpace(localizedSummary))
        {
            return localizedSummary;
        }

        return item.Summary;
    }
}
