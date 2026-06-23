using System.Reflection;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>CSV schema for the song_songPattern table.</summary>
public sealed class PatternCsvSchema : CsvSchema<SongPattern>
{
    public override string TableName => "song_songPattern";

    private static readonly PropertyInfo SongIdProperty =
        typeof(SongPattern).GetProperty("SongId", BindingFlags.Public | BindingFlags.Instance)!;

    public override IReadOnlyList<CsvColumn<SongPattern>> Columns { get; } =
    [
        new CsvColumn<SongPattern>("pattern_id",  0, sp => sp.PatternId,   (sp, v) => sp.PatternId = v),
        new CsvColumn<SongPattern>("song_id",     1, sp => sp.SongId,      (sp, v) => SongIdProperty.SetValue(sp, v)),
        new CsvColumn<SongPattern>("signature",   2, sp => sp.Signature,   (sp, v) => sp.Signature = v),
        new CsvColumn<SongPattern>("line",        3, sp => sp.Line,        (sp, v) => sp.Line = v),
        new CsvColumn<SongPattern>("difficulty",  4, sp => sp.Difficulty,  (sp, v) => sp.Difficulty = v),
        new CsvColumn<SongPattern>("point_type",  5, sp => sp.PointType,   (sp, v) => sp.PointType = v),
        new CsvColumn<SongPattern>("point_value", 6, sp => sp.PointValue,  (sp, v) => sp.PointValue = v),
        new CsvColumn<SongPattern>("flg",         7, sp => sp.Flg,         (sp, v) => sp.Flg = v),
        new CsvColumn<SongPattern>("update",      8, sp => sp.Update,      (sp, v) => sp.Update = v),
    ];
}
