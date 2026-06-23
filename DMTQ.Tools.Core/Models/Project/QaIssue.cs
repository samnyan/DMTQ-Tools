namespace DMTQ.Tools.Core.Models.Project;

public sealed class QaIssue
{
    public required string Category { get; init; }
    public required QaIssueSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
}
