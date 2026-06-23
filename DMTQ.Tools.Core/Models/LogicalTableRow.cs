namespace DMTQ.Tools.Core.Models;

public sealed class LogicalTableRow
{
    public required string Key { get; init; }
    public Dictionary<string, string> Cells { get; } = new(StringComparer.OrdinalIgnoreCase);
}
