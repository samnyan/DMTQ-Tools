namespace DMTQ.Tools.Core.Models;

public sealed class AddSongRequest
{
    public required string SongId { get; init; }
    public required string ProductId { get; init; }
    public required string ItemId { get; init; }
    public required string CategoryId { get; init; }
    public Dictionary<string, string> SongFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> TitlesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ItemNamesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ItemDescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<AddSongPatternRequest> Patterns { get; } = [];
    public string? PreviewPackageRelativePath { get; init; }
}
