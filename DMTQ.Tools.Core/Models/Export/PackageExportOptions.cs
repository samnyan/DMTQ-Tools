using DMTQ.Tools.Core.Services;

using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Models.Export;

public sealed class PackageExportOptions
{
    public Dictionary<string, bool> CompressionOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldCompress(PatchFileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var normalizedPath = FileUtility.NormalizePackageRelativePath(entry.FileName);
        return CompressionOverrides.TryGetValue(normalizedPath, out var compressed)
            ? compressed
            : entry.Compressed;
    }

    public void SetCompression(string packageRelativePath, bool compressed)
    {
        var normalizedPath = FileUtility.NormalizePackageRelativePath(packageRelativePath);
        CompressionOverrides[normalizedPath] = compressed;
    }
}
