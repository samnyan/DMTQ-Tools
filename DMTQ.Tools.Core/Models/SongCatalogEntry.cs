namespace DMTQ.Tools.Core.Models;

public sealed class SongCatalogEntry
{
    public required string SongId { get; init; }
    public Dictionary<string, string> SourceFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> TitlesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ItemNamesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SongPatternSummary> Patterns { get; } = [];
    public List<string> ProductIds { get; } = [];
    public List<string> ItemIds { get; } = [];
    public List<string> CategoryIds { get; } = [];
    public string? PreviewPackageRelativePath { get; set; }
    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewPackageRelativePath);

    public string GetTitle(string preferredLanguage)
        => GetLocalizedValue(TitlesByLanguage, preferredLanguage);

    public string GetDescription(string preferredLanguage)
        => GetLocalizedValue(DescriptionsByLanguage, preferredLanguage);

    public string GetItemName(string preferredLanguage)
        => GetLocalizedValue(ItemNamesByLanguage, preferredLanguage);

    private static string GetLocalizedValue(Dictionary<string, string> values, string preferredLanguage)
    {
        if (values.TryGetValue(preferredLanguage, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (values.TryGetValue("us", out var usValue) && !string.IsNullOrWhiteSpace(usValue))
        {
            return usValue;
        }

        if (values.TryGetValue("cn", out var cnValue) && !string.IsNullOrWhiteSpace(cnValue))
        {
            return cnValue;
        }

        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
