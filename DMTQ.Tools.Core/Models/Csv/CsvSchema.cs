using System.Globalization;
using System.Reflection;
using CsvHelper;
using CsvHelper.Configuration;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>
/// Base class for CSV schemas that produce entities of type <typeparamref name="T"/>.
/// Subclasses define column mappings via the <see cref="Columns"/> property.
/// </summary>
public abstract class CsvSchema<T>
{
    public abstract string TableName { get; }
    public virtual string? LanguageCode => null;
    public abstract IReadOnlyList<CsvColumn<T>> Columns { get; }

    /// <summary>
    /// Called after all column setters have been applied to a newly read entity.
    /// Override to perform post-processing such as computing composite keys.
    /// </summary>
    protected virtual void OnAfterRead(T entity) { }

    public List<T> ReadCsv(Stream stream, bool throwOnMissingColumn = true)
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

        // Build ordered list of (CsvColumn, csvFieldIndex) for each column we care about.
        // When throwOnMissingColumn is false, missing columns are silently skipped (their
        // setters are never called) so that partial tables from different platforms import
        // without errors.
        var columnMappings = Columns
            .OrderBy(c => c.Order)
            .Select(c =>
            {
                if (!headerIndexMap.TryGetValue(c.ColumnName, out var csvIndex))
                {
                    if (throwOnMissingColumn)
                    {
                        throw new InvalidDataException(
                            $"CSV table '{TableName}' is missing column '{c.ColumnName}'.");
                    }
                    return (Column: (CsvColumn<T>?)null, CsvIndex: -1);
                }
                return (Column: (CsvColumn<T>?)c, CsvIndex: csvIndex);
            })
            .Where(cm => cm.Column is not null)
            .Select(cm => (Column: cm.Column!, CsvIndex: cm.CsvIndex))
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
            var entity = Activator.CreateInstance<T>();

            foreach (var (column, csvIndex) in columnMappings)
            {
                var value = csv.GetField(csvIndex) ?? string.Empty;
                column.Setter(entity, value);
            }

            OnAfterRead(entity);

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

/// <summary>
/// Base class for CSV schemas that mutate existing entities in a lookup dictionary
/// rather than producing new entity instances. Used for localized description tables
/// and join tables that enrich previously read entities.
/// </summary>
public abstract class CsvLookupSchema<TTarget>
{
    public abstract string TableName { get; }
    public virtual string? LanguageCode => null;

    /// <summary>
    /// Reads CSV rows from <paramref name="stream"/> and applies each row
    /// to the <paramref name="lookup"/> dictionary via <see cref="ApplyRow"/>.
    /// </summary>
    public void ReadCsv(Stream stream, Dictionary<string, TTarget> lookup)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(lookup);

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

        var rowIndex = 0;
        while (csv.Read())
        {
            var fields = new Dictionary<string, string>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                fields[headers[i]] = csv.GetField(i) ?? string.Empty;
            }

            ApplyRow(lookup, fields, rowIndex++);
        }
    }

    /// <summary>
    /// Called for each CSV row. Implementations look up the target entity
    /// from <paramref name="lookup"/> (or create it) and apply the field values.
    /// </summary>
    /// <param name="lookup">The entity dictionary keyed by entity Id.</param>
    /// <param name="fields">Field values keyed by header name (case-insensitive).</param>
    /// <param name="rowIndex">Zero-based row index within the CSV.</param>
    protected abstract void ApplyRow(
        Dictionary<string, TTarget> lookup,
        IReadOnlyDictionary<string, string> fields,
        int rowIndex);
}
