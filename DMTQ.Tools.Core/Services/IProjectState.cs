using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

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
    bool HasProject { get; }
    bool HasPackage { get; }
    bool IsDirty { get; set; }
    IReadOnlyList<string> ImportIntegrityErrors { get; }
    PackageExportOptions CreateExportOptions();

    /// <summary>
    /// Raised after any state mutation so Blazor layout can re-render.
    /// </summary>
    event Action? StateChanged;
}
