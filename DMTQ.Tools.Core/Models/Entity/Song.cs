using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>
/// Project-domain song model with flat, strongly-typed fields.
/// Used for editing in the UI; not tied to CSV column layout.
/// </summary>
public sealed class Song
{
    [JsonInclude]
    public required int Id { get; init; }

    // ── song_song fields ──
    public int ItemId { get; set; }
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
    public int CostGamePoint { get; set; }
    public int CostGameCash { get; set; }
    public int Flag { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FreeYn { get; set; } = string.Empty;
    public string HiddenYn { get; set; } = string.Empty;
    public string OpenYn { get; set; } = string.Empty;
    public int TrackId { get; set; }
    public string ModDate { get; set; } = string.Empty;
    public string Update { get; set; } = string.Empty;

    /// <summary>Preview resource path (from preview column or inferred).</summary>
    public string? PreviewPackageRelativePath { get; set; }

    // ── Localized metadata overrides (from song_desc_&lt;lang&gt;) ──
    /// <summary>Per‑language metadata overrides (CN, JP, KR, TW, US).
    /// If a field is blank, the export falls back to the Basic Info value.</summary>
    private Dictionary<string, SongLocalization> _localizations = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SongLocalization> Localizations
    {
        get => _localizations;
        set => _localizations = value is not null
            ? new Dictionary<string, SongLocalization>(value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    // ── Patterns ──
    public List<SongPattern> Patterns { get; set; } = [];

    // ── Computed ──
    [JsonIgnore]
    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewPackageRelativePath);

    [SetsRequiredMembers]
    public Song() { Id = 0; }
}

/// <summary>
/// Project-domain pattern model with flat, strongly-typed fields.
/// </summary>
public sealed class SongPattern
{
    public required int PatternId { get; set; }
    [JsonInclude]
    public required int SongId { get; init; }

    [SetsRequiredMembers]
    public SongPattern() { SongId = 0; PatternId = 0; }

    // ── song_songPattern fields ──
    public string Name { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Signature { get; set; }
    public int Difficulty { get; set; }
    public int PointType { get; set; }
    public int PointValue { get; set; }
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
    public int SongId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string ComposedBy { get; set; } = string.Empty;
    public string Singer { get; set; } = string.Empty;
    public string FeatBy { get; set; } = string.Empty;
    public string ArrangedBy { get; set; } = string.Empty;
    public string VisualizedBy { get; set; } = string.Empty;
}
