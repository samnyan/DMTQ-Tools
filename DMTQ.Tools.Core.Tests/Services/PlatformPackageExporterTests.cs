using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PlatformPackageExporterTests
{
    [TestMethod]
    public async Task ExportPlatformAsync_DeltaKeepsBaselineManifestAndSkipsMissingUnchangedFile()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-export-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateProjectWithPlatform(projectRoot, "ios", [
                Entry("dlc/built-in-only.bin", 10, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: false)
            ]);
            var exporter = CreateExporter();

            var result = await exporter.ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "ios", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            result.Manifest.Entries.Should().ContainSingle(e => e.FileName == "dlc/built-in-only.bin");
            result.FilesSkippedAsBaseline.Should().Be(1);
            result.FilesWritten.Should().Be(2, "patch_new.csv and patch_new.csv.lz4 are always written");
            File.Exists(Path.Combine(exportRoot, "patch_new.csv")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "patch_new.csv.lz4")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "dlc", "built-in-only.bin")).Should().BeFalse();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_WritesChangedSharedTableInDeltaMode()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-table-export-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-table-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateProjectWithPlatform(projectRoot, "android", [
                Entry("table/us/song_song.csv", 1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: false)
            ]);
            AddSongTable(package, "changed-name");
            var exporter = CreateExporter();

            var result = await exporter.ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            result.FilesWritten.Should().BeGreaterThan(2);
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_song.csv")).Should().BeTrue();
            result.Manifest.Entries.Single(e => e.FileName == "table/us/song_song.csv").Checksum
                .Should().NotBe("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_UsesPlatformSpecificDlcAndSharedPreviewInclusion()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-resource-export-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-resource-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "android", "dlc"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "shared", "preview"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"), "android-dlc-current");
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "shared", "preview", "song.p.opus"), "preview-current");

            var package = CreateProjectWithPlatform(projectRoot, "android", [
                Entry("dlc/android.bin", 1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: false),
                Entry("preview/song.p.opus", 1, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", compressed: false)
            ]);
            package.Resources.Add(new ResourceFile("dlc/android.bin", "resources/android/dlc/android.bin", "dlc", false, null, "android"));
            package.Resources.Add(new ResourceFile("preview/song.p.opus", "resources/shared/preview/song.p.opus", "preview", false, null, null, ["android"]));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            File.Exists(Path.Combine(exportRoot, "dlc", "android.bin")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "preview", "song.p.opus")).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_SkipsUnchangedBaselineFileInDeltaMode()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-dlta-skip-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-dlta-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "android", "dlc"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"), "same-content");

            var checksum = Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(
                    await File.ReadAllBytesAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"))))
                .ToLowerInvariant();

            var package = CreateProjectWithPlatform(projectRoot, "android", [
                Entry("dlc/android.bin", 12, checksum, compressed: false)
            ]);
            package.Resources.Add(new ResourceFile("dlc/android.bin", "resources/android/dlc/android.bin", "dlc", false, null, "android"));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            result.FilesSkippedAsBaseline.Should().Be(1);
            File.Exists(Path.Combine(exportRoot, "dlc", "android.bin")).Should().BeFalse();
            result.Manifest.Entries.Should().ContainSingle(e => e.FileName == "dlc/android.bin" && e.Checksum == checksum);
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_WritesProjectOnlyPreviewResourceForIncludedPlatform()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-preview-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-preview-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "preview"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "preview", "new.opus"), "new-preview");
            var package = CreateProjectWithPlatform(projectRoot, "android", []);
            package.Resources.Add(new ResourceFile("preview/new.opus", "resources/preview/new.opus", "preview", false, null, null, ["android"]));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            result.Manifest.Entries.Should().Contain(entry => entry.FileName == "preview/new.opus" && !entry.Compressed);
            File.Exists(Path.Combine(exportRoot, "preview", "new.opus")).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_WritesProjectOnlyPlatformResourceWithCompressionFlag()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-dlc-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-dlc-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "ios", "dlc"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "ios", "dlc", "new.bundle"), "new-dlc");
            var package = CreateProjectWithPlatform(projectRoot, "ios", []);
            package.Resources.Add(new ResourceFile("dlc/new.bundle", "resources/ios/dlc/new.bundle", "dlc", true, null, "ios", null));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "ios", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            var entry = result.Manifest.Entries.Single(e => e.FileName == "dlc/new.bundle");
            entry.Compressed.Should().BeTrue();
            entry.CompressedFileSize.Should().BeGreaterThan(0);
            entry.CompressedChecksum.Should().NotBeEmpty();
            File.Exists(Path.Combine(exportRoot, "dlc", "new.bundle")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "dlc", "new.bundle.lz4")).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_HonorsResourceCompressedOverBaselineEntry()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-compression-override-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-compression-override-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "android", "dlc"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"), "dlc-content");
            var contentBytes = await File.ReadAllBytesAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"));
            var baselineChecksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(contentBytes)).ToLowerInvariant();

            var package = CreateProjectWithPlatform(projectRoot, "android", [
                Entry("dlc/android.bin", contentBytes.Length, baselineChecksum, compressed: true)
            ]);
            // User changes resource to uncompressed via Resource Manager
            package.Resources.Add(new ResourceFile("dlc/android.bin", "resources/android/dlc/android.bin", "dlc", false, null, "android"));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Delta });

            result.Validation.Errors.Should().BeEmpty();
            var entry = result.Manifest.Entries.Single(e => e.FileName == "dlc/android.bin");
            entry.Compressed.Should().BeFalse("resource.Compressed should override baseline.Compressed");
            entry.CompressedFileSize.Should().Be(0);
            entry.CompressedChecksum.Should().BeEmpty();
            File.Exists(Path.Combine(exportRoot, "dlc", "android.bin.lz4")).Should().BeFalse();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    [TestMethod]
    public async Task ExportPlatformAsync_ManifestChecksumsAreLowercaseMd5()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-md5-format-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-md5-format-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "android", "dlc"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"), "md5-test");
            var package = CreateProjectWithPlatform(projectRoot, "android", [
                Entry("dlc/android.bin", 8, "00000000000000000000000000000000", compressed: false)
            ]);
            package.Resources.Add(new ResourceFile("dlc/android.bin", "resources/android/dlc/android.bin", "dlc", false, null, "android"));

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Full });

            var entry = result.Manifest.Entries.Single(e => e.FileName == "dlc/android.bin");
            entry.Checksum.Should().MatchRegex("^[0-9a-f]{32}$", "checksum must be 32-char lowercase hex MD5");
            entry.Checksum.Should().NotContainAny("A", "B", "C", "D", "E", "F");
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(exportRoot);
        }
    }

    private static PlatformPackageExporter CreateExporter()
        => new(new PatchManifestWriter(), new Lz4CompressionService(), new PatchChecksumService());

    private static PatchPackage CreateProjectWithPlatform(string projectRoot, string platform, PatchFileEntry[] baselineEntries)
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo(projectRoot, null, "1.003.005", null)
        };
        var platformRecord = new PlatformPackageRecord
        {
            Platform = platform,
            SourcePackageRoot = "source-" + platform,
            Version = "1.003.005"
        };
        platformRecord.BaselineManifestEntries.AddRange(baselineEntries);
        package.Platforms.Add(platformRecord);
        return package;
    }

    private static PatchFileEntry Entry(string fileName, long fileSize, string checksum, bool compressed)
        => new(fileName, fileSize, checksum, compressed ? fileSize + 1 : 0, compressed ? checksum : string.Empty, 0, compressed, string.Empty, string.Empty);

    private static void AddSongTable(PatchPackage package, string songName)
    {
        package.Songs.Add(new Song
        {
            Id = "1",
            Name = songName
        });
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
