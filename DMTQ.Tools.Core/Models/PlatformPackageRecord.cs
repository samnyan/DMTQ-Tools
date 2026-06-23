using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

public sealed class PlatformPackageRecord
{
    [JsonInclude]
    public required string Platform { get; init; }
    [JsonInclude]
    public required string SourcePackageRoot { get; init; }
    public string? Version { get; init; }
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<PatchFileEntry> BaselineManifestEntries { get; set; } = [];
    public int ImportedTableFileCount { get; set; }
    public int ImportedResourceFileCount { get; set; }
    public int MissingPhysicalFileCount { get; set; }

    [SetsRequiredMembers]
    public PlatformPackageRecord() { Platform = ""; SourcePackageRoot = ""; }
}
