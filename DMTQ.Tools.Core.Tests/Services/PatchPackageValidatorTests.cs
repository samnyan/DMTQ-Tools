using DMTQ.Tools.Core.Models;
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

            var checksum = new PatchChecksumService();
            var manifest = new PatchManifest();
            manifest.Entries.Add(new PatchFileEntry(
                "table/us/song_song.csv",
                checksum.GetFileSize(filePath),
                await checksum.ComputeMd5Async(filePath),
                checksum.GetFileSize(filePath),
                await checksum.ComputeMd5Async(filePath),
                0,
                false,
                string.Empty,
                string.Empty));

            var validator = new PatchPackageValidator(checksum);

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
            var validator = new PatchPackageValidator(new PatchChecksumService());

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
}
