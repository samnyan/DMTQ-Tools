namespace DMTQ.Tools.Core.Models.Project;

public sealed class QaReport
{
    public List<QaIssue> Issues { get; } = [];
    public int ErrorCount => Issues.Count(issue => issue.Severity == QaIssueSeverity.Error);
    public int WarningCount => Issues.Count(issue => issue.Severity == QaIssueSeverity.Warning);
    public int InfoCount => Issues.Count(issue => issue.Severity == QaIssueSeverity.Info);
    public bool IsClean => Issues.Count == 0;
    public bool HasErrors => ErrorCount > 0;
    public string Summary => Issues.Count == 0
        ? "No issues found."
        : $"{ErrorCount} error(s), {WarningCount} warning(s), {InfoCount} info(s).";
}
