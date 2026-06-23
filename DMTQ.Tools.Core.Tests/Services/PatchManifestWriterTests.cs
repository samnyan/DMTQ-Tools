using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchManifestWriterTests
{
    [TestMethod]
    public async Task WriteAsync_WritesPatchNewCsvHeadersAndRows()
    {
        var manifest = new PatchManifest();
        manifest.Entries.Add(new PatchFileEntry(
            "table/us/song_song.csv",
            22838,
            "abc",
            10653,
            "def",
            0,
            true,
            string.Empty,
            string.Empty));

        await using var stream = new MemoryStream();
        var writer = new PatchManifestWriter();

        await writer.WriteAsync(manifest, stream);

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        output.Should().StartWith("file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag");
        output.Should().Contain("table/us/song_song.csv,22838,abc,10653,def,0,1,,");
    }
}
