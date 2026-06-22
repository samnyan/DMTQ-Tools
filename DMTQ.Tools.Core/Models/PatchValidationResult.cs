namespace DMTQ.Tools.Core.Models;

public sealed class PatchValidationResult
{
    public List<string> Errors { get; } = [];
    public bool IsValid => Errors.Count == 0;
}
