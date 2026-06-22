using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchPackageImporterTests
{
    [TestMethod]
    public async Task ImportAsync_ReadsSampleManifestAndCsvTables()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var tempProjectRoot = Path.Combine(Path.GetTempPath(), "dmtq-import-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempProjectRoot);
        try
        {
            var importer = new PatchPackageImporter(
                new Lz4CompressionService(),
                new PatchManifestReader(),
                new CsvTableReader());

            var package = await importer.ImportAsync(packageRoot, tempProjectRoot);

            package.ProjectInfo.ProjectRoot.Should().Be(tempProjectRoot);
            package.ProjectInfo.SourcePackageRoot.Should().Be(packageRoot);
            package.Manifest.Entries.Should().HaveCount(382);
            package.Tables.Tables.Should().Contain(t => t.PackageRelativePath == "table/us/song_song.csv");
            package.Tables.Tables.Should().Contain(t => t.PackageRelativePath == "table/us/song_songPattern.csv");
            package.Tables.Tables.Should().Contain(t => t.PackageRelativePath == "table/kr/song_desc_kr.csv");
            package.Tables.Tables.Select(t => t.LanguageCode).Where(v => v is not null).Distinct()
                .Should().Contain(["cn", "jp", "kr", "tw", "us"]);
            package.Resources.Should().Contain(r => r.PackageRelativePath.StartsWith("dlc/", StringComparison.Ordinal));
            package.Resources.Should().Contain(r => r.PackageRelativePath.StartsWith("preview/", StringComparison.Ordinal));

            var archivedResources = package.Resources
                .Where(r => r.PackageRelativePath.StartsWith("dlc/", StringComparison.Ordinal)
                    || r.PackageRelativePath.StartsWith("preview/", StringComparison.Ordinal))
                .Take(5)
                .ToArray();

            archivedResources.Should().NotBeEmpty();
            foreach (var resource in archivedResources)
            {
                var archivedPath = Path.Combine(tempProjectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Exists(archivedPath).Should().BeTrue("import should copy resource files into the project archive");
                new FileInfo(archivedPath).Length.Should().BeGreaterThan(0);
            }
        }
        finally
        {
            Directory.Delete(tempProjectRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task ImportAsync_ArchivesCompressedResourcesAsUncompressedBytes()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var tempProjectRoot = Path.Combine(Path.GetTempPath(), "dmtq-import-resource-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempProjectRoot);
        try
        {
            var checksum = new PatchChecksumService();
            var importer = new PatchPackageImporter(
                new Lz4CompressionService(),
                new PatchManifestReader(),
                new CsvTableReader());

            var package = await importer.ImportAsync(packageRoot, tempProjectRoot);
            var entry = package.Manifest.Entries.First(e =>
                e.Compressed && e.FileName.StartsWith("dlc/", StringComparison.Ordinal));
            var resource = package.Resources.Single(r => r.PackageRelativePath == entry.FileName);
            var archivedPath = Path.Combine(
                tempProjectRoot,
                resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

            checksum.GetFileSize(archivedPath).Should().Be(entry.FileSize);
            var archivedChecksum = await checksum.ComputeMd5Async(archivedPath);
            archivedChecksum.Should().Be(entry.Checksum);
        }
        finally
        {
            Directory.Delete(tempProjectRoot, recursive: true);
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
