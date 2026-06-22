namespace DMTQ.Tools.Core.Models;

public sealed class PlatformPackageRecord
{
    public required string Platform { get; init; }
    public required string SourcePackageRoot { get; init; }
    public string? Version { get; init; }
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<PatchFileEntry> BaselineManifestEntries { get; } = [];
    public int ImportedTableFileCount { get; set; }
    public int ImportedResourceFileCount { get; set; }
    public int MissingPhysicalFileCount { get; set; }
}
