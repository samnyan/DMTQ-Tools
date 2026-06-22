namespace DMTQ.Tools.Core.Models;

public sealed class PatchPackage
{
    public required ProjectInfo ProjectInfo { get; init; }
    public PatchManifest Manifest { get; } = new();
    public GameTableSet Tables { get; } = new();
    public List<ResourceFile> Resources { get; } = [];
    public List<PlatformPackageRecord> Platforms { get; } = [];
}
