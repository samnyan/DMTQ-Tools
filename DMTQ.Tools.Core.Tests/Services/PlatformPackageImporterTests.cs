using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
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
            Directory.CreateDirectory(Path.Combine(packageRoot, "preview"));
            var previewContent = "preview-bytes"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(packageRoot, "preview", "shared.p.opus"), previewContent);
            var previewChecksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(previewContent)).ToLowerInvariant();

            await WriteManifestAsync(packageRoot, [
                Entry("table/us/song_song.csv", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", compressed: true),
                Entry("preview/shared.p.opus", previewChecksum, compressed: false)
            ]);

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, packageRoot, "ios");

            // Check that resources were created
            package.Resources.Should().NotBeEmpty();
            var previewResource = package.Resources.FirstOrDefault(r => r.FileName == "preview/shared.p.opus");
            previewResource.Should().NotBeNull();
            previewResource!.Category.Should().Be("preview");
            previewResource.PlatformManifest.Should().Contain(m => m.Platform == "share" && m.Exist);
            // Table should have been attempted but source file is missing
            package.Tables.Tables.Should().BeEmpty();
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
            var dlcContent = "android-dlc"u8.ToArray();
            var fontContent = "android-font"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(packageRoot, "dlc", "android-only.bin"), dlcContent);
            await File.WriteAllBytesAsync(Path.Combine(packageRoot, "Fonts", "font.bin"), fontContent);
            var dlcChecksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(dlcContent)).ToLowerInvariant();
            var fontChecksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(fontContent)).ToLowerInvariant();
            await WriteManifestAsync(packageRoot, [
                Entry("dlc/android-only.bin", dlcChecksum, compressed: false),
                Entry("Fonts/font.bin", fontChecksum, compressed: false)
            ]);

            var package = CreateEmptyProject(projectRoot);
            var importer = CreateImporter();

            await importer.ImportPlatformAsync(package, packageRoot, "android");

            package.Resources.Should().HaveCount(2);
            package.Resources.Should().OnlyContain(r => r.PlatformManifest.Any(m => m.Platform == "android" && m.Exist));
            package.Resources.Should().Contain(r => r.FileName == "dlc/android-only.bin");
            package.Resources.Should().Contain(r => r.FileName == "Fonts/font.bin");
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
    public async Task ImportPlatformAsync_PreservesDifferentEntityTablesPerPlatform()
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
            // Legacy root data mirrors the first import for backwards compatibility.
            package.Songs.Should().ContainSingle();
            package.Songs[0].Id.Should().Be(1);
            package.Songs[0].Name.Should().Be("android-song");

            package.GetPlatformTables("android").Songs.Should().ContainSingle()
                .Which.Name.Should().Be("android-song");
            package.GetPlatformTables("ios").Songs.Should().ContainSingle()
                .Which.Name.Should().Be("ios-song-jp");
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
            preview.PlatformManifest.Should().Contain(m => m.Platform == "share");
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(androidRoot);
            DeleteDirectory(iosRoot);
        }
    }

    [TestMethod]
    public async Task ImportPlatformAsync_ImportsSlangAsEditableSharedTable()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-slang-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-slang-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            CreatePackageWithTable(packageRoot, "table/slang/slang.csv", "", "slang,", "\"blocked\",");
            var package = CreateEmptyProject(projectRoot);

            await CreateImporter().ImportPlatformAsync(package, packageRoot, "android");

            package.SlangEntries.Should().ContainSingle().Which.Value.Should().Be("blocked");
            package.Resources.Should().NotContain(resource => resource.FileName == "table/slang/slang.csv");
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(packageRoot);
        }
    }

    [TestMethod]
    public async Task ImportPlatformAsync_PreservesCategoryMultiplicityWithoutRepeatingLanguageCopies()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-category-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(Path.GetTempPath(), "dmtq-platform-category-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            CreatePackageWithTables(packageRoot,
                ("table/cn/product_product.csv", "product_id,item_id", "P1,I1"),
                ("table/cn/category_categoryproduct.csv", "category_id,product_id", "C1,P1\nC1,P1"),
                ("table/jp/category_categoryproduct.csv", "category_id,product_id", "C1,P1\nC1,P1"));
            var package = CreateEmptyProject(projectRoot);

            await CreateImporter().ImportPlatformAsync(package, packageRoot, "android");

            package.GetPlatformTables("android").Products.Single().CategoryIds.Should().Equal("C1", "C1");
        }
        finally
        {
            DeleteDirectory(projectRoot);
            DeleteDirectory(packageRoot);
        }
    }

    private static PatchPackage CreateEmptyProject(string projectRoot)
        => new()
        {
            ProjectInfo = new ProjectInfo(projectRoot, null, null, null)
        };

    private static PlatformPackageImporter CreateImporter()
        => new();

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

        var checksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(csvBytes)).ToLowerInvariant();
        var checksumCompressed = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(File.ReadAllBytes(compressedPath))).ToLowerInvariant();
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

    private static void CreatePackageWithTables(
        string packageRoot,
        params (string RelativePath, string Header, string Rows)[] tables)
    {
        Directory.CreateDirectory(packageRoot);
        var manifestRows = new List<string>
        {
            "file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag"
        };

        foreach (var table in tables)
        {
            var csvPath = Path.Combine(packageRoot, table.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
            File.WriteAllText(csvPath, $"{table.Header}\n{table.Rows}");
            var csvBytes = File.ReadAllBytes(csvPath);
            var compressedPath = csvPath + ".lz4";
            using (var compressed = File.Create(compressedPath))
            using (var lz4 = K4os.Compression.LZ4.Legacy.LZ4Legacy.Encode(compressed, leaveOpen: false))
                lz4.Write(csvBytes);
            var compressedBytes = File.ReadAllBytes(compressedPath);
            manifestRows.Add($"{table.RelativePath},{csvBytes.Length},{Md5(csvBytes)},{compressedBytes.Length},{Md5(compressedBytes)},0,1,,");
        }

        var manifestPath = Path.Combine(packageRoot, "patch_new.csv");
        File.WriteAllLines(manifestPath, manifestRows);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        using var manifestCompressed = File.Create(Path.Combine(packageRoot, "patch_new.csv.lz4"));
        using var manifestLz4 = K4os.Compression.LZ4.Legacy.LZ4Legacy.Encode(manifestCompressed, leaveOpen: false);
        manifestLz4.Write(manifestBytes);
    }

    private static string Md5(byte[] bytes)
        => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant();

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
