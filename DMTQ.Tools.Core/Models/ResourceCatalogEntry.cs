namespace DMTQ.Tools.Core.Models;

public sealed class ResourceCatalogEntry
{
    public required string PackageRelativePath { get; init; }
    public required string ProjectRelativePath { get; init; }
    public required string Category { get; init; }
    public bool Compressed { get; init; }
    public string? Platform { get; init; }
    public IReadOnlyList<string> IncludedPlatforms { get; init; } = [];
    public string? SourcePackagePath { get; init; }
    public bool IsSharedPreview => Category.Equals("preview", StringComparison.OrdinalIgnoreCase);
}
