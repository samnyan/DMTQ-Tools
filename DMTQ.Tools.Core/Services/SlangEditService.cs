using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Provides ordered CRUD operations for the shared slang table.</summary>
public sealed class SlangEditService
{
    public IReadOnlyList<SlangEntry> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.SlangEntries;
    }

    public SlangEntry Add(PatchPackage package, string value)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        var entry = new SlangEntry { Id = Guid.NewGuid().ToString("N"), Value = normalized };
        package.SlangEntries.Add(entry);
        return entry;
    }

    public void Update(PatchPackage package, string id, string value)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var entry = Find(package, id);
        var normalized = value.Trim();
        entry.Value = normalized;
    }

    public void Remove(PatchPackage package, string id)
    {
        ArgumentNullException.ThrowIfNull(package);
        package.SlangEntries.Remove(Find(package, id));
    }

    private static SlangEntry Find(PatchPackage package, string id)
        => package.SlangEntries.FirstOrDefault(entry => entry.Id == id)
           ?? throw new InvalidOperationException($"Slang entry '{id}' was not found.");
}
