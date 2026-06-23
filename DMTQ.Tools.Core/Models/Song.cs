using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models;

/// <summary>
/// Project-domain song model with flat, strongly-typed fields.
/// Used for editing in the UI; not tied to CSV column layout.
/// </summary>
public sealed class Song
{
    [JsonInclude]
    public required string Id { get; init; }

    // ── song_song fields ──
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string OriginalBgaYn { get; set; } = string.Empty;
    public string LoopBgaYn { get; set; } = string.Empty;
    public string ComposedBy { get; set; } = string.Empty;
    public string Singer { get; set; } = string.Empty;
    public string FeatBy { get; set; } = string.Empty;
    public string ArrangedBy { get; set; } = string.Empty;
    public string VisualizedBy { get; set; } = string.Empty;
    public string CostGamePoint { get; set; } = string.Empty;
    public string CostGameCash { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FreeYn { get; set; } = string.Empty;
    public string HiddenYn { get; set; } = string.Empty;
    public string OpenYn { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string ModDate { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;

    /// <summary>Preview resource path (from preview column or inferred).</summary>
    public string? PreviewPackageRelativePath { get; set; }

    // ── Localized fields (from song_desc_&lt;lang&gt;, item_desc_&lt;lang&gt;) ──
    public Dictionary<string, string> TitlesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ItemNamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Relationships ──
    public List<string> ProductIds { get; set; } = [];
    public List<string> ItemIds { get; set; } = [];
    public List<string> CategoryIds { get; set; } = [];

    /// <summary>Per‑language metadata overrides (CN, JP, KR, TW, US).
    /// If a field is blank, the export falls back to the Basic Info value.</summary>
    public Dictionary<string, SongLocalization> Localizations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Patterns ──
    public List<SongPattern> Patterns { get; set; } = [];

    // ── Computed ──
    [JsonIgnore]
    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewPackageRelativePath);

    [SetsRequiredMembers]
    public Song() { Id = ""; }

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
/// Project-domain pattern model with flat, strongly-typed fields.
/// </summary>
public sealed class SongPattern
{
    public required string PatternId { get; set; }
    [JsonInclude]
    public required string SongId { get; init; }

    [SetsRequiredMembers]
    public SongPattern() { SongId = ""; PatternId = ""; }

    // ── song_songPattern fields ──
    public string Name { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string PointType { get; set; } = string.Empty;
    public string PointValue { get; set; } = string.Empty;
    public string Flg { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;
}

/// <summary>
/// Per-language metadata override for a song.  When fields are blank,
/// the export falls back to the song's basic‑info values.
/// </summary>
public sealed class SongLocalization
{
    /// <summary>The song this localization belongs to.</summary>
    public string SongId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string ComposedBy { get; set; } = string.Empty;
    public string Singer { get; set; } = string.Empty;
    public string FeatBy { get; set; } = string.Empty;
    public string ArrangedBy { get; set; } = string.Empty;
    public string VisualizedBy { get; set; } = string.Empty;
}
