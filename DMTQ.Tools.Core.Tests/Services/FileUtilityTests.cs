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

    [TestMethod]
    public async Task Lz4Roundtrip_DummyData_MatchesManifestChecksums()
    {
        // Self-contained: generates test data, LZ4-compresses, records checksums,
        // then decompresses and verifies MD5 matches the recorded source checksum.
        // No external file dependencies.

        var tempRoot = Path.Combine(Path.GetTempPath(), "dmtq-lz4-rt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // Generate test content
            var content = "id,name,value\r\n1,test,abcdefghijklmnop\r\n2,foo,bar\r\n" +
                          new string('x', 500);
            var sourcePath = Path.Combine(tempRoot, "source.csv");
            await File.WriteAllTextAsync(sourcePath, content);

            // Compute expected source checksum
            var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
            var expectedChecksum = Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(sourceBytes)).ToLowerInvariant();
            var expectedSize = sourceBytes.Length;

            // LZ4 compress
            var compressedPath = sourcePath + ".lz4";
            await FileUtility.CompressFileAsync(sourcePath, compressedPath);

            // Delete uncompressed (simulate patch package with only .lz4)
            File.Delete(sourcePath);

            // Decompress and verify
            var restoredPath = Path.Combine(tempRoot, "restored.csv");
            await FileUtility.DecompressFileAsync(compressedPath, restoredPath);

            var actualChecksum = await FileUtility.ComputeMd5Async(restoredPath);
            var actualSize = FileUtility.GetFileSize(restoredPath);

            actualChecksum.Should().Be(expectedChecksum, "decompressed MD5 must match source");
            actualSize.Should().Be(expectedSize, "decompressed size must match source");
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
