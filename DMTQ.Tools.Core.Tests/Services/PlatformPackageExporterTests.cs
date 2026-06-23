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

    [TestMethod]
    public async Task FullWorkflow_ImportThenExport_ProducesCorrectManifest()
    {
        // Self-contained: generates all test data in temp directories — no external dependencies.
        var tempProjectRoot = Path.Combine(Path.GetTempPath(), "dmtq-full-workflow-" + Guid.NewGuid().ToString("N"));
        var tempExportRoot = Path.Combine(Path.GetTempPath(), "dmtq-full-export-" + Guid.NewGuid().ToString("N"));
        var tempAndroidRoot = Path.Combine(Path.GetTempPath(), "dmtq-full-android-" + Guid.NewGuid().ToString("N"));
        var tempIosRoot = Path.Combine(Path.GetTempPath(), "dmtq-full-ios-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempProjectRoot);
            Directory.CreateDirectory(tempAndroidRoot);
            Directory.CreateDirectory(tempIosRoot);

            GenerateTestPatchPackage(tempAndroidRoot, "android", tempProjectRoot);
            GenerateTestPatchPackage(tempIosRoot, "ios", tempProjectRoot);

            // Step 1: Create empty PatchPackage
            var package = CreateEmptyProject(tempProjectRoot);

            // Step 2: Import android platform
            var importer = new PlatformPackageImporter();
            await importer.ImportPlatformAsync(package, tempAndroidRoot, "android");

            // Step 3: Import ios platform
            await importer.ImportPlatformAsync(package, tempIosRoot, "ios");

            // Step 4: Verify Songs and Resources populated
            package.Songs.Count.Should().BeGreaterThan(0, "songs imported from song_song.csv");
            package.Resources.Count.Should().BeGreaterThan(0, "dlc + preview resources");

            // Step 5: Export for ios in Full mode
            var exporter = new PlatformPackageExporter();
            var result = await exporter.ExportPlatformAsync(
                package,
                tempExportRoot,
                new PlatformExportOptions { Platform = "ios", Mode = PlatformExportMode.Full });

            // Step 6: Verify patch_new.csv exists and manifest has entries
            result.Validation.Errors.Should().BeEmpty();
            File.Exists(Path.Combine(tempExportRoot, "patch_new.csv")).Should().BeTrue();
            result.Manifest.Entries.Should().NotBeEmpty();

            // Step 7: Verify at least one resource/table file exists in export dir
            var exportedFiles = Directory.GetFiles(tempExportRoot, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(tempExportRoot, f).Replace('\\', '/'))
                .Where(f => f != "patch_new.csv" && f != "patch_new.csv.lz4")
                .ToArray();
            exportedFiles.Should().NotBeEmpty("at least one resource or table file should be exported");

            // Step 8: Verify LZ4 compression — .lz4 files exist for compressed entries
            var lz4Files = Directory.GetFiles(tempExportRoot, "*.lz4", SearchOption.AllDirectories);
            lz4Files.Should().NotBeEmpty("compressed files should produce .lz4 artifacts");
        }
        finally
        {
            DeleteDirectory(tempProjectRoot);
            DeleteDirectory(tempExportRoot);
            DeleteDirectory(tempAndroidRoot);
            DeleteDirectory(tempIosRoot);
        }
    }

    /// <summary>
    /// Generates a minimal self-contained patch package:
    ///   - patch_new.csv.lz4 (manifest with checksums)
    ///   - dlc/*.lz4 resource files
    ///   - preview/*.lz4 resource files
    ///   - table/us/song_song.csv (2 songs)
    ///   - table/us/song_songPattern.csv (3 patterns)
    /// </summary>
    private static void GenerateTestPatchPackage(string packageRoot, string platform, string tempProjectRoot)
    {
        // Resource files
        var dlcDir = Path.Combine(packageRoot, "dlc");
        var previewDir = Path.Combine(packageRoot, "preview");
        var tableDir = Path.Combine(packageRoot, "table", "us");
        Directory.CreateDirectory(dlcDir);
        Directory.CreateDirectory(previewDir);
        Directory.CreateDirectory(tableDir);

        var dlcBytes = System.Text.Encoding.UTF8.GetBytes($"dlc-file-for-{platform}-1234567890");
        var dlcPath = Path.Combine(dlcDir, $"{platform}_test.bin");
        File.WriteAllBytes(dlcPath, dlcBytes);

        var previewBytes = System.Text.Encoding.UTF8.GetBytes($"preview-opus-for-{platform}-abcdef");
        var previewPath = Path.Combine(previewDir, "song_test.p.opus");
        File.WriteAllBytes(previewPath, previewBytes);

        // LZ4 compress resource files
        var dlcLz4Path = dlcPath + ".lz4";
        var previewLz4Path = previewPath + ".lz4";
        FileUtility.CompressFileAsync(dlcPath, dlcLz4Path).GetAwaiter().GetResult();
        FileUtility.CompressFileAsync(previewPath, previewLz4Path).GetAwaiter().GetResult();

        // Delete uncompressed originals (only .lz4 stays, simulating a real patch package)
        File.Delete(dlcPath);
        File.Delete(previewPath);

        // Song CSV table (entity-backed, uncompressed)
        var songCsv = "id,name,fullname,genre,artistname,originalbgayn,loopbgayn,composedby,singer,featby,arrangedby,visualizedby,costgamepoint,costgamecash,flag,status,freeyn,hiddenyn,openyn,trackid,moddate,update\n"
            + $"1,TestSong1,Song1Full,Pop,Artist1,Y,N,Composer1,Singer1,,Arranger1,,0,0,0,,Y,N,Y,1,2024-01-01,\n"
            + $"2,TestSong2,Song2Full,Rock,Artist2,Y,N,Composer2,Singer2,,Arranger2,,0,0,0,,Y,N,Y,2,2024-01-01,";
        File.WriteAllText(Path.Combine(packageRoot, "table", "us", "song_song.csv"), songCsv);

        // Pattern CSV table
        var patternCsv = "patternid,songid,name,line,signature,difficulty,pointtype,pointvalue,flg,update\n"
            + "1,1,4K NM,4,4,1,1,100,,\n"
            + "2,1,5K NM,5,5,1,1,100,,\n"
            + "3,2,4K NM,4,4,1,1,100,,";
        File.WriteAllText(Path.Combine(packageRoot, "table", "us", "song_songPattern.csv"), patternCsv);

        // Compute all checksums
        var dlcChecksum = ComputeMd5FromBytes(dlcBytes);
        var dlcCompressedChecksum = FileUtility.ComputeMd5Async(dlcLz4Path).GetAwaiter().GetResult();
        var dlcCompressedSize = FileUtility.GetFileSize(dlcLz4Path);

        var previewChecksum = ComputeMd5FromBytes(previewBytes);
        var previewCompressedChecksum = FileUtility.ComputeMd5Async(previewLz4Path).GetAwaiter().GetResult();
        var previewCompressedSize = FileUtility.GetFileSize(previewLz4Path);

        var songCsvPath = Path.Combine(packageRoot, "table", "us", "song_song.csv");
        var songCsvSize = FileUtility.GetFileSize(songCsvPath);
        var songCsvMd5 = FileUtility.ComputeMd5Async(songCsvPath).GetAwaiter().GetResult();

        var patternCsvPath = Path.Combine(packageRoot, "table", "us", "song_songPattern.csv");
        var patternCsvSize = FileUtility.GetFileSize(patternCsvPath);
        var patternCsvMd5 = FileUtility.ComputeMd5Async(patternCsvPath).GetAwaiter().GetResult();

        // Build manifest CSV
        var manifestRows = new List<string>
        {
            "file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag,",
            $"dlc/{platform}_test.bin,{dlcBytes.Length},{dlcChecksum},{dlcCompressedSize},{dlcCompressedChecksum},0,1,,,",
            $"preview/song_test.p.opus,{previewBytes.Length},{previewChecksum},{previewCompressedSize},{previewCompressedChecksum},0,1,,,",
            $"table/us/song_song.csv,{songCsvSize},{songCsvMd5},0,,0,0,,,",
            $"table/us/song_songPattern.csv,{patternCsvSize},{patternCsvMd5},0,,0,0,,,",
        };
        var manifestPath = Path.Combine(packageRoot, "patch_new.csv");
        File.WriteAllText(manifestPath, string.Join("\n", manifestRows));

        // LZ4 compress manifest → patch_new.csv.lz4
        var manifestLz4Path = Path.Combine(packageRoot, "patch_new.csv.lz4");
        FileUtility.CompressFileAsync(manifestPath, manifestLz4Path).GetAwaiter().GetResult();
    }

    private static string ComputeMd5FromBytes(byte[] bytes)
    {
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

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
