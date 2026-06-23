namespace DMTQ.Tools.Components.Models;

public sealed class ResourceDialogData
{
    public bool IsNew { get; set; }
    public string FileName { get; set; } = "";
    public string Category { get; set; } = "dlc";
    public bool Compressed { get; set; }
    public List<PlatformCardData> Platforms { get; set; } = [];
}

public sealed class PlatformCardData
{
    public string Platform { get; set; } = "";
    public bool Exist { get; set; }
    public long SourceFileSize { get; set; }
    public string SourceChecksum { get; set; } = "";
    public string? LocalChecksum { get; set; }
    public string? PendingFilePath { get; set; }
    public bool IsNew { get; set; }
    public bool MarkedForDeletion { get; set; }
}
