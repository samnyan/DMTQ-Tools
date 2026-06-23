using System.Reflection;

using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the song_songPattern table.</summary>
public sealed class PatternCsvSchema : CsvSchema<SongPattern>
{
    public override string TableName => "song_songPattern";

    private static readonly PropertyInfo SongIdProperty =
        typeof(SongPattern).GetProperty("SongId", BindingFlags.Public | BindingFlags.Instance)!;

    private static int ParseInt(string v) => int.TryParse(v, out var n) ? n : 0;

    public override IReadOnlyList<CsvColumn<SongPattern>> Columns { get; } =
    [
        new CsvColumn<SongPattern>("pattern_id",  0, sp => sp.PatternId.ToString(),   (sp, v) => sp.PatternId = ParseInt(v)),
        new CsvColumn<SongPattern>("song_id",     1, sp => sp.SongId.ToString(),      (sp, v) => SongIdProperty.SetValue(sp, ParseInt(v))),
        new CsvColumn<SongPattern>("signature",   2, sp => sp.Signature.ToString(),   (sp, v) => sp.Signature = ParseInt(v)),
        new CsvColumn<SongPattern>("line",        3, sp => sp.Line.ToString(),        (sp, v) => sp.Line = ParseInt(v)),
        new CsvColumn<SongPattern>("difficulty",  4, sp => sp.Difficulty.ToString(),  (sp, v) => sp.Difficulty = ParseInt(v)),
        new CsvColumn<SongPattern>("point_type",  5, sp => sp.PointType.ToString(),   (sp, v) => sp.PointType = ParseInt(v)),
        new CsvColumn<SongPattern>("point_value", 6, sp => sp.PointValue.ToString(),  (sp, v) => sp.PointValue = ParseInt(v)),
        new CsvColumn<SongPattern>("flg",         7, sp => sp.Flg,                    (sp, v) => sp.Flg = v),
        new CsvColumn<SongPattern>("update",      8, sp => sp.Update,                 (sp, v) => sp.Update = v),
    ];
}
