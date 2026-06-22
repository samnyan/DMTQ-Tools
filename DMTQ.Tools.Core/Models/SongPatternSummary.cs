namespace DMTQ.Tools.Core.Models;

public sealed class SongPatternSummary
{
    public required string PatternId { get; init; }
    public required string SongId { get; init; }
    public string PatternName { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public Dictionary<string, string> SourceFields { get; } = new(StringComparer.OrdinalIgnoreCase);
}
