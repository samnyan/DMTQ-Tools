namespace DMTQ.Tools.Core.Models;

/// <summary>
/// Unified song model used for both reading (projection from tables)
/// and writing (editing back into tables).
/// </summary>
public sealed class Song
{
    public required string Id { get; init; }

    /// <summary>All cells from song_song rows, keyed by column name.</summary>
    public Dictionary<string, string> SourceFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Title by language code (from song_desc_&lt;lang&gt;).</summary>
    public Dictionary<string, string> TitlesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Description by language code (from song_desc_&lt;lang&gt;).</summary>
    public Dictionary<string, string> DescriptionsByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Item name by language code (from item_desc_&lt;lang&gt;).</summary>
    public Dictionary<string, string> ItemNamesByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Preview resource path, or null.</summary>
    public string? PreviewPackageRelativePath { get; set; }

    /// <summary>Product IDs linked to this song via product_product.</summary>
    public List<string> ProductIds { get; } = [];

    /// <summary>Item IDs linked to this song via product_item.</summary>
    public List<string> ItemIds { get; } = [];

    /// <summary>Category IDs linked to this song via category_categoryproduct.</summary>
    public List<string> CategoryIds { get; } = [];

    /// <summary>Patterns linked to this song via song_songPattern.</summary>
    public List<SongPattern> Patterns { get; } = [];

    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewPackageRelativePath);

    /// <summary>Get title for a language, with fallback.</summary>
    public string GetTitle(string language)
    {
        if (TitlesByLanguage.TryGetValue(language, out var title) && !string.IsNullOrWhiteSpace(title))
            return title;
        if (TitlesByLanguage.TryGetValue("us", out var us) && !string.IsNullOrWhiteSpace(us))
            return us;
        if (TitlesByLanguage.TryGetValue("cn", out var cn) && !string.IsNullOrWhiteSpace(cn))
            return cn;
        return TitlesByLanguage.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? Id;
    }

    public string GetDescription(string language)
    {
        if (DescriptionsByLanguage.TryGetValue(language, out var desc) && !string.IsNullOrWhiteSpace(desc))
            return desc;
        return DescriptionsByLanguage.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    public string GetItemName(string language)
    {
        if (ItemNamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        return ItemNamesByLanguage.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}

/// <summary>
/// A pattern (chart) belonging to a song, sourced from song_songPattern.
/// </summary>
public sealed class SongPattern
{
    public required string PatternId { get; init; }
    public required string SongId { get; init; }

    /// <summary>All cells from song_songPattern rows for this pattern.</summary>
    public Dictionary<string, string> SourceFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Convenience accessors for commonly-used columns.
    public string Name => GetField("name", "pattern_name");
    public string Difficulty => GetField("difficulty", "difficulty_type", "diff");
    public string Level => GetField("level", "level_text", "rating");
    public string Line => GetField("line");
    public string Signature => GetField("signature", "sig");

    private string GetField(params string[] columnNames)
    {
        foreach (var name in columnNames)
        {
            if (SourceFields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }
}
