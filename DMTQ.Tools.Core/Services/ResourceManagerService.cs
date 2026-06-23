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
                PackageRelativePath = resource.PackageRelativePath,
                ProjectRelativePath = resource.ProjectRelativePath,
                Category = resource.Category,
                Compressed = resource.Compressed,
                Platform = resource.Platform,
                IncludedPlatforms = resource.IncludedPlatforms?.ToArray() ?? [],
                SourcePackagePath = resource.SourcePackagePath
            })
            .OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Platform ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.PackageRelativePath, StringComparer.OrdinalIgnoreCase)
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

        var normalizedPath = PathClassifier.NormalizePackageRelativePath(packageRelativePath);
        var category = PathClassifier.ResourceCategory(normalizedPath);
        var isPreview = category.Equals("preview", StringComparison.OrdinalIgnoreCase);
        if (!isPreview && string.IsNullOrWhiteSpace(platform))
        {
            throw new InvalidOperationException("Non-preview resources must target a platform.");
        }

        var projectRelativePath = isPreview
            ? Path.Combine("resources", normalizedPath).Replace('\\', '/')
            : Path.Combine("resources", platform!, normalizedPath).Replace('\\', '/');
        var archivePath = Path.Combine(package.ProjectInfo.ProjectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath) ?? package.ProjectInfo.ProjectRoot);
        File.Copy(sourceFilePath, archivePath, overwrite: true);
        await Task.CompletedTask.ConfigureAwait(false);

        RemoveResource(package, normalizedPath, isPreview ? null : platform);
        package.Resources.Add(new ResourceFile(
            normalizedPath,
            projectRelativePath,
            category,
            compressed,
            null,
            isPreview ? null : platform,
            isPreview ? includedPlatforms.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList() : null));
    }

    public void RemoveResource(PatchPackage package, string packageRelativePath, string? platform)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRelativePath);

        var normalizedPath = PathClassifier.NormalizePackageRelativePath(packageRelativePath);
        package.Resources.RemoveAll(resource =>
            resource.PackageRelativePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resource.Platform, platform, StringComparison.OrdinalIgnoreCase));
    }

    public void SetCompression(PatchPackage package, string packageRelativePath, string? platform, bool compressed)
    {
        ArgumentNullException.ThrowIfNull(package);
        var resource = FindResource(package, packageRelativePath, platform);
        ReplaceResource(package, resource, resource with { Compressed = compressed });
    }

    public void SetPreviewIncludedPlatforms(PatchPackage package, string packageRelativePath, IReadOnlyCollection<string> includedPlatforms)
    {
        ArgumentNullException.ThrowIfNull(package);
        var resource = FindResource(package, packageRelativePath, platform: null);
        if (!resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only preview resources can use shared platform inclusion flags.");
        }

        var normalizedPlatforms = includedPlatforms
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ReplaceResource(package, resource, resource with { IncludedPlatforms = normalizedPlatforms });
    }

    private static ResourceFile FindResource(PatchPackage package, string packageRelativePath, string? platform)
    {
        var normalizedPath = PathClassifier.NormalizePackageRelativePath(packageRelativePath);
        return package.Resources.FirstOrDefault(resource =>
                resource.PackageRelativePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(resource.Platform, platform, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Resource '{normalizedPath}' was not found.");
    }

    private static void ReplaceResource(PatchPackage package, ResourceFile oldResource, ResourceFile newResource)
    {
        var index = package.Resources.IndexOf(oldResource);
        package.Resources[index] = newResource;
    }
}
