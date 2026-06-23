using System.Globalization;
using System.Reflection;
using CsvHelper;
using CsvHelper.Configuration;

namespace DMTQ.Tools.Core.Models.Csv;

public abstract class CsvSchema<T> where T : new()
{
    public abstract string TableName { get; }
    public virtual string? LanguageCode => null;
    public abstract IReadOnlyList<CsvColumn<T>> Columns { get; }

    public List<T> ReadCsv(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null
        });

        if (!csv.Read())
        {
            throw new InvalidDataException($"CSV table '{TableName}' is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord
            ?? throw new InvalidDataException($"CSV table '{TableName}' has no header.");

        // Build a map from header name (case-insensitive) to column index in the CSV
        var headerIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            headerIndexMap[headers[i]] = i;
        }

        // Build ordered list of (CsvColumn, csvFieldIndex) for each column we care about
        var columnMappings = Columns
            .OrderBy(c => c.Order)
            .Select(c =>
            {
                if (!headerIndexMap.TryGetValue(c.ColumnName, out var csvIndex))
                {
                    throw new InvalidDataException(
                        $"CSV table '{TableName}' is missing column '{c.ColumnName}'.");
                }
                return (Column: c, CsvIndex: csvIndex);
            })
            .ToList();

        // Detect an Id property on T for deduplication
        var idProperty = typeof(T).GetProperty("Id",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        var results = new List<T>();
        var seenIds = idProperty is not null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : null;

        while (csv.Read())
        {
            var entity = new T();

            foreach (var (column, csvIndex) in columnMappings)
            {
                var value = csv.GetField(csvIndex) ?? string.Empty;
                column.Setter(entity, value);
            }

            if (idProperty is not null && seenIds is not null)
            {
                var idValue = idProperty.GetValue(entity)?.ToString();
                if (idValue is not null && !seenIds.Add(idValue))
                {
                    // Duplicate Id, skip this row
                    continue;
                }
            }

            results.Add(entity);
        }

        return results;
    }

    public void WriteCsv(Stream stream, IEnumerable<T> entities)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(entities);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        var orderedColumns = Columns.OrderBy(c => c.Order).ToList();

        // Write header
        foreach (var column in orderedColumns)
        {
            csv.WriteField(column.ColumnName);
        }

        csv.NextRecord();

        // Write rows
        foreach (var entity in entities)
        {
            foreach (var column in orderedColumns)
            {
                csv.WriteField(column.Getter(entity));
            }

            csv.NextRecord();
        }

        writer.Flush();
    }
}
