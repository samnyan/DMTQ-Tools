using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchPackageValidatorTests
{
    [TestMethod]
    public async Task ValidateAsync_ReturnsSuccessWhenFilesMatchManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "dmtq-validate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "table", "us"));
            var filePath = Path.Combine(root, "table", "us", "song_song.csv");
            await File.WriteAllTextAsync(filePath, "abc");

            var manifest = new PatchManifest();
            manifest.Entries.Add(new PatchFileEntry(
                "table/us/song_song.csv",
                FileUtility.GetFileSize(filePath),
                await FileUtility.ComputeMd5Async(filePath),
                FileUtility.GetFileSize(filePath),
                await FileUtility.ComputeMd5Async(filePath),
                0,
                false,
                string.Empty,
                string.Empty));

            var validator = new PatchPackageValidator();

            var result = await validator.ValidateAsync(manifest, root);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ValidateAsync_ReportsMissingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "dmtq-validate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var manifest = new PatchManifest();
            manifest.Entries.Add(new PatchFileEntry("missing.bin", 1, "abc", 1, "abc", 0, false, string.Empty, string.Empty));
            var validator = new PatchPackageValidator();

            var result = await validator.ValidateAsync(manifest, root);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Missing file", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ValidateAsync_ChecksCompressedFileWhenEntryIsCompressed()
    {
        var root = Path.Combine(Path.GetTempPath(), "dmtq-validate-compressed-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "dlc"));
            var filePath = Path.Combine(root, "dlc", "sample.unity3d");
            var compressedPath = filePath + ".lz4";
            await File.WriteAllTextAsync(filePath, "uncompressed");
            await File.WriteAllTextAsync(compressedPath, "compressed");

            var manifest = new PatchManifest();
            manifest.Entries.Add(new PatchFileEntry(
                "dlc/sample.unity3d",
                FileUtility.GetFileSize(filePath),
                await FileUtility.ComputeMd5Async(filePath),
                FileUtility.GetFileSize(compressedPath),
                await FileUtility.ComputeMd5Async(compressedPath),
                0,
                true,
                string.Empty,
                string.Empty));

            var validator = new PatchPackageValidator();

            var result = await validator.ValidateAsync(manifest, root);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ValidateAsync_DoesNotRequireCompressedFileWhenEntryIsUncompressed()
    {
        var root = Path.Combine(Path.GetTempPath(), "dmtq-validate-uncompressed-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "table", "us"));
            var filePath = Path.Combine(root, "table", "us", "song_song.csv");
            await File.WriteAllTextAsync(filePath, "id,name\r\n1,test\r\n");

            var manifest = new PatchManifest();
            manifest.Entries.Add(new PatchFileEntry(
                "table/us/song_song.csv",
                FileUtility.GetFileSize(filePath),
                await FileUtility.ComputeMd5Async(filePath),
                0,
                string.Empty,
                0,
                false,
                string.Empty,
                string.Empty));

            var validator = new PatchPackageValidator();

            var result = await validator.ValidateAsync(manifest, root);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
