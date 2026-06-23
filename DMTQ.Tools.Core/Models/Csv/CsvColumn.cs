namespace DMTQ.Tools.Core.Models.Csv;

public sealed record CsvColumn<T>(
    string ColumnName,
    int Order,
    Func<T, string> Getter,
    Action<T, string> Setter
);
