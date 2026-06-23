using System.Text.RegularExpressions;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed partial class PackageQaService
{
    public QaReport Run(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var report = new QaReport();

        CheckManifest(package, report);
        CheckLanguageTableVariants(package, report);
        CheckSongPreviewResources(package, report);
        CheckSongProductItemLinks(package, report);
        CheckArchiveFiles(package, report);

        return report;
    }

    private static void CheckManifest(PatchPackage package, QaReport report)
    {
        if (package.Resources.Count == 0 && package.Tables.Tables.Count == 0)
        {
            report.Issues.Add(new QaIssue
            {
                Category = "Manifest",
                Severity = QaIssueSeverity.Warning,
                Message = "Package has no resources or tables. Export will produce an empty manifest."
            });
        }
    }

    private static void CheckLanguageTableVariants(PatchPackage package, QaReport report)
    {
        var importedLanguages = package.Tables.Tables
            .Select(table => table.LanguageCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Collect languages from ResourceFile entries that are CSV tables
        var baselineLanguages = package.Resources
            .Where(r => FileUtility.IsCsvTable(r.FileName))
            .Select(r => ExtractLanguageFromPath(r.FileName))
            .Where(lang => lang is not null)
            .Select(lang => lang!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = baselineLanguages.Except(importedLanguages, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var language in missing)
        {
            report.Issues.Add(new QaIssue
            {
                Category = "Tables",
                Severity = QaIssueSeverity.Warning,
                Message = $"Language '{language}' from baseline has no matching table data."
            });
        }

        foreach (var baseName in new[] { "song_desc", "item_desc" })
        {
            var languagesWithTable = package.Tables.Tables
                .Where(table => table.TableName.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase))
                .Select(table => table.LanguageCode ?? ExtractLanguageSuffix(table.TableName))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allLanguages = package.Tables.Tables
                .Select(table => table.LanguageCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .Concat(package.Resources
                    .Where(r => FileUtility.IsCsvTable(r.FileName))
                    .Select(r => ExtractLanguageFromPath(r.FileName))
                    .Where(lang => lang is not null)
                    .Select(lang => lang!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var language in allLanguages.Except(languagesWithTable, StringComparer.OrdinalIgnoreCase))
            {
                report.Issues.Add(new QaIssue
                {
                    Category = "Tables",
                    Severity = QaIssueSeverity.Warning,
                    Message = $"Localized table '{baseName}' is missing language '{language}'."
                });
            }
        }
    }

    private static void CheckSongPreviewResources(PatchPackage package, QaReport report)
    {
        var resourcePaths = package.Resources
            .Select(resource => resource.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var table in package.Tables.Tables.Where(table =>
            table.TableName.Equals("song_song", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var row in table.Rows)
            {
                var songId = GetCell(row, "song_id");
                var preview = GetCell(row, "preview", "preview_path", "preview_file");
                if (!string.IsNullOrWhiteSpace(preview) && !resourcePaths.Contains(preview))
                {
                    report.Issues.Add(new QaIssue
                    {
                        Category = "Songs",
                        Severity = QaIssueSeverity.Warning,
                        Message = $"Song '{songId}' references preview '{preview}' which is not in project resources.",
                        Detail = $"Add the preview file via the Resources page, or update the song's preview field."
                    });
                }
            }
        }
    }

    private static void CheckSongProductItemLinks(PatchPackage package, QaReport report)
    {
        var songProducts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables.Tables.Where(table =>
            table.TableName.Equals("product_product", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var row in table.Rows)
            {
                var songId = GetCell(row, "song_id");
                var productId = GetCell(row, "product_id");
                if (!string.IsNullOrWhiteSpace(songId) && !string.IsNullOrWhiteSpace(productId))
                {
                    if (!songProducts.TryGetValue(songId, out var list))
                    {
                        list = [];
                        songProducts[songId] = list;
                    }
                    list.Add(productId);
                }
            }
        }

        var productItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables.Tables.Where(table =>
            table.TableName.Equals("product_item", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var row in table.Rows)
            {
                var productId = GetCell(row, "product_id");
                if (!string.IsNullOrWhiteSpace(productId))
                    productItems.Add(productId);
            }
        }

        foreach (var (songId, products) in songProducts)
        {
            foreach (var productId in products)
            {
                if (!productItems.Contains(productId))
                {
                    report.Issues.Add(new QaIssue
                    {
                        Category = "Songs",
                        Severity = QaIssueSeverity.Warning,
                        Message = $"Song '{songId}' product {productId} has no matching product_item row."
                    });
                }
            }
        }
    }

    private static void CheckArchiveFiles(PatchPackage package, QaReport report)
    {
        var projectRoot = package.ProjectInfo.ProjectRoot;

        foreach (var resource in package.Resources)
        {
            // Try several possible archive paths
            var candidates = new[]
            {
                Path.Combine(projectRoot, "resources", resource.FileName.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(projectRoot, "resources", "android", resource.FileName.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(projectRoot, "resources", "ios", resource.FileName.Replace('/', Path.DirectorySeparatorChar)),
            };
            var archivePath = candidates.FirstOrDefault(File.Exists);
            if (archivePath is null)
            {
                report.Issues.Add(new QaIssue
                {
                    Category = "Resources",
                    Severity = QaIssueSeverity.Error,
                    Message = $"Resource '{resource.FileName}' archive file missing."
                });
            }
        }
    }

    private static string GetCell(GameTableRow row, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            var value = row.Cells.FirstOrDefault(cell =>
                cell.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return string.Empty;
    }

    private static string? ExtractLanguageFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var lastUnderscore = fileName.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore >= fileName.Length - 1)
            return null;
        return fileName[(lastUnderscore + 1)..];
    }

    private static string ExtractLanguageSuffix(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? string.Empty : tableName[(index + 1)..];
    }

    [GeneratedRegex("^(?<base>.+)_(?<lang>[a-z]{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalizedTableRegex();
}
