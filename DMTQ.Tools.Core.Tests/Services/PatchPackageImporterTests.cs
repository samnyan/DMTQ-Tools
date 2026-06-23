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
                new CsvTableReader());

            var package = await importer.ImportAsync(packageRoot, tempProjectRoot);

            package.ProjectInfo.ProjectRoot.Should().Be(tempProjectRoot);
            package.ProjectInfo.SourcePackageRoot.Should().Be(packageRoot);
            package.Songs.Should().NotBeEmpty();
            package.Songs.SelectMany(s => s.Localizations.Keys).Distinct()
                .Should().Contain(["cn", "jp", "kr", "tw", "us"]);
            package.Resources.Should().Contain(r => r.FileName.StartsWith("dlc/", StringComparison.Ordinal));
            package.Resources.Should().Contain(r => r.FileName.StartsWith("preview/", StringComparison.Ordinal));

            var archivedResources = package.Resources
                .Where(r => r.FileName.StartsWith("dlc/", StringComparison.Ordinal)
                    || r.FileName.StartsWith("preview/", StringComparison.Ordinal))
                .Take(5)
                .ToArray();

            archivedResources.Should().NotBeEmpty();
            foreach (var resource in archivedResources)
            {
                var archivedPath = Path.Combine(tempProjectRoot, "resources", resource.FileName.Replace('/', Path.DirectorySeparatorChar));
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
            var importer = new PatchPackageImporter(new CsvTableReader());

            var package = await importer.ImportAsync(packageRoot, tempProjectRoot);
            // Find a compressed dlc resource
            var resource = package.Resources.First(r =>
                r.Compressed && r.FileName.StartsWith("dlc/", StringComparison.Ordinal));
            // Find its manifest entry from one of the PlatformManifest entries
            var platformEntry = resource.PlatformManifest.First();
            var archivedPath = Path.Combine(
                tempProjectRoot,
                "resources",
                resource.FileName.Replace('/', Path.DirectorySeparatorChar));

            FileUtility.GetFileSize(archivedPath).Should().Be(platformEntry.SourceFileSize);
            var archivedChecksum = await FileUtility.ComputeMd5Async(archivedPath);
            archivedChecksum.Should().Be(platformEntry.SourceChecksum);
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
