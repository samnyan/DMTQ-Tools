using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchManifestIOTests
{
    [TestMethod]
    public async Task ReadAsync_ReadsPatchNewCsvRows()
    {
        var csv = "file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag,\r\n" +
                  "table/us/song_song.csv,22838,abc,10653,def,0,1,,,\r\n";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var manifest = await PatchManifestIO.ReadAsync(stream);

        manifest.Entries.Should().ContainSingle();
        var entry = manifest.Entries[0];
        entry.FileName.Should().Be("table/us/song_song.csv");
        entry.FileSize.Should().Be(22838);
        entry.Checksum.Should().Be("abc");
        entry.CompressedFileSize.Should().Be(10653);
        entry.CompressedChecksum.Should().Be("def");
        entry.AcquireOnDemand.Should().Be(0);
        entry.Compressed.Should().BeTrue();
        entry.Platform.Should().BeEmpty();
        entry.Tag.Should().BeEmpty();
    }

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

        await PatchManifestIO.WriteAsync(manifest, stream);

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        output.Should().StartWith("file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag");
        output.Should().Contain("table/us/song_song.csv,22838,abc,10653,def,0,1,,");
    }
}
