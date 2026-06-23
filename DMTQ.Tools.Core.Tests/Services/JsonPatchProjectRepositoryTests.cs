using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class JsonPatchProjectRepositoryTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsMinimalPackage()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-json-project-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateMinimalPackage(projectRoot);
            var options = new PackageExportOptions();
            options.SetCompression("table/us/song_song.csv", compressed: false);
            var repository = new JsonPatchProjectRepository();

            await repository.SaveAsync(package, "UncompressAll", options, projectRoot);

            File.Exists(Path.Combine(projectRoot, "project.json")).Should().BeTrue();
            var snapshot = await repository.LoadAsync(projectRoot);

            snapshot.ExportCompressionMode.Should().Be("UncompressAll");
            snapshot.ExportOptions.ShouldCompress(new PatchFileEntry("table/us/song_song.csv", 0, "", 0, "", 0, true, "", "")).Should().BeFalse();
            snapshot.Package.ProjectInfo.ProjectRoot.Should().Be(projectRoot);
            snapshot.Package.Resources.Should().ContainSingle();
            snapshot.Package.Resources[0].FileName.Should().Be("preview/oblivion.p.opus");
            snapshot.Package.Tables.Tables.Should().ContainSingle();
            snapshot.Package.Tables.Tables[0].Rows.Should().ContainSingle();
            snapshot.Package.Tables.Tables[0].Rows[0].Cells.Single(c => c.ColumnName == "name").Value.Should().Be("oblivion");
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_PreservesImportedSamplePackageForExport()
    {
        var repoRoot = FindRepoRoot();
        var packageRoot = Path.Combine(repoRoot, "external", "patch", "phone_new", "1.003.005", "android");
        Directory.Exists(packageRoot).Should().BeTrue("the repository sample package is required for this integration test");

        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-json-import-project-" + Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-json-import-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importer = new PatchPackageImporter(new CsvTableReader());
            var package = await importer.ImportAsync(packageRoot, projectRoot);
            var exportOptions = new PackageExportOptions();
            exportOptions.SetCompression("table/us/song_song.csv", compressed: false);
            var repository = new JsonPatchProjectRepository();

            await repository.SaveAsync(package, "UncompressAll", exportOptions, projectRoot);
            var snapshot = await repository.LoadAsync(projectRoot);

            snapshot.Package.Songs.Should().HaveCount(package.Songs.Count);
            snapshot.Package.Resources.Should().HaveCount(package.Resources.Count);
            snapshot.Package.Songs.Should().NotBeEmpty();

            var exporter = new PatchPackageExporter();
            var exportedManifest = await exporter.ExportAsync(snapshot.Package, exportRoot, snapshot.ExportOptions);
            var validation = await new PatchPackageValidator().ValidateAsync(exportedManifest, exportRoot);

            validation.Errors.Should().BeEmpty();
            exportedManifest.Entries.Count.Should().BeGreaterThan(0);
            File.Exists(Path.Combine(exportRoot, "table", "us", "song_song.csv")).Should().BeTrue();
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

    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsPlatformMetadataAndPreviewInclusion()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dmtq-json-platform-project-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(projectRoot);
            var package = CreateMinimalPackage(projectRoot);
            package.Resources.Clear();
            package.Resources.Add(new ResourceFile
            {
                FileName = "preview/oblivion.p.opus",
                Category = "preview",
                Compressed = false,
                PlatformManifest =
                {
                    new PlatformManifestEntry
                    {
                        Platform = "android",
                        Exist = true,
                        SourceFileSize = 12,
                        SourceChecksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    },
                    new PlatformManifestEntry
                    {
                        Platform = "ios",
                        Exist = true,
                        SourceFileSize = 12,
                        SourceChecksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    }
                }
            });

            var repository = new JsonPatchProjectRepository();

            await repository.SaveAsync(package, "Keep", new PackageExportOptions(), projectRoot);
            var snapshot = await repository.LoadAsync(projectRoot);

            snapshot.Package.Resources.Should().ContainSingle();
            snapshot.Package.Resources[0].FileName.Should().Be("preview/oblivion.p.opus");
            snapshot.Package.Resources[0].PlatformManifest.Should().HaveCount(2);
            snapshot.Package.Resources[0].PlatformManifest.Should().Contain(m => m.Platform == "android");
            snapshot.Package.Resources[0].PlatformManifest.Should().Contain(m => m.Platform == "ios");
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    private static PatchPackage CreateMinimalPackage(string projectRoot)
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo(projectRoot, "source-package", "1.003.005", "android")
        };

        var table = new GameTable
        {
            PackageRelativePath = "table/us/song_song.csv",
            TableName = "song_song",
            LanguageCode = "us"
        };
        table.Columns.Add(new GameTableColumn("song_id", 0));
        table.Columns.Add(new GameTableColumn("name", 1));
        var row = new GameTableRow { Order = 0 };
        row.Cells.Add(new GameTableCell("song_id", "1"));
        row.Cells.Add(new GameTableCell("name", "oblivion"));
        table.Rows.Add(row);
        package.Tables.Tables.Add(table);

        // Also add as entity for schema-based export
        package.Songs.Add(new Song
        {
            Id = 1,
            Name = "oblivion"
        });

        package.Resources.Add(new ResourceFile
        {
            FileName = "preview/oblivion.p.opus",
            Category = "preview",
            Compressed = false
        });

        return package;
    }
}
