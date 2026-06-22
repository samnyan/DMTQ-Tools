using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class CsvTableWriter
{
    public async Task WriteAsync(
        GameTable table,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(stream);

        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        var columns = table.Columns.OrderBy(c => c.Order).ToArray();
        foreach (var column in columns)
        {
            csv.WriteField(column.Name);
        }

        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (var row in table.Rows.OrderBy(r => r.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var column in columns)
            {
                var cell = row.Cells.FirstOrDefault(c => c.ColumnName == column.Name);
                csv.WriteField(cell?.Value ?? string.Empty);
            }

            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
