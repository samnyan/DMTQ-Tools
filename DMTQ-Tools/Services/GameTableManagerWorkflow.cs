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
    private readonly InstallPackImporter _installPackImporter;
    private readonly SemaphoreSlim _projectSaveGate = new(1, 1);

    public GameTableManagerWorkflow(
        GameTableManagerState state,
        IPatchProjectRepository repository,
        PlatformPackageImporter platformImporter,
        PlatformPackageExporter platformExporter,
        ResourceManagerService resourceManager,
        InstallPackImporter installPackImporter)
    {
        _state = state;
        _repository = repository;
        _platformImporter = platformImporter;
        _platformExporter = platformExporter;
        _resourceManager = resourceManager;
        _installPackImporter = installPackImporter;
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
            ProjectInfo = new ProjectInfo(projectRoot, null, "0.0.0", null),
            ProductCategoryMappings = ProductCategoryDefaults.Create()
        };
        _state.SetPackage(package);
        await _repository.SaveAsync(
                package,
                _state.ExportCompressionMode,
                _state.CreateExportOptions(),
                projectRoot,
                CancellationToken.None)
            .ConfigureAwait(false);
        _state.IsDirty = false;
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
        _state.IsDirty = false;
        _state.Diagnostics.Add("Auto-saved after import.");
    }

    public async Task<InstallPackImportResult> ImportInstallPackAsync(
        string streamingAssetsRoot,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamingAssetsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (string.IsNullOrWhiteSpace(_state.ProjectRoot))
            throw new InvalidOperationException("Create or open a project directory before importing InstallPack files.");

        if (_state.CurrentPackage is null)
            _state.SetPackage(new PatchPackage { ProjectInfo = new ProjectInfo(_state.ProjectRoot, null, null, null) });

        var result = await _installPackImporter.ImportAsync(
            _state.CurrentPackage!, streamingAssetsRoot, platform, cancellationToken).ConfigureAwait(false);
        _state.IsDirty = true;
        await _repository.SaveAsync(_state.CurrentPackage!, _state.ExportCompressionMode,
            _state.CreateExportOptions(), _state.ProjectRoot, cancellationToken).ConfigureAwait(false);
        _state.IsDirty = false;
        _state.Diagnostics.Add($"Imported {result.ImportedFiles} {platform} InstallPack resources.");
        foreach (var error in result.Errors)
            _state.Diagnostics.Add("InstallPack import error: " + error);
        return result;
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
        if (_state.IsDirty) throw new InvalidOperationException("Save the project before exporting.");

        var result = await _platformExporter.ExportPlatformAsync(_state.CurrentPackage, exportRoot,
            new PlatformExportOptions { Platform = platform, Mode = exportMode, PackageOptions = _state.CreateExportOptions() }, cancellationToken).ConfigureAwait(false);
        _state.SetPlatformExportResult(result);
    }

    public async Task SaveProjectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_state.ProjectRoot)) throw new InvalidOperationException("Create or open a project directory before saving.");
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import a package before saving.");

        await _projectSaveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_state.IsDirty) return;
            await _repository.SaveAsync(_state.CurrentPackage, _state.ExportCompressionMode, _state.CreateExportOptions(), _state.ProjectRoot, cancellationToken).ConfigureAwait(false);
            _state.IsDirty = false;
            _state.Diagnostics.Add("Project saved.");
        }
        finally
        {
            _projectSaveGate.Release();
        }
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
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource added or replaced: {packageRelativePath}");
    }

    public async Task AddOrReplaceResourcesAsync(
        IReadOnlyCollection<ResourceReplacement> replacements,
        CancellationToken cancellationToken = default)
    {
        if (replacements.Count == 0) return;
        if (_state.CurrentPackage is null || string.IsNullOrWhiteSpace(_state.ProjectRoot))
            throw new InvalidOperationException("Import or open a project before managing resources.");

        var package = _state.CurrentPackage;
        var projectRoot = Path.GetFullPath(_state.ProjectRoot);
        var backupRoot = Path.Combine(Path.GetTempPath(), $"dmtq-resource-transaction-{Guid.NewGuid():N}");
        var resourceSnapshot = package.Resources.Select(CloneResource).ToArray();
        var fileBackups = new List<(string Destination, string? Backup)>();
        Directory.CreateDirectory(backupRoot);
        try
        {
            var uniqueTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var replacement in replacements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(replacement.SourceFilePath))
                    throw new FileNotFoundException("Replacement source file was not found.", replacement.SourceFilePath);
                var relativePath = FileUtility.NormalizePackageRelativePath(replacement.FileName);
                var platform = replacement.Platform.Trim().ToLowerInvariant();
                if (platform is not ("android" or "ios"))
                    throw new InvalidDataException($"Unsupported resource platform '{replacement.Platform}'.");
                var targetKey = platform + "/" + relativePath;
                if (!uniqueTargets.Add(targetKey))
                    throw new InvalidDataException($"The batch contains duplicate target '{targetKey}'.");

                var destination = Path.GetFullPath(Path.Combine(projectRoot, "resources", platform,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                var resourcesRoot = Path.GetFullPath(Path.Combine(projectRoot, "resources", platform));
                var rootPrefix = resourcesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Resource target escapes its platform folder: {relativePath}");

                if (File.Exists(destination))
                {
                    var backup = Path.Combine(backupRoot, fileBackups.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(destination, backup);
                    fileBackups.Add((destination, backup));
                }
                else
                {
                    fileBackups.Add((destination, null));
                }
            }

            foreach (var replacement in replacements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _resourceManager.AddOrReplaceResourceAsync(package, replacement.SourceFilePath,
                    replacement.FileName, replacement.Platform, [], replacement.Compressed, cancellationToken)
                    .ConfigureAwait(false);
            }

            _state.IsDirty = true;
            await _repository.SaveAsync(package, _state.ExportCompressionMode, _state.CreateExportOptions(),
                projectRoot, cancellationToken).ConfigureAwait(false);
            _state.IsDirty = false;
            _state.Diagnostics.Add($"Applied {replacements.Count} resource replacements as one project transaction.");
        }
        catch
        {
            package.Resources.Clear();
            package.Resources.AddRange(resourceSnapshot);
            foreach (var (destination, backup) in fileBackups)
            {
                if (backup is null)
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(backup, destination, overwrite: true);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, recursive: true);
        }
    }

    private static ResourceFile CloneResource(ResourceFile resource)
        => new()
        {
            FileName = resource.FileName,
            Category = resource.Category,
            Compressed = resource.Compressed,
            AcquireOnDemand = resource.AcquireOnDemand,
            PlatformManifest = resource.PlatformManifest.Select(entry => new PlatformManifestEntry
            {
                Platform = entry.Platform,
                Exist = entry.Exist,
                IsInstallPack = entry.IsInstallPack,
                SourceFileSize = entry.SourceFileSize,
                SourceChecksum = entry.SourceChecksum,
                SourceCompressedFileSize = entry.SourceCompressedFileSize,
                SourceCompressedChecksum = entry.SourceCompressedChecksum,
                Checksum = entry.Checksum,
                InstallPackBaselineChecksum = entry.InstallPackBaselineChecksum
            }).ToList()
        };

    public async Task AddResourceStubAsync(
        string packageRelativePath,
        bool compressed,
        CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null)
            throw new InvalidOperationException("Import or open a project before managing resources.");

        _resourceManager.AddResourceStub(_state.CurrentPackage, packageRelativePath, compressed);
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource stub added: {packageRelativePath}");
    }

    public async Task RemoveResourceAsync(string packageRelativePath, string? platform, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.RemoveResource(_state.CurrentPackage, packageRelativePath, platform);
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource removed from project: {packageRelativePath}");
    }

    public async Task SetResourceCompressionAsync(string packageRelativePath, string? platform, bool compressed, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.SetCompression(_state.CurrentPackage, packageRelativePath, platform, compressed);
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Resource compression updated: {packageRelativePath} = {compressed}");
    }

    public async Task SetResourceInstallPackAsync(
        string packageRelativePath,
        string platform,
        bool isInstallPack,
        CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null)
            throw new InvalidOperationException("Import or open a project before managing resources.");

        await _resourceManager.SetInstallPackAsync(
            _state.CurrentPackage, packageRelativePath, platform, isInstallPack, cancellationToken).ConfigureAwait(false);
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPreviewIncludedPlatformsAsync(string packageRelativePath, IReadOnlyCollection<string> includedPlatforms, CancellationToken cancellationToken = default)
    {
        if (_state.CurrentPackage is null) throw new InvalidOperationException("Import or open a project before managing resources.");
        _resourceManager.SetPreviewIncludedPlatforms(_state.CurrentPackage, packageRelativePath, includedPlatforms);
        _state.IsDirty = true;
        await SaveProjectAsync(cancellationToken).ConfigureAwait(false);
        _state.Diagnostics.Add($"Preview platform inclusion updated: {packageRelativePath}");
    }
}
