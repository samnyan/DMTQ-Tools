using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the song_desc_&lt;lang&gt; localized tables.</summary>
public sealed class SongDescCsvSchema : CsvSchema<SongLocalization>
{
    public override string TableName => "song_desc";

    private readonly string _languageCode;

    public SongDescCsvSchema(string languageCode)
    {
        _languageCode = languageCode;
    }

    public override string? LanguageCode => _languageCode;

    private static int ParseInt(string v) => int.TryParse(v, out var n) ? n : 0;

    public override IReadOnlyList<CsvColumn<SongLocalization>> Columns { get; } =
    [
        new CsvColumn<SongLocalization>("song_id",        0, sl => sl.SongId.ToString(), (sl, v) => sl.SongId = ParseInt(v)),
        new CsvColumn<SongLocalization>("fullname",       1, sl => sl.FullName,          (sl, v) => sl.FullName = v),
        new CsvColumn<SongLocalization>("genre",          2, sl => sl.Genre,             (sl, v) => sl.Genre = v),
        new CsvColumn<SongLocalization>("artist",         3, sl => sl.ArtistName,        (sl, v) => sl.ArtistName = v),
        new CsvColumn<SongLocalization>("composed_by",    4, sl => sl.ComposedBy,        (sl, v) => sl.ComposedBy = v),
        new CsvColumn<SongLocalization>("singer",         5, sl => sl.Singer,            (sl, v) => sl.Singer = v),
        new CsvColumn<SongLocalization>("feat_by",        6, sl => sl.FeatBy,            (sl, v) => sl.FeatBy = v),
        new CsvColumn<SongLocalization>("arranged_by",    7, sl => sl.ArrangedBy,        (sl, v) => sl.ArrangedBy = v),
        new CsvColumn<SongLocalization>("visualized_by",  8, sl => sl.VisualizedBy,      (sl, v) => sl.VisualizedBy = v),
    ];
}
