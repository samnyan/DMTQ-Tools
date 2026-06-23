using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

public sealed class GameTableRow
{
    [JsonInclude]
    public required int Order { get; init; }
    public List<GameTableCell> Cells { get; set; } = [];

    [SetsRequiredMembers]
    public GameTableRow() { Order = 0; }
}
