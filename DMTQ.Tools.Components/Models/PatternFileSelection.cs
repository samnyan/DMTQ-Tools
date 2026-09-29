namespace DMTQ_Tools.Components.Models;

/// <summary>Bytes selected for import alongside a new song pattern row.</summary>
public sealed class PatternFileSelection
{
    public string? FileName { get; set; }
    public byte[]? Content { get; set; }
    public bool IsHeadphone { get; set; }
}
