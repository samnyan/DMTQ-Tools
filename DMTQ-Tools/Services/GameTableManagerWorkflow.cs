using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

public sealed class GameTableManagerWorkflow(
    GameTableManagerState state,
    PatchPackageImporter importer,
    PatchPackageExporter exporter,
    PatchPackageValidator validator,
    IPatchProjectRepository repository,
    PlatformPackageImporter platformImporter,
    PlatformPackageExporter platformExporter)
{
    public void CreateProject(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "resources"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "exports"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "temp"));
        state.SetProjectRoot(projectRoot);
    }

    public async Task ImportPackageAsync(
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        if (string.IsNullOrWhiteSpace(state.ProjectRoot))
        {
            throw new InvalidOperationException("Create or open a project directory before importing a package.");
        }

        var package = await importer.ImportAsync(packageRoot, state.ProjectRoot, cancellationToken)
            .ConfigureAwait(false);
        state.SetPackage(package);
    }

    public async Task ExportPackageAsync(
        string exportRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        if (state.CurrentPackage is null)
        {
            throw new InvalidOperationException("Import a package before exporting.");
        }

        var options = state.CreateExportOptions();
        var manifest = await exporter.ExportAsync(state.CurrentPackage, exportRoot, options, cancellationToken)
            .ConfigureAwait(false);
        var validation = await validator.ValidateAsync(manifest, exportRoot, cancellationToken)
            .ConfigureAwait(false);
        state.SetExportResult(manifest, validation);
    }

    public async Task SaveProjectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state.ProjectRoot))
        {
            throw new InvalidOperationException("Create or open a project directory before saving.");
        }

        if (state.CurrentPackage is null)
        {
            throw new InvalidOperationException("Import a package before saving.");
        }

        await repository.SaveAsync(
                state.CurrentPackage,
                state.ExportCompressionMode,
                state.CreateExportOptions(),
                state.ProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        state.Diagnostics.Add("Project saved.");
    }

    public async Task OpenProjectAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var snapshot = await repository.LoadAsync(projectRoot, cancellationToken)
            .ConfigureAwait(false);
        state.RestoreProject(snapshot);
    }

    public async Task ImportPlatformPackageAsync(
        string packageRoot,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (string.IsNullOrWhiteSpace(state.ProjectRoot))
        {
            throw new InvalidOperationException("Create or open a project directory before importing a platform package.");
        }

        if (state.CurrentPackage is null)
        {
            state.SetPackage(new DMTQ.Tools.Core.Models.PatchPackage
            {
                ProjectInfo = new DMTQ.Tools.Core.Models.ProjectInfo(state.ProjectRoot, null, null, null)
            });
        }

        await platformImporter.ImportPlatformAsync(state.CurrentPackage, packageRoot, platform, cancellationToken)
            .ConfigureAwait(false);
        state.SetPlatformImportResult(platform);
    }

    public async Task ExportPlatformPackageAsync(
        string exportRoot,
        string platform,
        PlatformExportMode exportMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (state.CurrentPackage is null)
        {
            throw new InvalidOperationException("Import a package before exporting.");
        }

        var result = await platformExporter.ExportPlatformAsync(
                state.CurrentPackage,
                exportRoot,
                new DMTQ.Tools.Core.Models.PlatformExportOptions
                {
                    Platform = platform,
                    Mode = exportMode,
                    PackageOptions = state.CreateExportOptions()
                },
                cancellationToken)
            .ConfigureAwait(false);
        state.SetPlatformExportResult(result);
    }
}
