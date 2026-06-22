namespace DMTQ.Tools.Core.Models;

public sealed class AddSongPatternRequest
{
    public required string PatternId { get; init; }
    public string PatternName { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
}
