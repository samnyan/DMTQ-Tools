namespace DMTQ.Tools.Core.Models;

/// <summary>
/// Lightweight view of a <see cref="Project.ResourceFile"/> for the Resources UI page.
/// </summary>
public sealed class ResourceCatalogEntry
{
    public required string FileName { get; init; }
    public required string Category { get; init; }
    public bool Compressed { get; init; }
    public IReadOnlyList<PlatformManifestInfo> PlatformManifest { get; init; } = [];
    public bool IsSharedPreview => Category.Equals("preview", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Lightweight view of a <see cref="Project.PlatformManifestEntry"/> for the Resources UI.
/// </summary>
public sealed class PlatformManifestInfo
{
    public string Platform { get; init; } = string.Empty;
    public bool Exist { get; init; }
    public long SourceFileSize { get; init; }
    public string SourceChecksum { get; init; } = string.Empty;
}
