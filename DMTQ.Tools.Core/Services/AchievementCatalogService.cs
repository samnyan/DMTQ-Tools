using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Builds the editable achievement catalog for a client platform.</summary>
public sealed class AchievementCatalogService
{
    public IReadOnlyList<Achievement> BuildCatalog(PatchPackage package, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.GetPlatformTables(platform).Achievements
            .OrderBy(achievement => achievement.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetDisplayName(Achievement achievement, string language)
    {
        ArgumentNullException.ThrowIfNull(achievement);
        return achievement.NamesByLanguage.TryGetValue(language, out var name)
               && !string.IsNullOrWhiteSpace(name)
            ? name
            : string.IsNullOrWhiteSpace(achievement.Name) ? achievement.Id : achievement.Name;
    }
}
