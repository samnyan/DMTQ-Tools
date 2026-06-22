using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Project workflow contract used by Blazor pages.
/// Implemented by the MAUI host, mocked in UI tests.
/// </summary>
public interface IProjectWorkflow
{
    Task CreateProjectAsync(string projectRoot);
    Task ImportPlatformPackageAsync(string packageRoot, string platform, CancellationToken cancellationToken = default);
    Task ExportPlatformPackageAsync(string exportRoot, string platform, PlatformExportMode exportMode, CancellationToken cancellationToken = default);
    Task SaveProjectAsync(CancellationToken cancellationToken = default);
    Task OpenProjectAsync(string projectRoot, CancellationToken cancellationToken = default);
    Task AddOrReplaceResourceAsync(string sourceFilePath, string packageRelativePath, string? platform, IReadOnlyCollection<string> includedPlatforms, bool compressed, CancellationToken cancellationToken = default);
    Task RemoveResourceAsync(string packageRelativePath, string? platform, CancellationToken cancellationToken = default);
    Task SetResourceCompressionAsync(string packageRelativePath, string? platform, bool compressed, CancellationToken cancellationToken = default);
    Task SetPreviewIncludedPlatformsAsync(string packageRelativePath, IReadOnlyCollection<string> includedPlatforms, CancellationToken cancellationToken = default);
}
