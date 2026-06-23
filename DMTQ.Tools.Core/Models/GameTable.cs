using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

public sealed class GameTable
{
    [JsonInclude]
    public required string PackageRelativePath { get; init; }
    [JsonInclude]
    public required string TableName { get; init; }
    public string? LanguageCode { get; init; }
    public List<GameTableColumn> Columns { get; set; } = [];
    public List<GameTableRow> Rows { get; set; } = [];

    [SetsRequiredMembers]
    public GameTable() { PackageRelativePath = ""; TableName = ""; }
}
