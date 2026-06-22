using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class CsvTableWriterTests
{
    [TestMethod]
    public async Task WriteAsync_WritesColumnsInOrderAndQuotesCommaValues()
    {
        var table = new GameTable
        {
            PackageRelativePath = "table/us/song_song.csv",
            TableName = "song_song",
            LanguageCode = "us"
        };
        table.Columns.Add(new GameTableColumn("song_id", 0));
        table.Columns.Add(new GameTableColumn("artist_name", 1));
        var row = new GameTableRow { Order = 0 };
        row.Cells.Add(new GameTableCell("song_id", "1"));
        row.Cells.Add(new GameTableCell("artist_name", "PUNEW, Super52"));
        table.Rows.Add(row);

        await using var stream = new MemoryStream();
        var writer = new CsvTableWriter();

        await writer.WriteAsync(table, stream);

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Contain("song_id,artist_name");
        output.Should().Contain("1,\"PUNEW, Super52\"");
    }
}
