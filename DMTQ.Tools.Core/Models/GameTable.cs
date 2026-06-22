namespace DMTQ.Tools.Core.Models;

public sealed class GameTable
{
    public required string PackageRelativePath { get; init; }
    public required string TableName { get; init; }
    public string? LanguageCode { get; init; }
    public List<GameTableColumn> Columns { get; } = [];
    public List<GameTableRow> Rows { get; } = [];
}
