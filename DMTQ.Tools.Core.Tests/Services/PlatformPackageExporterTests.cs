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
    public async Task ExportPlatformAsync_WritesManifestForMissingOnDiskFileWithPlatformEntry()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-export-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateProjectWithResource(projectRoot, "ios", "dlc/built-in-only.bin",
                sourceFileSize: 10, sourceChecksum: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: false, exist: false);
            var exporter = CreateExporter();

            var result = await exporter.ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "ios", Mode = PlatformExportMode.Full });

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
    public async Task ExportPlatformAsync_WritesChangedSharedTable()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-table-export-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-table-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateProjectWithResource(projectRoot, "android", "table/us/song_song.csv",
                sourceFileSize: 1, sourceChecksum: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: false, exist: true);
            AddSongTable(package, "changed-name");
            var exporter = CreateExporter();

            var result = await exporter.ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Full });

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
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "preview"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "android", "dlc", "android.bin"), "android-dlc-current");
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "preview", "song.p.opus"), "preview-current");

            var package = CreateEmptyProject(projectRoot);
            package.Resources.Add(new ResourceFile
            {
                FileName = "dlc/android.bin",
                Category = "dlc",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "android",
                        Exist = true,
                        SourceFileSize = 1,
                        SourceChecksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    }
                }
            });
            package.Resources.Add(new ResourceFile
            {
                FileName = "preview/song.p.opus",
                Category = "preview",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "share",
                        Exist = true,
                        SourceFileSize = 1,
                        SourceChecksum = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                }
            });

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Full });

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
    public async Task ExportPlatformAsync_WritesProjectOnlyPreviewResourceForIncludedPlatform()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-preview-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-project-preview-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "resources", "preview"));
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "resources", "preview", "new.opus"), "new-preview");
            var package = CreateEmptyProject(projectRoot);
            package.Resources.Add(new ResourceFile
            {
                FileName = "preview/new.opus",
                Category = "preview",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "share",
                        Exist = true
                    }
                }
            });

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Full });

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
            var package = CreateEmptyProject(projectRoot);
            package.Resources.Add(new ResourceFile
            {
                FileName = "dlc/new.bundle",
                Category = "dlc",
                Compressed = true,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "ios",
                        Exist = true
                    }
                }
            });

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "ios", Mode = PlatformExportMode.Full });

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

            var package = CreateEmptyProject(projectRoot);
            // User changes resource to uncompressed via Resource Manager
            package.Resources.Add(new ResourceFile
            {
                FileName = "dlc/android.bin",
                Category = "dlc",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "android",
                        Exist = true,
                        SourceFileSize = contentBytes.Length,
                        SourceChecksum = baselineChecksum
                    }
                }
            });

            var result = await CreateExporter().ExportPlatformAsync(
                package,
                exportRoot,
                new PlatformExportOptions { Platform = "android", Mode = PlatformExportMode.Full });

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
            var package = CreateEmptyProject(projectRoot);
            package.Resources.Add(new ResourceFile
            {
                FileName = "dlc/android.bin",
                Category = "dlc",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "android",
                        Exist = true,
                        SourceFileSize = 8,
                        SourceChecksum = "00000000000000000000000000000000"
                    }
                }
            });

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
        => new();

    private static PatchPackage CreateEmptyProject(string projectRoot)
        => new()
        {
            ProjectInfo = new ProjectInfo(projectRoot, null, "1.003.005", null)
        };

    private static PatchPackage CreateProjectWithResource(
        string projectRoot, string platform, string fileName,
        long sourceFileSize, string sourceChecksum, bool compressed, bool exist)
    {
        var package = CreateEmptyProject(projectRoot);
        package.Resources.Add(new ResourceFile
        {
            FileName = fileName,
            Category = FileUtility.ResourceCategory(fileName),
            Compressed = compressed,
            PlatformManifest =
            {
                new PlatformManifestEntry
                {
                    Platform = platform,
                    Exist = exist,
                    SourceFileSize = sourceFileSize,
                    SourceChecksum = sourceChecksum
                }
            }
        });
        return package;
    }

    private static void AddSongTable(PatchPackage package, string songName)
    {
        package.Songs.Add(new Song
        {
            Id = 1,
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
