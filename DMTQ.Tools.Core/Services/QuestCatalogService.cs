using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Builds the editable localized quest catalog for a client platform.</summary>
public sealed class QuestCatalogService
{
    public IReadOnlyList<Quest> BuildCatalog(PatchPackage package, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.GetPlatformTables(platform).Quests
            .OrderBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetDisplayName(Quest quest, string language)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.NamesByLanguage.TryGetValue(language, out var name)
               && !string.IsNullOrWhiteSpace(name)
            ? name
            : quest.Id;
    }
}
