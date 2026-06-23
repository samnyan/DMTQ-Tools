using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class CsvTableReaderTests
{
    [TestMethod]
    public async Task ReadAsync_PreservesColumnsRowsAndQuotedValues()
    {
        var csv = "song_id,name,artist_name\r\n1,oblivion,ESTi\r\n2,raisemeup,\"Planetboom, Miya\"\r\n";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var reader = new CsvTableReader();

        var table = await reader.ReadAsync(stream, "table/us/song_song.csv");

        table.PackageRelativePath.Should().Be("table/us/song_song.csv");
        table.TableName.Should().Be("song_song");
        table.LanguageCode.Should().Be("us");
        table.Columns.Select(c => c.Name).Should().Equal("song_id", "name", "artist_name");
        table.Rows.Should().HaveCount(2);
        table.Rows[1].Cells.Single(c => c.ColumnName == "artist_name").Value.Should().Be("Planetboom, Miya");
    }
}
