namespace DMTQ.Tools.Core.Models.Project;

/// <summary>Summary of one platform's InstallPack import attempt.</summary>
public sealed class InstallPackImportResult
{
    public int ImportedFiles { get; set; }
    public int SkippedEntries { get; set; }
    public List<string> Errors { get; } = [];
}
