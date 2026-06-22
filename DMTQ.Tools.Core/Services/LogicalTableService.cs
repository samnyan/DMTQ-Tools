using System.Text.RegularExpressions;
using DMTQ.Tools.Core.Models;

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

        foreach (var row in primary.Rows.OrderBy(row => row.Order))
        {
            var rowKey = GetCellValue(row, keyColumn.Name);
            var logicalRow = new LogicalTableRow { Key = rowKey };
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
            var logicalRow = new LogicalTableRow { Key = rowKey };
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
}
