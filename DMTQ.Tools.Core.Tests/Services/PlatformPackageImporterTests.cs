using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PlatformPackageImporterTests
{
    [TestMethod]
    public async Task ImportPlatformAsync_PreservesBaselineWhenManifestFileIsMissing()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-import-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(packageRoot);
            await WriteManifestAsync(packageRoot, [
                Entry("table/us/song_song.csv", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: true),
                Entry("preview/shared.p.opus", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", compressed: false)
            ]);
            Directory.CreateDirectory(Path.Combine(packageRoot, "preview"));
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "preview", "shared.p.opus"), "preview-bytes");

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, packageRoot, "ios");

            var platform = package.Platforms.Single(p => p.Platform == "ios");
            platform.BaselineManifestEntries.Should().HaveCount(2);
            platform.MissingPhysicalFileCount.Should().Be(1);
            package.Tables.Tables.Should().BeEmpty();
            package.Resources.Should().ContainSingle(r =>
                r.PackageRelativePath == "preview/shared.p.opus"
                && r.Category == "preview"
                && r.IncludedPlatforms!.Contains("ios"));
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(packageRoot);
        }
    }

    [TestMethod]
    public async Task ImportPlatformAsync_StoresDlcAndFontsUnderPlatformSpecificArchive()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-resources-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(Path.Combine(packageRoot, "dlc"));
            Directory.CreateDirectory(Path.Combine(packageRoot, "Fonts"));
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "dlc", "android-only.bin"), "android-dlc");
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "Fonts", "font.bin"), "android-font");
            await WriteManifestAsync(packageRoot, [
                Entry("dlc/android-only.bin", "11111111111111111111111111111111", compressed: false),
                Entry("Fonts/font.bin", "22222222222222222222222222222222", compressed: false)
            ]);

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, packageRoot, "android");

            package.Resources.Should().HaveCount(2);
            package.Resources.Should().OnlyContain(r => r.Platform == "android");
            package.Resources.Should().Contain(r => r.ProjectRelativePath == "resources/android/dlc/android-only.bin");
            package.Resources.Should().Contain(r => r.ProjectRelativePath == "resources/android/Fonts/font.bin");
            File.Exists(Path.Combine(projectRoot, "resources", "android", "dlc", "android-only.bin")).Should().BeTrue();
            File.Exists(Path.Combine(projectRoot, "resources", "android", "Fonts", "font.bin")).Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(packageRoot);
        }
    }

    [TestMethod]
    public async Task ImportPlatformAsync_ExtractsSongsAsEntitiesAndDeduplicates()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-tables-" + Guid.NewGuid().ToString("N"));
        var androidRoot = Path.Combine(Path.GetTempPath(), "dmtq-android-package-" + Guid.NewGuid().ToString("N"));
        var iosRoot = Path.Combine(Path.GetTempPath(), "dmtq-ios-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            CreatePackageWithTable(androidRoot, "table/us/song_song.csv", "us", "song_id,name", "1,android-song");
            CreatePackageWithTable(iosRoot, "table/us/song_song.csv", "us", "song_id,name", "1,ios-song");
            CreatePackageWithTable(iosRoot, "table/jp/song_song.csv", "jp", "song_id,name", "1,ios-song-jp");

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, androidRoot, "android");
            await importer.ImportPlatformAsync(package, iosRoot, "ios");

            // Song tables should be removed after entity extraction
            package.Tables.Tables.Where(t => SongCatalogService.IsSongRelatedTable(t.TableName))
                .Should().BeEmpty("song tables should be extracted into entities");
            // Songs should be stored as entities
            package.Songs.Should().ContainSingle();
            package.Songs[0].Id.Should().Be("1");
            package.Songs[0].Name.Should().Be("android-song", "first import wins");
            package.Platforms.Should().HaveCount(2);
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(androidRoot);
            DeleteDirectory(iosRoot);
        }
    }

    [TestMethod]
    public async Task ImportPlatformAsync_MergesPreviewInclusionAcrossPlatforms()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-preview-" + Guid.NewGuid().ToString("N"));
        var androidRoot = Path.Combine(Path.GetTempPath(), "dmtq-android-preview-" + Guid.NewGuid().ToString("N"));
        var iosRoot = Path.Combine(Path.GetTempPath(), "dmtq-ios-preview-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(Path.Combine(androidRoot, "preview"));
            Directory.CreateDirectory(Path.Combine(iosRoot, "preview"));
            await File.WriteAllTextAsync(Path.Combine(androidRoot, "preview", "shared.p.opus"), "binary");
            await File.WriteAllTextAsync(Path.Combine(iosRoot, "preview", "shared.p.opus"), "binary");
            await WriteManifestAsync(androidRoot, [
                Entry("preview/shared.p.opus", "11111111111111111111111111111111", compressed: false)
            ]);
            await WriteManifestAsync(iosRoot, [
                Entry("preview/shared.p.opus", "22222222222222222222222222222222", compressed: false)
            ]);

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, androidRoot, "android");
            await importer.ImportPlatformAsync(package, iosRoot, "ios");

            var preview = package.Resources.Single(r => r.Category == "preview");
            preview.IncludedPlatforms.Should().Contain(["android", "ios"]);
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(androidRoot);
            DeleteDirectory(iosRoot);
        }
    }

    private static PatchPackage CreateEmptyProject(string projectRoot)
        => new()
        {
            ProjectInfo = new ProjectInfo(projectRoot, null, null, null)
        };

    private static PlatformPackageImporter CreateImporter()
        => new(
            new Lz4CompressionService(),
            new PatchManifestReader(),
            new CsvTableReader(),
            new SongCatalogService());

    private static async Task WriteManifestAsync(
        string packageRoot,
        List<(string FileName, string Checksum, bool Compressed)> entries)
    {
        var manifestPath = Path.Combine(packageRoot, "patch_new.csv");
        await using var writer = new StreamWriter(manifestPath);
        await writer.WriteLineAsync("file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag");
        foreach (var entry in entries)
        {
            await writer.WriteLineAsync($"{entry.FileName},0,{entry.Checksum},0,,0,{(entry.Compressed ? 1 : 0)},,");
        }

        await writer.FlushAsync();
        writer.Close();

        var csvBytes = await File.ReadAllBytesAsync(manifestPath);
        using var ms = new MemoryStream();
        using (var lz4 = K4os.Compression.LZ4.Legacy.LZ4Legacy.Encode(ms, leaveOpen: true))
        {
            await lz4.WriteAsync(csvBytes);
        }

        await File.WriteAllBytesAsync(Path.Combine(packageRoot, "patch_new.csv.lz4"), ms.ToArray());
    }

    private static (string FileName, string Checksum, bool Compressed) Entry(
        string fileName, string checksum, bool compressed)
        => (fileName, checksum, compressed);

    private static void CreatePackageWithTable(string packageRoot, string relativePath, string languageCode, string columnLine, string rowLine)
    {
        var dir = Path.Combine(packageRoot, Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(dir);
        var csvPath = Path.Combine(packageRoot, relativePath);
        File.WriteAllText(csvPath, $"{columnLine}\n{rowLine}");

        var compressedPath = csvPath + ".lz4";
        var csvBytes = File.ReadAllBytes(csvPath);
        using (var ms = new MemoryStream())
        {
            using (var lz4 = K4os.Compression.LZ4.Legacy.LZ4Legacy.Encode(ms, leaveOpen: true))
            {
                lz4.Write(csvBytes);
            }
            File.WriteAllBytes(compressedPath, ms.ToArray());
        }

        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(csvBytes)).ToLowerInvariant();
        var checksumCompressed = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(compressedPath))).ToLowerInvariant();
        var manifestPath = Path.Combine(packageRoot, "patch_new.csv");
        using (var manifestWriter = new StreamWriter(manifestPath))
        {
            manifestWriter.WriteLine("file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag");
            manifestWriter.WriteLine($"{relativePath},{csvBytes.Length},{checksum},{File.ReadAllBytes(compressedPath).Length},{checksumCompressed},0,1,,src:{languageCode}");
            manifestWriter.Flush();
        }

        var manifestCsvBytes = File.ReadAllBytes(manifestPath);
        using (var manifestMs = new MemoryStream())
        {
            using (var lz4 = K4os.Compression.LZ4.Legacy.LZ4Legacy.Encode(manifestMs, leaveOpen: true))
            {
                lz4.Write(manifestCsvBytes);
            }
            File.WriteAllBytes(Path.Combine(packageRoot, "patch_new.csv.lz4"), manifestMs.ToArray());
        }
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
