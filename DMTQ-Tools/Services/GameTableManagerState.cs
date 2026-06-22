using DMTQ.Tools.Core.Models;

namespace DMTQ_Tools.Services;

public sealed class GameTableManagerState
{
    public string? ProjectRoot { get; private set; }
    public PatchPackage? CurrentPackage { get; private set; }
    public PatchManifest? LastExportManifest { get; private set; }
    public PatchValidationResult? LastValidationResult { get; private set; }
    public string ExportCompressionMode { get; set; } = "Keep";
    public List<string> Diagnostics { get; } = [];

    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectRoot);
    public bool HasPackage => CurrentPackage is not null;

    public void SetProjectRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ProjectRoot = projectRoot;
        Diagnostics.Add($"Project root set: {projectRoot}");
    }

    public void SetPackage(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        CurrentPackage = package;
        LastExportManifest = null;
        LastValidationResult = null;
        Diagnostics.Add($"Imported package: {package.Manifest.Entries.Count} manifest entries, {package.Tables.Tables.Count} tables, {package.Resources.Count} resources.");
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

    public PackageExportOptions CreateExportOptions()
    {
        var options = new PackageExportOptions();
        if (CurrentPackage is null)
        {
            return options;
        }

        if (ExportCompressionMode == "CompressAll")
        {
            foreach (var entry in CurrentPackage.Manifest.Entries)
            {
                options.SetCompression(entry.FileName, compressed: true);
            }
        }
        else if (ExportCompressionMode == "UncompressAll")
        {
            foreach (var entry in CurrentPackage.Manifest.Entries)
            {
                options.SetCompression(entry.FileName, compressed: false);
            }
        }

        return options;
    }
}
