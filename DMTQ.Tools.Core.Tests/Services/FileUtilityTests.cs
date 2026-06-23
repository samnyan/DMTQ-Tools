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
    public async Task Lz4RoundtripWithExternalPatchData_MatchesManifestChecksums()
    {
        // Verify LZ4 decompression is correct against real patch data.
        // Decompress .lz4 file → verify MD5 matches patch_new.csv "checksum" (source).
        // Note: recompression checksums are NOT verified — LZ4 compression output
        // is not deterministic across implementations/versions.

        var iosRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "external", "patch", "phone_new", "1.003.005", "ios"));
        var csvPath = Path.Combine(iosRoot, "patch_new.csv");

        if (!File.Exists(csvPath))
        {
            Assert.Inconclusive($"External test data not found at {iosRoot}. Skipping LZ4 roundtrip test.");
            return;
        }

        // Read manifest to look up expected checksums
        var manifest = new Dictionary<string, (string sourceChecksum, long fileSize)>();
        foreach (var line in await File.ReadAllLinesAsync(csvPath))
        {
            var parts = line.Split(',');
            if (parts.Length < 5 || parts[0] == "file_name") continue;
            manifest[parts[0]] = (parts[2], long.Parse(parts[1]));
        }

        // Pick representative files across categories (only files that exist on disk)
        var testFiles = new[] { "dlc/d3_i0.unity3d", "preview/childof.p.opus", "table/cn/category_categoryproduct.csv" };
        var mismatches = new List<string>();

        foreach (var relativePath in testFiles)
        {
            var expected = manifest[relativePath];
            var compressedPath = Path.Combine(iosRoot, relativePath + ".lz4");
            File.Exists(compressedPath).Should().BeTrue($"test data must exist: {compressedPath}");

            var tempRoot = Path.Combine(Path.GetTempPath(), "dmtq-lz4-ext-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var decompressedPath = Path.Combine(tempRoot, Path.GetFileName(relativePath));

                // Decompress and verify
                await FileUtility.DecompressFileAsync(compressedPath, decompressedPath);
                var decompressedChecksum = await FileUtility.ComputeMd5Async(decompressedPath);
                var decompressedSize = FileUtility.GetFileSize(decompressedPath);

                if (!string.Equals(decompressedChecksum, expected.sourceChecksum, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add($"{relativePath}: decompressed MD5 mismatch (expected {expected.sourceChecksum}, got {decompressedChecksum})");
                if (decompressedSize != expected.fileSize)
                    mismatches.Add($"{relativePath}: decompressed size mismatch (expected {expected.fileSize}, got {decompressedSize})");
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        mismatches.Should().BeEmpty(string.Join("\n", mismatches));
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
