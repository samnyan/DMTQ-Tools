using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

public sealed class GameTableManagerWorkflow : IProjectWorkflow
{
    private readonly GameTableManagerState _state;
    private readonly IPatchProjectRepository _repository;
    private readonly PlatformPackageImporter _platformImporter;
    private readonly PlatformPackageExporter _platformExporter;
    private readonly ResourceManagerService _resourceManager;

    public GameTableManagerWorkflow(
        GameTableManagerState state,
        IPatchProjectRepository repository,
        PlatformPackageImporter platformImporter,
        PlatformPackageExporter platformExporter,
        ResourceManagerService resourceManager)
    {
        _state = state;
        _repository = repository;
        _platformImporter = platformImporter;
        _platformExporter = platformExporter;
        _resourceManager = resourceManager;
    }

    public async Task CreateProjectAsync(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "resources"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "exports"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "temp"));
        _state.SetProjectRoot(projectRoot);

        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo(projectRoot, null, "0.0.0", null)
        };
        _state.SetPackage(package);
        await _repository.SaveAsync(
                package,
                _state.ExportCompressionMode,
                _state.CreateExportOptions(),
                projectRoot,
                CancellationToken.None)
            .ConfigureAwait(false);
        _state.Diagnostics.Add("Empty project created and saved.");
    }

    public async Task ImportPlatformPackageAsync(
        string packageRoot,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (string.IsNullOrWhiteSpace(_state.ProjectRoot))
            throw new InvalidOperationException("Create or open a project directory before importing a platform package.");

        if (_state.CurrentPackage is null)
            _state.SetPackage(new PatchPackage { ProjectInfo = new ProjectInfo(_state.ProjectRoot, null, null, null) });

        await _platformImporter.ImportPlatformAsync(_state.CurrentPackage!, packageRoot, platform, cancellationToken).ConfigureAwait(false);
        _state.SetPlatformImportResult(platform);

        // Report integrity errors found during import
        if (_state.CurrentPackage!.IntegrityErrors.Count > 0)
        {
            foreach (var error in _state.CurrentPackage.IntegrityErrors)
            {
                _state.Diagnostics.Add(error);
            }
        }

        await _repository.SaveAsync(_state.CurrentPackage!, _state.ExportCompressionMode, _state.CreateExportOptions(), _state.ProjectRoot!, cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add("Auto-saved after import.");
    }

    public async Task ExportPlatformPackageAsync(
        string exportRoot,
        string platform,
        PlatformExportMode exportMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import a package before exporting.");

        var result = await _platformExporter.ExportPlatformAsync(_state.CurrentPackage, exportRoot,
            new PlatformExportOptions { Platform = platform, Mode = exportMode, PackageOptions = _state.CreateExportOptions() }, cancellationToken).ConfigureAwait(false);
        _state.SetPlatformExportResult(result);
    }

    public async Task SaveProjectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_state.ProjectRoot)) throw new InvalidOperationException("Create or open a project directory before saving.");
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import a package before saving.");

        await _repository.SaveAsync(_state.CurrentPackage, _state.ExportCompressionMode, _state.CreateExportOptions(), _state.ProjectRoot, cancellationToken).ConfigureAwait(false);
        _state.IsDirty = false;
        _state.Diagnostics.Add("Project saved.");
    }

    public async Task OpenProjectAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var snapshot = await _repository.LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        _state.RestoreProject(snapshot);
    }

    public async Task AddOrReplaceResourceAsync(string sourceFilePath, string packageRelativePath, string? platform,
        IReadOnlyCollection<string> includedPlatforms, bool compressed, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        await _resourceManager.AddOrReplaceResourceAsync(_state.CurrentPackage, sourceFilePath, packageRelativePath, platform, includedPlatforms, compressed, cancellationToken).ConfigureAwait(false);
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource added or replaced: {packageRelativePath}");
    }

    public async Task RemoveResourceAsync(string packageRelativePath, string? platform, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.RemoveResource(_state.CurrentPackage, packageRelativePath, platform);
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource removed from project: {packageRelativePath}");
    }

    public async Task SetResourceCompressionAsync(string packageRelativePath, string? platform, bool compressed, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.SetCompression(_state.CurrentPackage, packageRelativePath, platform, compressed);
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource compression updated: {packageRelativePath} = {compressed}");
    }

    public async Task SetPreviewIncludedPlatformsAsync(string packageRelativePath, IReadOnlyCollection<string> includedPlatforms, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.SetPreviewIncludedPlatforms(_state.CurrentPackage, packageRelativePath, includedPlatforms);
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Preview platform inclusion updated: {packageRelativePath}");
    }
}
