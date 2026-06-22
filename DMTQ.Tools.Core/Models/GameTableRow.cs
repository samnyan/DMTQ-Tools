namespace DMTQ.Tools.Core.Models;

public sealed class GameTableRow
{
    public required int Order { get; init; }
    public List<GameTableCell> Cells { get; } = [];
}
