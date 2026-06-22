namespace DMTQ.Tools.Core.Models;

public sealed class PatchPackage
{
    public required ProjectInfo ProjectInfo { get; init; }
    public PatchManifest Manifest { get; } = new();
    public GameTableSet Tables { get; } = new();
    public List<ResourceFile> Resources { get; } = [];
    public List<PlatformPackageRecord> Platforms { get; } = [];

    /// <summary>Song entities with their patterns and localizations.
    /// Populated during import; used for editing and exported back to CSV tables.</summary>
    public List<Song> Songs { get; } = [];
}
