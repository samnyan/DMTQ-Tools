using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class ResourceManagerService
{
    public IReadOnlyList<ResourceCatalogEntry> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return package.Resources
            .Select(resource => new ResourceCatalogEntry
            {
                FileName = resource.FileName,
                Category = resource.Category,
                Compressed = resource.Compressed,
                PlatformManifest = resource.PlatformManifest
                    .Select(m => new PlatformManifestInfo
                    {
                        Platform = m.Platform,
                        Exist = m.Exist,
                        SourceFileSize = m.SourceFileSize,
                        SourceChecksum = m.SourceChecksum
                    })
                    .ToArray()
            })
            .OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task AddOrReplaceResourceAsync(
        PatchPackage package,
        string sourceFilePath,
        string packageRelativePath,
        string? platform,
        IReadOnlyCollection<string> includedPlatforms,
        bool compressed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRelativePath);
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Resource source file was not found.", sourceFilePath);
        }

        var normalizedPath = FileUtility.NormalizePackageRelativePath(packageRelativePath);
        var category = FileUtility.ResourceCategory(normalizedPath);
        var isPreview = category.Equals("preview", StringComparison.OrdinalIgnoreCase);

        // Archive the file
        string archiveSubPath;
        if (isPreview)
        {
            archiveSubPath = Path.Combine("resources", normalizedPath);
        }
        else if (!string.IsNullOrWhiteSpace(platform))
        {
            archiveSubPath = Path.Combine("resources", platform, normalizedPath);
        }
        else
        {
            throw new InvalidOperationException("Non-preview resources must target a platform.");
        }

        var archivePath = Path.Combine(package.ProjectInfo.ProjectRoot, archiveSubPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath) ?? package.ProjectInfo.ProjectRoot);
        File.Copy(sourceFilePath, archivePath, overwrite: true);

        // Compute checksum and size of the archived file
        var checksum = await FileUtility.ComputeMd5Async(archivePath, cancellationToken).ConfigureAwait(false);
        var fileSize = FileUtility.GetFileSize(archivePath);

        // Create or update ResourceFile
        var resourceFile = package.Resources.FirstOrDefault(r =>
            r.FileName.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (resourceFile is null)
        {
            resourceFile = new ResourceFile
            {
                FileName = normalizedPath,
                Category = category,
                Compressed = compressed
            };
            package.Resources.Add(resourceFile);
        }
        else
        {
            resourceFile.Compressed = compressed;
        }

        // Update PlatformManifest
        var platforms = isPreview
            ? includedPlatforms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : (platform is not null ? new[] { platform } : []);

        foreach (var plat in platforms)
        {
            var existingEntry = resourceFile.PlatformManifest
                .FirstOrDefault(m => m.Platform.Equals(plat, StringComparison.OrdinalIgnoreCase));
            if (existingEntry is null)
            {
                resourceFile.PlatformManifest.Add(new PlatformManifestEntry
                {
                    Platform = plat,
                    Exist = true,
                    Checksum = checksum,
                    SourceFileSize = fileSize
                });
            }
            else
            {
                existingEntry.Exist = true;
                existingEntry.Checksum = checksum;
                existingEntry.SourceFileSize = fileSize;
            }
        }
    }

    public void RemoveResource(PatchPackage package, string packageRelativePath, string? platform)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRelativePath);

        var normalizedPath = FileUtility.NormalizePackageRelativePath(packageRelativePath);
        package.Resources.RemoveAll(resource =>
            resource.FileName.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public void SetCompression(PatchPackage package, string packageRelativePath, string? platform, bool compressed)
    {
        ArgumentNullException.ThrowIfNull(package);
        var resource = FindResource(package, packageRelativePath);
        resource.Compressed = compressed;
    }

    public void SetPreviewIncludedPlatforms(PatchPackage package, string packageRelativePath, IReadOnlyCollection<string> includedPlatforms)
    {
        ArgumentNullException.ThrowIfNull(package);
        var resource = FindResource(package, packageRelativePath);
        if (!resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only preview resources can use shared platform inclusion flags.");
        }

        // Replace all share entries with the new set
        resource.PlatformManifest.RemoveAll(m => m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase));
        foreach (var plat in includedPlatforms.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (!resource.PlatformManifest.Any(m => m.Platform.Equals(plat, StringComparison.OrdinalIgnoreCase)))
            {
                resource.PlatformManifest.Add(new PlatformManifestEntry
                {
                    Platform = plat,
                    Exist = true
                });
            }
        }
    }

    private static ResourceFile FindResource(PatchPackage package, string packageRelativePath)
    {
        var normalizedPath = FileUtility.NormalizePackageRelativePath(packageRelativePath);
        return package.Resources.FirstOrDefault(resource =>
                resource.FileName.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Resource '{normalizedPath}' was not found.");
    }
}
