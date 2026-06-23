using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

public sealed class GameTableManagerState : IProjectState
{
    public string? ProjectRoot { get; private set; }
    public PatchPackage? CurrentPackage { get; private set; }
    public PatchManifest? LastExportManifest { get; private set; }
    public PatchValidationResult? LastValidationResult { get; private set; }
    public string ExportCompressionMode { get; set; } = "Keep";
    public PackageExportOptions? RestoredExportOptions { get; private set; }
    public List<string> Diagnostics { get; } = [];

    public PlatformExportResult? LastPlatformExportResult { get; private set; }
    public string SelectedExportPlatform { get; set; } = "android";
    public PlatformExportMode PlatformExportMode { get; set; } = PlatformExportMode.Full;
    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectRoot);
    public bool HasPackage => CurrentPackage is not null;

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            StateChanged?.Invoke();
        }
    }
    public IReadOnlyList<string> ImportIntegrityErrors => CurrentPackage?.IntegrityErrors ?? [];
    public event Action? StateChanged;

    private void MarkDirty()
    {
        IsDirty = true;
    }

    public void SetProjectRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ProjectRoot = projectRoot;
        Diagnostics.Add($"Project root set: {projectRoot}");
        MarkDirty();
    }

    public void SetPackage(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        CurrentPackage = package;
        LastExportManifest = null;
        LastValidationResult = null;
        LastPlatformExportResult = null;
        RestoredExportOptions = null;
        Diagnostics.Add($"Imported package: {package.Resources.Count} resources, {package.Tables.Tables.Count} tables.");
        MarkDirty();
    }

    public void SetExportResult(PatchManifest manifest, PatchValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(validation);
        LastExportManifest = manifest;
        LastValidationResult = validation;
        Diagnostics.Add(validation.IsValid
            ? $"Export validation passed: {manifest.Entries.Count} manifest entries."
            : $"Export validation failed: {validation.Errors.Count} errors.");
    }

    public void SetPlatformImportResult(string platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        LastExportManifest = null;
        LastValidationResult = null;
        LastPlatformExportResult = null;
        SelectedExportPlatform = platform;
        var resourceCount = CurrentPackage?.Resources.Count(r =>
            r.PlatformManifest.Any(m => m.Platform == platform)) ?? 0;
        Diagnostics.Add($"Platform import completed: {platform}, {resourceCount} resources.");
    }

    public void SetPlatformExportResult(PlatformExportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastPlatformExportResult = result;
        LastExportManifest = result.Manifest;
        LastValidationResult = result.Validation;
        Diagnostics.Add(result.Validation.IsValid
            ? $"Platform export validation passed: {result.Platform}, {result.ManifestEntryCount} manifest entries, {result.FilesWritten} files written, {result.FilesSkippedAsBaseline} skipped."
            : $"Platform export validation failed: {result.Platform}, {result.Validation.Errors.Count} errors.");
    }

    public PackageExportOptions CreateExportOptions()
    {
        var options = new PackageExportOptions();
        if (CurrentPackage is null)
        {
            return options;
        }

        if (ExportCompressionMode == "Keep" && RestoredExportOptions is not null)
        {
            return RestoredExportOptions;
        }

        if (ExportCompressionMode == "CompressAll")
        {
            foreach (var resource in CurrentPackage.Resources)
            {
                options.SetCompression(resource.FileName, compressed: true);
            }
        }
        else if (ExportCompressionMode == "UncompressAll")
        {
            foreach (var resource in CurrentPackage.Resources)
            {
                options.SetCompression(resource.FileName, compressed: false);
            }
        }

        return options;
    }

    public void SetExportCompressionMode(string exportCompressionMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportCompressionMode);
        ExportCompressionMode = exportCompressionMode;
        Diagnostics.Add($"Export compression mode set: {exportCompressionMode}");
        MarkDirty();
    }

    public void RestoreProject(PatchProjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ProjectRoot = snapshot.Package.ProjectInfo.ProjectRoot;
        CurrentPackage = snapshot.Package;
        ExportCompressionMode = snapshot.ExportCompressionMode;
        RestoredExportOptions = snapshot.ExportOptions;
        LastExportManifest = null;
        LastValidationResult = null;
        LastPlatformExportResult = null;
        Diagnostics.Add($"Opened project: {ProjectRoot}");
        Diagnostics.Add($"Loaded package: {snapshot.Package.Resources.Count} resources, {snapshot.Package.Tables.Tables.Count} tables.");
        StateChanged?.Invoke();
    }
}
