using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the song_song table.</summary>
public sealed class SongCsvSchema : CsvSchema<Song>
{
    public override string TableName => "song_song";

    private static readonly PropertyInfo IdProperty =
        typeof(Song).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;

    private static int ParseInt(string v) => int.TryParse(v, out var n) ? n : 0;

    public override IReadOnlyList<CsvColumn<Song>> Columns { get; } =
    [
        // Order 0–22 matching song_song CSV column order
        new CsvColumn<Song>("song_id",           0,  s => s.Id.ToString(),               (s, v) => IdProperty.SetValue(s, ParseInt(v))),
        new CsvColumn<Song>("item_id",           1,  s => s.ItemId.ToString(),           (s, v) => s.ItemId = ParseInt(v)),
        new CsvColumn<Song>("name",              2,  s => s.Name,                        (s, v) => s.Name = v),
        new CsvColumn<Song>("full_name",         3,  s => s.FullName,                    (s, v) => s.FullName = v),
        new CsvColumn<Song>("genre",             4,  s => s.Genre,                       (s, v) => s.Genre = v),
        new CsvColumn<Song>("artist_name",       5,  s => s.ArtistName,                  (s, v) => s.ArtistName = v),
        new CsvColumn<Song>("original_bga_yn",   6,  s => s.OriginalBgaYn,               (s, v) => s.OriginalBgaYn = v),
        new CsvColumn<Song>("loop_bga_yn",       7,  s => s.LoopBgaYn,                   (s, v) => s.LoopBgaYn = v),
        new CsvColumn<Song>("composed_by",       8,  s => s.ComposedBy,                  (s, v) => s.ComposedBy = v),
        new CsvColumn<Song>("singer",            9,  s => s.Singer,                      (s, v) => s.Singer = v),
        new CsvColumn<Song>("feat_by",           10, s => s.FeatBy,                      (s, v) => s.FeatBy = v),
        new CsvColumn<Song>("arranged_by",       11, s => s.ArrangedBy,                  (s, v) => s.ArrangedBy = v),
        new CsvColumn<Song>("visualized_by",     12, s => s.VisualizedBy,                (s, v) => s.VisualizedBy = v),
        new CsvColumn<Song>("cost_game_point",   13, s => s.CostGamePoint.ToString(),    (s, v) => s.CostGamePoint = ParseInt(v)),
        new CsvColumn<Song>("cost_game_cash",    14, s => s.CostGameCash.ToString(),     (s, v) => s.CostGameCash = ParseInt(v)),
        new CsvColumn<Song>("flag",              15, s => s.Flag.ToString(),             (s, v) => s.Flag = ParseInt(v)),
        new CsvColumn<Song>("status",            16, s => s.Status,                      (s, v) => s.Status = v),
        new CsvColumn<Song>("free_yn",           17, s => s.FreeYn,                      (s, v) => s.FreeYn = v),
        new CsvColumn<Song>("hidden_yn",         18, s => s.HiddenYn,                    (s, v) => s.HiddenYn = v),
        new CsvColumn<Song>("open_yn",           19, s => s.OpenYn,                      (s, v) => s.OpenYn = v),
        new CsvColumn<Song>("track_id",          20, s => s.TrackId.ToString(),          (s, v) => s.TrackId = ParseInt(v)),
        new CsvColumn<Song>("mod_date",          21, s => s.ModDate,                     (s, v) => s.ModDate = v),
        new CsvColumn<Song>("update",            22, s => s.Update,                      (s, v) => s.Update = v),
    ];
}
