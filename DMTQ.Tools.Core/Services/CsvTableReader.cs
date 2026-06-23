using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class CsvTableReader
{
    public async Task<GameTable> ReadAsync(
        Stream stream,
        string packageRelativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRelativePath);

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null
        });

        if (!await csv.ReadAsync().ConfigureAwait(false))
        {
            throw new InvalidDataException($"CSV table '{packageRelativePath}' is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? throw new InvalidDataException($"CSV table '{packageRelativePath}' has no header.");
        var table = new GameTable
        {
            PackageRelativePath = NormalizePath(packageRelativePath),
            TableName = GetTableName(packageRelativePath),
            LanguageCode = GetLanguageCode(packageRelativePath)
        };

        for (var i = 0; i < headers.Length; i++)
        {
            table.Columns.Add(new GameTableColumn(headers[i], i));
        }

        var rowIndex = 0;
        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new GameTableRow { Order = rowIndex };
            foreach (var column in table.Columns)
            {
                row.Cells.Add(new GameTableCell(column.Name, csv.GetField(column.Order) ?? string.Empty));
            }

            table.Rows.Add(row);
            rowIndex++;
        }

        return table;
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private static string GetTableName(string path)
    {
        var normalized = NormalizePath(path);
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    private static string? GetLanguageCode(string path)
    {
        var parts = NormalizePath(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[0].Equals("table", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }
}
