namespace DMTQ.Tools.Core.Models;

public sealed class SongEditRequest
{
    public required string SongId { get; init; }
    public Dictionary<string, string> SourceFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> TitlesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PatternDifficultyByPatternId { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PatternLevelByPatternId { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string>> PatternFieldsByPatternId { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PreviewPackageRelativePath { get; init; }
}
