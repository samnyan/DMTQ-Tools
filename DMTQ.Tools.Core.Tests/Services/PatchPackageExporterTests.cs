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
            var compression = new Lz4CompressionService();
            var importer = new PatchPackageImporter(compression, new PatchManifestReader(), new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);

            var exporter = new PatchPackageExporter(
                new CsvTableWriter(),
                new PatchManifestWriter(),
                compression,
                new PatchChecksumService());

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
