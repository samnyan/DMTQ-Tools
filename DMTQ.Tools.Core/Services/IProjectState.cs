using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Project state contract used by Blazor pages.
/// Implemented by the MAUI host, mocked in UI tests.
/// </summary>
public interface IProjectState
{
    string? ProjectRoot { get; }
    PatchPackage? CurrentPackage { get; }
    PatchManifest? LastExportManifest { get; }
    PatchValidationResult? LastValidationResult { get; }
    string ExportCompressionMode { get; set; }
    PackageExportOptions? RestoredExportOptions { get; }
    List<string> Diagnostics { get; }
    PlatformExportResult? LastPlatformExportResult { get; }
    string SelectedExportPlatform { get; set; }
    PlatformExportMode PlatformExportMode { get; set; }
    IReadOnlyList<PlatformPackageRecord> Platforms { get; }
    bool HasProject { get; }
    bool HasPackage { get; }
    PackageExportOptions CreateExportOptions();
}
