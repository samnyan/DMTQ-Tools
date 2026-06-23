using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchPackageExporterTests
{
    [TestMethod]
    public async Task ExportAsync_WritesTablesResourcesAndPatchManifest()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-export-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-export-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);

            var exporter = new PatchPackageExporter();

            await exporter.ExportAsync(package, exportRoot);

            File.Exists(Path.Combine(exportRoot, "patch_new.csv")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "patch_new.csv.lz4")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_song.csv")).Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_song.csv.lz4")).Should().BeTrue();
            Directory.EnumerateFiles(exportRoot, "*", SearchOption.AllDirectories)
                .Should().Contain(path => path.Contains(Path.Combine("dlc", ""), StringComparison.OrdinalIgnoreCase)
                    || path.Contains(Path.Combine("preview", ""), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ImportExportValidateAsync_SucceedsForSamplePackage()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-roundtrip-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-roundtrip-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);

            var exporter = new PatchPackageExporter();
            var exportedManifest = await exporter.ExportAsync(package, exportRoot);

            var validator = new PatchPackageValidator();
            var validation = await validator.ValidateAsync(exportedManifest, exportRoot);

            validation.Errors.Should().BeEmpty();
            validation.IsValid.Should().BeTrue();
            exportedManifest.Entries.Should().NotBeEmpty();

            var resourceEntry = exportedManifest.Entries.First(e =>
                e.Compressed && e.FileName.StartsWith("dlc/", StringComparison.Ordinal));
            var resourcePath = Path.Combine(exportRoot, resourceEntry.FileName.Replace('/', Path.DirectorySeparatorChar));
            var compressedResourcePath = resourcePath + ".lz4";
            var decompressedResourcePath = Path.Combine(exportRoot, "roundtrip-resource-check-" + Guid.NewGuid().ToString("N"));

            File.Exists(resourcePath).Should().BeTrue();
            File.Exists(compressedResourcePath).Should().BeTrue();
            FileUtility.GetFileSize(resourcePath).Should().Be(resourceEntry.FileSize);
            (await FileUtility.ComputeMd5Async(resourcePath)).Should().Be(resourceEntry.Checksum);

            await FileUtility.DecompressFileAsync(compressedResourcePath, decompressedResourcePath);
            var exportedBytes = await File.ReadAllBytesAsync(resourcePath);
            var decompressedBytes = await File.ReadAllBytesAsync(decompressedResourcePath);
            decompressedBytes.Should().Equal(exportedBytes);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportAsync_WritesResourceManifestFieldsForUncompressedAndCompressedFiles()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-export-resource-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-export-resource-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);
            // Find a compressed dlc resource
            var sourceEntry = package.Resources.First(r =>
                r.Compressed && r.FileName.StartsWith("dlc/", StringComparison.Ordinal));

            var exporter = new PatchPackageExporter();

            var exportedManifest = await exporter.ExportAsync(package, exportRoot);

            var exportedEntry = exportedManifest.Entries.Single(e => e.FileName == sourceEntry.FileName);
            var exportedPath = Path.Combine(exportRoot, sourceEntry.FileName.Replace('/', Path.DirectorySeparatorChar));
            var compressedPath = exportedPath + ".lz4";
            var decompressedPath = Path.Combine(exportRoot, "resource-check-" + Guid.NewGuid().ToString("N"));

            File.Exists(exportedPath).Should().BeTrue();
            File.Exists(compressedPath).Should().BeTrue();
            FileUtility.GetFileSize(exportedPath).Should().Be(exportedEntry.FileSize);
            (await FileUtility.ComputeMd5Async(exportedPath)).Should().Be(exportedEntry.Checksum);
            FileUtility.GetFileSize(compressedPath).Should().Be(exportedEntry.CompressedFileSize);
            (await FileUtility.ComputeMd5Async(compressedPath)).Should().Be(exportedEntry.CompressedChecksum);

            await FileUtility.DecompressFileAsync(compressedPath, decompressedPath);
            var originalBytes = await File.ReadAllBytesAsync(exportedPath);
            var decompressedBytes = await File.ReadAllBytesAsync(decompressedPath);
            decompressedBytes.Should().Equal(originalBytes);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportAsync_UsesImportedCompressionFlagWhenNoOverrideExists()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-default-compression-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-default-compression-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);

            var exporter = new PatchPackageExporter();

            var exportedManifest = await exporter.ExportAsync(package, exportRoot, new PackageExportOptions());

            var exportedEntry = exportedManifest.Entries.Single(e => e.FileName == "table/us/song_song.csv");
            var tablePath = Path.Combine(exportRoot, "table", "us", "song_song.csv");
            exportedEntry.Compressed.Should().BeTrue();
            File.Exists(tablePath).Should().BeTrue();
            File.Exists(tablePath + ".lz4").Should().BeTrue();
            exportedEntry.CompressedFileSize.Should().BeGreaterThan(0);
            exportedEntry.CompressedChecksum.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportAsync_CanOverrideTableToUncompressed()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-table-uncompressed-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-table-uncompressed-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);
            var options = new PackageExportOptions();
            options.SetCompression("table/us/song_song.csv", compressed: false);

            var exporter = new PatchPackageExporter();

            var exportedManifest = await exporter.ExportAsync(package, exportRoot, options);

            var exportedEntry = exportedManifest.Entries.Single(e => e.FileName == "table/us/song_song.csv");
            var tablePath = Path.Combine(exportRoot, "table", "us", "song_song.csv");
            exportedEntry.Compressed.Should().BeFalse();
            exportedEntry.CompressedFileSize.Should().Be(0);
            exportedEntry.CompressedChecksum.Should().BeEmpty();
            File.Exists(tablePath).Should().BeTrue();
            File.Exists(tablePath + ".lz4").Should().BeFalse();
            FileUtility.GetFileSize(tablePath).Should().Be(exportedEntry.FileSize);
            (await FileUtility.ComputeMd5Async(tablePath)).Should().Be(exportedEntry.Checksum);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportAsync_CanOverrideResourceToUncompressed()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-resource-uncompressed-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-resource-uncompressed-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);
            var resourceEntry = package.Resources.First(r =>
                r.Compressed && r.FileName.StartsWith("preview/", StringComparison.Ordinal));
            var options = new PackageExportOptions();
            options.SetCompression(resourceEntry.FileName, compressed: false);

            var exporter = new PatchPackageExporter();

            var exportedManifest = await exporter.ExportAsync(package, exportRoot, options);

            var exportedEntry = exportedManifest.Entries.Single(e => e.FileName == resourceEntry.FileName);
            var resourcePath = Path.Combine(exportRoot, resourceEntry.FileName.Replace('/', Path.DirectorySeparatorChar));
            exportedEntry.Compressed.Should().BeFalse();
            exportedEntry.CompressedFileSize.Should().Be(0);
            exportedEntry.CompressedChecksum.Should().BeEmpty();
            File.Exists(resourcePath).Should().BeTrue();
            File.Exists(resourcePath + ".lz4").Should().BeFalse();
            FileUtility.GetFileSize(resourcePath).Should().Be(exportedEntry.FileSize);
            (await FileUtility.ComputeMd5Async(resourcePath)).Should().Be(exportedEntry.Checksum);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportAsync_ValidatesMixedCompressionPolicy()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-mixed-compression-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-mixed-compression-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);
            var resourceEntry = package.Resources.First(r =>
                r.Compressed && r.FileName.StartsWith("preview/", StringComparison.Ordinal));
            var options = new PackageExportOptions();
            options.SetCompression("table/us/song_song.csv", compressed: false);
            options.SetCompression(resourceEntry.FileName, compressed: false);

            var exporter = new PatchPackageExporter();

            var exportedManifest = await exporter.ExportAsync(package, exportRoot, options);
            var validator = new PatchPackageValidator();
            var validation = await validator.ValidateAsync(exportedManifest, exportRoot);

            validation.Errors.Should().BeEmpty();
            validation.IsValid.Should().BeTrue();

            var uncompressedTable = exportedManifest.Entries.Single(e => e.FileName == "table/us/song_song.csv");
            uncompressedTable.Compressed.Should().BeFalse();
            uncompressedTable.CompressedFileSize.Should().Be(0);
            uncompressedTable.CompressedChecksum.Should().BeEmpty();
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_song.csv.lz4")).Should().BeFalse();

            var uncompressedResource = exportedManifest.Entries.Single(e => e.FileName == resourceEntry.FileName);
            uncompressedResource.Compressed.Should().BeFalse();
            uncompressedResource.CompressedFileSize.Should().Be(0);
            uncompressedResource.CompressedChecksum.Should().BeEmpty();
            File.Exists(Path.Combine(exportRoot, resourceEntry.FileName.Replace('/', Path.DirectorySeparatorChar)) + ".lz4")
                .Should().BeFalse();

            var stillCompressedTable = exportedManifest.Entries.Single(e => e.FileName == "table/us/song_songPattern.csv");
            stillCompressedTable.Compressed.Should().BeTrue();
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_songPattern.csv.lz4")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "DMTQ-Tools.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing DMTQ-Tools.sln.");
    }
}
