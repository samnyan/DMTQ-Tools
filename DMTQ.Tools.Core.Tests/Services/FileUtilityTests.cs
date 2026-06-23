using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class FileUtilityTests
{
    // ── File size & hashing (was PatchChecksumServiceTests) ──

    [TestMethod]
    public async Task GetFileSize_ReturnsExactByteLength()
    {
        var path = Path.Combine(Path.GetTempPath(), "dmtq-size-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03, 0x04]);

            var size = FileUtility.GetFileSize(path);

            size.Should().Be(4);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task ComputeMd5Async_ReturnsLowercaseHexDigest()
    {
        var path = Path.Combine(Path.GetTempPath(), "dmtq-md5-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, "abc");

            var checksum = await FileUtility.ComputeMd5Async(path);

            checksum.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    // ── LZ4 compression (was Lz4CompressionServiceTests) ──

    [TestMethod]
    public async Task CompressThenDecompressAsync_RestoresOriginalBytes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dmtq-lz4-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var source = Path.Combine(tempRoot, "sample.csv");
            var compressed = Path.Combine(tempRoot, "sample.csv.lz4");
            var restored = Path.Combine(tempRoot, "sample-restored.csv");
            await File.WriteAllTextAsync(source, "id,name\r\n1,oblivion\r\n");

            await FileUtility.CompressFileAsync(source, compressed);
            await FileUtility.DecompressFileAsync(compressed, restored);

            var originalBytes = await File.ReadAllBytesAsync(source);
            var restoredBytes = await File.ReadAllBytesAsync(restored);
            restoredBytes.Should().Equal(originalBytes);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── Path classification (was PathClassifierTests) ──

    [TestMethod]
    public void NormalizePackageRelativePath_ConvertsBackslashes()
    {
        var path = FileUtility.NormalizePackageRelativePath(@"table\us\song_song.csv");

        path.Should().Be("table/us/song_song.csv");
    }

    [TestMethod]
    public void NormalizePackageRelativePath_RejectsParentTraversal()
    {
        var action = () => FileUtility.NormalizePackageRelativePath("../outside.bin");

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*unsafe package path*");
    }

    [TestMethod]
    public void NormalizePackageRelativePath_RejectsRootedPaths()
    {
        var rooted = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "outside.bin"));

        var action = () => FileUtility.NormalizePackageRelativePath(rooted);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*unsafe package path*");
    }
}
