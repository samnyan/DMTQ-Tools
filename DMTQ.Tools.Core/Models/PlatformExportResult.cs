namespace DMTQ.Tools.Core.Models;

public sealed class PlatformExportResult
{
    public required string Platform { get; init; }
    public required string ExportRoot { get; init; }
    public PatchManifest Manifest { get; } = new();
    public PatchValidationResult Validation { get; } = new();
    public int ManifestEntryCount => Manifest.Entries.Count;
    public int FilesWritten { get; set; }
    public int FilesSkippedAsBaseline { get; set; }
    public int MissingCurrentFiles { get; set; }
    public List<string> Messages { get; } = [];
}
