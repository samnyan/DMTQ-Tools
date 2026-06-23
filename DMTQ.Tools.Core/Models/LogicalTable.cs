namespace DMTQ.Tools.Core.Models;

public sealed class LogicalTable
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Kind { get; init; }
    public List<string> SourcePackageRelativePaths { get; } = [];
    public List<string> Languages { get; } = [];
    public List<LogicalTableColumn> Columns { get; } = [];
    public List<LogicalTableRow> Rows { get; } = [];
}
