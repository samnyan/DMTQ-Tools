namespace DMTQ.Tools.Core.Models.Project;

/// <summary>
/// Represents one resource file in the patch package, keyed by its
/// package-relative path (e.g. "dlc/d3_a0.unity3d").
/// Holds per-platform manifest metadata and per-platform existence flags.
/// </summary>
public sealed class ResourceFile
{
    /// <summary>Package-relative path with forward slashes (e.g. "preview/coloursof.p.opus").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>"preview", "dlc", "Fonts", or "other".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Whether the file uses LZ4 compression.</summary>
    public bool Compressed { get; set; }

    /// <summary>Acquire-on-demand flag from the original manifest (0 = normal).</summary>
    public int AcquireOnDemand { get; set; }

    /// <summary>Per-platform manifest data (one entry per platform, or one "share" entry for preview).</summary>
    public List<PlatformManifestEntry> PlatformManifest { get; set; } = [];
}

/// <summary>
/// Per-platform metadata for a resource file — original manifest checksums/sizes plus
/// the actual local file checksum.  Also tracks whether the physical file exists.
/// </summary>
public sealed class PlatformManifestEntry
{
    /// <summary>"android", "ios", or "share" (for preview resources shared by all platforms).</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Whether the local physical file exists under the project resources tree.</summary>
    public bool Exist { get; set; }

    /// <summary>Original uncompressed file size from the patch manifest.</summary>
    public long SourceFileSize { get; set; }

    /// <summary>Original MD5 checksum (lowercase hex) from the patch manifest.</summary>
    public string SourceChecksum { get; set; } = string.Empty;

    /// <summary>Original compressed file size from the patch manifest.</summary>
    public long SourceCompressedFileSize { get; set; }

    /// <summary>Original compressed MD5 checksum (lowercase hex) from the patch manifest.</summary>
    public string SourceCompressedChecksum { get; set; } = string.Empty;

    /// <summary>Actual local file MD5 checksum (computed when the file was archived).</summary>
    public string Checksum { get; set; } = string.Empty;
}
