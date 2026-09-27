using System.Text.RegularExpressions;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed partial class LogicalTableService
{
    public IReadOnlyList<LogicalTable> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var tables = package.Tables.Tables
            .OrderBy(table => table.PackageRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var localizedGroups = tables
            .Select(table => new { Table = table, Match = LocalizedTableNameRegex().Match(table.TableName) })
            .Where(item => item.Match.Success)
            .GroupBy(item => item.Match.Groups["base"].Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildLocalizedTable(group.Key, group.Select(item => item.Table).ToArray()));

        var localizedPaths = tables
            .Where(table => LocalizedTableNameRegex().IsMatch(table.TableName))
            .Select(table => table.PackageRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sharedGroups = tables
            .Where(table => !localizedPaths.Contains(table.PackageRelativePath))
            .GroupBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildSharedTable(group.Key, group.ToArray()));

        return sharedGroups.Concat(localizedGroups)
            .OrderBy(table => table.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LogicalTable BuildSharedTable(string key, IReadOnlyList<GameTable> sourceTables)
    {
        var primary = sourceTables
            .OrderBy(table => table.LanguageCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .First();
        var keyColumn = primary.Columns.OrderBy(column => column.Order).First();
        var logicalTable = new LogicalTable
        {
            Key = key,
            DisplayName = key,
            Kind = "Shared"
        };
        logicalTable.SourcePackageRelativePaths.AddRange(sourceTables.Select(table => table.PackageRelativePath));
        logicalTable.Languages.AddRange(sourceTables.Select(table => table.LanguageCode)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase));

        foreach (var column in primary.Columns.OrderBy(column => column.Order))
        {
            logicalTable.Columns.Add(new LogicalTableColumn(column.Name, column.Name, null, column.Name, Editable: true));
        }

        var keyOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in primary.Rows.OrderBy(row => row.Order))
        {
            var rowKey = GetCellValue(row, keyColumn.Name);
            var occurrence = keyOccurrences.GetValueOrDefault(rowKey);
            keyOccurrences[rowKey] = occurrence + 1;
            var logicalRow = new LogicalTableRow
            {
                Key = occurrence == 0 ? rowKey : $"{rowKey} (duplicate {occurrence + 1})",
                SourceKeyValue = rowKey,
                SourceKeyOccurrence = occurrence
            };
            foreach (var column in primary.Columns.OrderBy(column => column.Order))
            {
                logicalRow.Cells[column.Name] = GetCellValue(row, column.Name);
            }

            logicalTable.Rows.Add(logicalRow);
        }

        return logicalTable;
    }

    private static LogicalTable BuildLocalizedTable(string key, IReadOnlyList<GameTable> sourceTables)
    {
        var orderedTables = sourceTables
            .OrderBy(table => table.LanguageCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var primary = orderedTables.First();
        var keyColumn = primary.Columns.OrderBy(column => column.Order).First();
        var valueColumns = primary.Columns
            .OrderBy(column => column.Order)
            .Skip(1)
            .ToArray();

        var logicalTable = new LogicalTable
        {
            Key = key,
            DisplayName = key,
            Kind = "Localized"
        };
        logicalTable.SourcePackageRelativePaths.AddRange(orderedTables.Select(table => table.PackageRelativePath));
        logicalTable.Languages.AddRange(orderedTables.Select(table => table.LanguageCode)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase));

        logicalTable.Columns.Add(new LogicalTableColumn(keyColumn.Name, keyColumn.Name, null, keyColumn.Name, Editable: false));
        foreach (var column in valueColumns)
        {
            foreach (var language in logicalTable.Languages)
            {
                logicalTable.Columns.Add(new LogicalTableColumn(
                    $"{column.Name}:{language}",
                    $"{column.Name} ({language})",
                    language,
                    column.Name,
                    Editable: true));
            }
        }

        var rowKeys = orderedTables
            .SelectMany(table => table.Rows.Select(row => GetCellValue(row, keyColumn.Name)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        foreach (var rowKey in rowKeys)
        {
            var logicalRow = new LogicalTableRow { Key = rowKey, SourceKeyValue = rowKey };
            logicalRow.Cells[keyColumn.Name] = rowKey;

            foreach (var table in orderedTables)
            {
                var language = table.LanguageCode ?? string.Empty;
                var sourceRow = table.Rows.FirstOrDefault(row =>
                    GetCellValue(row, keyColumn.Name).Equals(rowKey, StringComparison.OrdinalIgnoreCase));
                foreach (var column in valueColumns)
                {
                    logicalRow.Cells[$"{column.Name}:{language}"] = sourceRow is null
                        ? string.Empty
                        : GetCellValue(sourceRow, column.Name);
                }
            }

            logicalTable.Rows.Add(logicalRow);
        }

        return logicalTable;
    }

    private static string GetCellValue(GameTableRow row, string columnName)
        => row.Cells.FirstOrDefault(cell => cell.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    [GeneratedRegex("^(?<base>.+_desc)_(?<lang>[^_]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalizedTableNameRegex();

    [GeneratedRegex("^(?<column>.+):(?<lang>[^:]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalizedColumnKeyRegex();

    public void UpdateCell(
        PatchPackage package,
        string logicalTableKey,
        string rowKey,
        string columnKey,
        string value)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalTableKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnKey);

        var localizedMatch = LocalizedColumnKeyRegex().Match(columnKey);
        if (localizedMatch.Success)
        {
            var sourceColumn = localizedMatch.Groups["column"].Value;
            var language = localizedMatch.Groups["lang"].Value;
            var sourceTable = package.Tables.Tables.FirstOrDefault(table =>
                table.TableName.Equals($"{logicalTableKey}_{language}", StringComparison.OrdinalIgnoreCase)
                || (table.TableName.StartsWith(logicalTableKey + "_", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(table.LanguageCode, language, StringComparison.OrdinalIgnoreCase)));
            if (sourceTable is null)
            {
                return;
            }

            UpdateSourceTableCell(sourceTable, rowKey, sourceColumn, value);
            return;
        }

        var sourceTables = package.Tables.Tables
            .Where(table => table.TableName.Equals(logicalTableKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var table in sourceTables)
        {
            UpdateSourceTableCell(table, rowKey, columnKey, value);
        }
    }

    /// <summary>Saves one row from the generic editor without changing its CSV column order.</summary>
    public void SaveRow(
        PatchPackage package,
        LogicalTable logicalTable,
        LogicalTableRow? originalRow,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(logicalTable);
        ArgumentNullException.ThrowIfNull(values);

        var keyColumn = logicalTable.Columns.FirstOrDefault()
            ?? throw new InvalidOperationException($"Table '{logicalTable.Key}' has no columns.");
        if (!values.TryGetValue(keyColumn.Key, out var rowKey) || string.IsNullOrWhiteSpace(rowKey))
            throw new InvalidDataException($"Column '{keyColumn.DisplayName}' is required.");

        var sourceTables = package.Tables.Tables
            .Where(table => logicalTable.SourcePackageRelativePaths.Contains(
                table.PackageRelativePath, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (sourceTables.Length == 0)
            throw new InvalidOperationException($"Source tables for '{logicalTable.Key}' no longer exist.");

        var originalRowKey = originalRow?.Key;
        var keyColumnHasUniqueId = keyColumn.SourceColumnName.Equals("id", StringComparison.OrdinalIgnoreCase)
                                   || keyColumn.SourceColumnName.EndsWith("_id", StringComparison.OrdinalIgnoreCase);
        if (keyColumnHasUniqueId && logicalTable.Rows.Any(row =>
                !row.Key.Equals(originalRowKey, StringComparison.OrdinalIgnoreCase)
                && row.Cells.GetValueOrDefault(keyColumn.Key, string.Empty).Equals(rowKey, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Row key '{rowKey}' already exists in '{logicalTable.Key}'.");

        foreach (var table in sourceTables)
        {
            var sourceKeyColumn = table.Columns.OrderBy(column => column.Order).FirstOrDefault()
                ?? throw new InvalidOperationException($"Source table '{table.TableName}' has no columns.");
            GameTableRow? row = null;
            if (originalRow is not null)
            {
                var matchingRows = table.Rows
                    .Where(candidate => GetCellValue(candidate, sourceKeyColumn.Name)
                        .Equals(originalRow.SourceKeyValue, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(candidate => candidate.Order)
                    .ToArray();
                row = matchingRows.ElementAtOrDefault(originalRow.SourceKeyOccurrence);
            }

            if (row is null)
            {
                if (originalRow is not null && logicalTable.Kind == "Shared")
                    continue;

                row = new GameTableRow { Order = table.Rows.Count };
                table.Rows.Add(row);
            }

            foreach (var sourceColumn in table.Columns.OrderBy(column => column.Order))
            {
                var logicalColumnKey = logicalTable.Kind == "Localized"
                    && !sourceColumn.Name.Equals(sourceKeyColumn.Name, StringComparison.OrdinalIgnoreCase)
                    ? $"{sourceColumn.Name}:{table.LanguageCode}"
                    : sourceColumn.Name;
                if (values.TryGetValue(logicalColumnKey, out var value))
                    SetCellValue(row, sourceColumn.Name, value);
            }
        }
    }

    private static void UpdateSourceTableCell(
        GameTable table,
        string rowKey,
        string columnName,
        string value)
    {
        var keyColumn = table.Columns.OrderBy(column => column.Order).First();
        var row = table.Rows.FirstOrDefault(candidate =>
            GetCellValue(candidate, keyColumn.Name).Equals(rowKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return;
        }

        SetCellValue(row, columnName, value);
    }

    private static void SetCellValue(GameTableRow row, string columnName, string value)
    {
        for (var i = 0; i < row.Cells.Count; i++)
        {
            if (row.Cells[i].ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                row.Cells[i] = row.Cells[i] with { Value = value };
                return;
            }
        }

        row.Cells.Add(new GameTableCell(columnName, value));
    }
}
