namespace DMTQ.Tools.Core.Models;

public sealed class LogicalTableRow
{
    public required string Key { get; init; }
    public string SourceKeyValue { get; init; } = string.Empty;
    public int SourceKeyOccurrence { get; init; }
    public Dictionary<string, string> Cells { get; } = new(StringComparer.OrdinalIgnoreCase);
}
