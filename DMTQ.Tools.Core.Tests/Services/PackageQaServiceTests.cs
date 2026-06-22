using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PackageQaServiceTests
{
    [TestMethod]
    public void Run_WarnsOnEmptyManifest()
    {
        var package = CreatePackage();
        var report = new PackageQaService().Run(package);
        report.Issues.Should().Contain(issue =>
            issue.Category == "Manifest"
            && issue.Severity == QaIssueSeverity.Warning
            && issue.Message.Contains("no manifest entries"));
    }

    [TestMethod]
    public void Run_DetectsMissingLanguageVariant()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us",
            ["song_id", "title"], ["1", "Title"]));
        // jp variant is missing
        package.Platforms.Add(new PlatformPackageRecord
        {
            Platform = "android",
            SourcePackageRoot = "source",
            BaselineManifestEntries =
            {
                new PatchFileEntry("table/us/song_desc_us.csv", 10, "aaaa", 0, "", 0, false, "", ""),
                new PatchFileEntry("table/jp/song_desc_jp.csv", 10, "bbbb", 0, "", 0, false, "", "")
            }
        });

        var report = new PackageQaService().Run(package);
        report.Issues.Should().Contain(issue =>
            issue.Category == "Tables"
            && issue.Severity == QaIssueSeverity.Warning
            && issue.Message.Contains("song_desc")
            && issue.Message.Contains("jp"));
    }

    [TestMethod]
    public void Run_DetectsSongPreviewMissingFromResources()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id", "preview"], ["1001", "preview/missing.opus"]));
        // preview resource not in package.Resources

        var report = new PackageQaService().Run(package);
        report.Issues.Should().Contain(issue =>
            issue.Category == "Songs"
            && issue.Severity == QaIssueSeverity.Warning
            && issue.Message.Contains("preview/missing.opus")
            && issue.Message.Contains("1001"));
    }

    [TestMethod]
    public void Run_DetectsMissingArchiveFile()
    {
        var package = CreatePackage("non-existent-project-root");
        package.Resources.Add(new ResourceFile("dlc/test.bin", "resources/android/dlc/test.bin", "dlc", false, null, "android"));

        var report = new PackageQaService().Run(package);
        report.Issues.Should().Contain(issue =>
            issue.Category == "Resources"
            && issue.Severity == QaIssueSeverity.Error
            && issue.Message.Contains("dlc/test.bin")
            && issue.Message.Contains("archive file missing"));
    }

    [TestMethod]
    public void Run_ReportsNoIssuesForCleanPackage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dmtq-qa-clean-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "resources", "android", "dlc"));
            File.WriteAllText(Path.Combine(tempDir, "resources", "android", "dlc", "test.bin"), "data");

            var package = CreatePackage(tempDir);
            package.Manifest.Entries.Add(new PatchFileEntry("dlc/test.bin", 4, "aaaa", 0, "", 0, false, "", ""));
            package.Resources.Add(new ResourceFile("dlc/test.bin", "resources/android/dlc/test.bin", "dlc", false, null, "android"));

            var report = new PackageQaService().Run(package);
            report.IsClean.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Run_DetectsProductWithoutItemLink()
    {
        var package = CreatePackage();
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us",
            ["song_id"], ["1001"]));
        package.Tables.Tables.Add(CreateTable("table/us/product_product.csv", "product_product", "us",
            ["product_id", "song_id"], ["5001", "1001"]));
        // product_item table is missing — product 5001 has no items

        var report = new PackageQaService().Run(package);
        report.Issues.Should().Contain(issue =>
            issue.Category == "Songs"
            && issue.Severity == QaIssueSeverity.Warning
            && issue.Message.Contains("product 5001")
            && issue.Message.Contains("item"));
    }

    private static PatchPackage CreatePackage(string? projectRoot = null)
        => new()
        {
            ProjectInfo = new ProjectInfo(projectRoot ?? "project", null, "1.0", null)
        };

    private static GameTable CreateTable(string path, string tableName, string languageCode, string[] columns, params string[][] rows)
    {
        var table = new GameTable { PackageRelativePath = path, TableName = tableName, LanguageCode = languageCode };
        for (var i = 0; i < columns.Length; i++)
            table.Columns.Add(new GameTableColumn(columns[i], i));
        for (var r = 0; r < rows.Length; r++)
        {
            var row = new GameTableRow { Order = r };
            for (var c = 0; c < columns.Length; c++)
                row.Cells.Add(new GameTableCell(columns[c], rows[r][c]));
            table.Rows.Add(row);
        }
        return table;
    }
}
