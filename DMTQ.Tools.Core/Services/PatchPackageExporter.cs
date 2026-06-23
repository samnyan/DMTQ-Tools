using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageExporter
{
    public Task<PatchManifest> ExportAsync(
        PatchPackage package,
        string exportRoot,
        CancellationToken cancellationToken = default)
        => ExportAsync(package, exportRoot, new PackageExportOptions(), cancellationToken);

    public async Task<PatchManifest> ExportAsync(
        PatchPackage package,
        string exportRoot,
        PackageExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(exportRoot);
        var exportedManifest = new PatchManifest();

        // Collect all table paths from raw GameTables and entity-backed tables
        var tablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables.Tables)
        {
            tablePaths.Add(table.PackageRelativePath);
        }

        // Add entity-backed table paths
        AddEntityTablePaths(package, tablePaths);

        // Export CSV tables
        foreach (var relativePath in tablePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uncompressedPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(uncompressedPath) ?? exportRoot);

            await using (var stream = File.Create(uncompressedPath))
            {
                if (!ExportSchemaRegistry.TryWriteTable(relativePath, package, stream))
                {
                    // Fallback for non-entity tables that were imported as raw GameTables
                    var table = package.Tables.Tables.Single(t => t.PackageRelativePath == relativePath);
                    WriteGameTableToStream(table, stream);
                }
            }

            // Determine compression: check if we have a ResourceFile for this table path
            var resourceForTable = package.Resources.FirstOrDefault(r =>
                r.FileName.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
            var defaultCompressed = resourceForTable?.Compressed ?? true;
            var shouldCompress = options.CompressionOverrides.TryGetValue(
                FileUtility.NormalizePackageRelativePath(relativePath), out var overrideCompressed)
                ? overrideCompressed
                : defaultCompressed;

            var compressedPath = shouldCompress ? uncompressedPath + ".lz4" : null;
            if (compressedPath is not null)
            {
                await FileUtility.CompressFileAsync(uncompressedPath, compressedPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            exportedManifest.Entries.Add(await CreateExportEntryAsync(
                relativePath,
                uncompressedPath,
                compressedPath,
                shouldCompress,
                resourceForTable?.AcquireOnDemand ?? 0,
                cancellationToken).ConfigureAwait(false));
        }

        // Export resources (non-table files)
        var projectRoot = package.ProjectInfo.ProjectRoot;
        foreach (var resource in package.Resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = FileUtility.NormalizePackageRelativePath(resource.FileName);

            // Compute archive path from resource FileName
            var archivedPath = Path.Combine(projectRoot, "resources", relativePath.Replace('/', Path.DirectorySeparatorChar));
            var exportPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? exportRoot);

            if (File.Exists(archivedPath))
            {
                File.Copy(archivedPath, exportPath, overwrite: true);
            }
            else
            {
                // Try platform-specific path
                archivedPath = Path.Combine(projectRoot, "resources", "android", relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(archivedPath))
                    archivedPath = Path.Combine(projectRoot, "resources", "ios", relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(archivedPath))
                    File.Copy(archivedPath, exportPath, overwrite: true);
            }

            var shouldCompress = options.CompressionOverrides.TryGetValue(
                FileUtility.NormalizePackageRelativePath(resource.FileName), out var resOverrideCompressed)
                ? resOverrideCompressed
                : resource.Compressed;

            var compressedPath = shouldCompress ? exportPath + ".lz4" : null;
            if (compressedPath is not null && File.Exists(exportPath))
            {
                await FileUtility.CompressFileAsync(exportPath, compressedPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            exportedManifest.Entries.Add(await CreateExportEntryAsync(
                relativePath,
                exportPath,
                compressedPath,
                shouldCompress,
                resource.AcquireOnDemand,
                cancellationToken).ConfigureAwait(false));
        }

        var manifestPath = Path.Combine(exportRoot, "patch_new.csv");
        await using (var manifestStream = File.Create(manifestPath))
        {
            await PatchManifestIO.WriteAsync(exportedManifest, manifestStream, cancellationToken).ConfigureAwait(false);
        }

        await FileUtility.CompressFileAsync(manifestPath, manifestPath + ".lz4", cancellationToken).ConfigureAwait(false);
        return exportedManifest;
    }

    private static void AddEntityTablePaths(PatchPackage package, HashSet<string> tablePaths)
    {
        var languages = new[] { "cn", "jp", "kr", "tw", "us" };

        if (package.Songs.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/song_song.csv");
                tablePaths.Add($"table/{lang}/song_songPattern.csv");
                tablePaths.Add($"table/{lang}/song_desc_{lang}.csv");
            }
        }
        if (package.Achievements.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/quest_achievement.csv");
                tablePaths.Add($"table/{lang}/acievement_desc_{lang}.csv");
            }
        }
        if (package.Quests.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/quest_desc_{lang}.csv");
                tablePaths.Add($"table/{lang}/quest_mission_desc_{lang}.csv");
            }
        }
        if (package.Products.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/product_product.csv");
            }
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/category_categoryproduct.csv");
            }
        }
        if (package.Items.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/product_item.csv");
                tablePaths.Add($"table/{lang}/item_desc_{lang}.csv");
            }
        }
        if (package.IngameItems.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/ingameitem_ingameitem.csv");
            }
        }
        if (package.IngameItemEffects.Count > 0)
        {
            foreach (var lang in languages)
            {
                tablePaths.Add($"table/{lang}/ingameitem_itemeffect.csv");
            }
        }

        // Shared non-entity tables (now handled as shared resources) are not added here.
    }

    private async Task<PatchFileEntry> CreateExportEntryAsync(
        string relativePath,
        string filePath,
        string? compressedPath,
        bool compressed,
        int acquireOnDemand,
        CancellationToken cancellationToken)
    {
        var fileSize = FileUtility.GetFileSize(filePath);
        var checksum = await FileUtility.ComputeMd5Async(filePath, cancellationToken).ConfigureAwait(false);
        var compressedFileSize = compressedPath is null || !File.Exists(compressedPath)
            ? 0
            : FileUtility.GetFileSize(compressedPath);
        var compressedChecksum = compressedPath is null || !File.Exists(compressedPath)
            ? string.Empty
            : await FileUtility.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);

        return new PatchFileEntry(
            relativePath,
            fileSize,
            checksum,
            compressedFileSize,
            compressedChecksum,
            acquireOnDemand,
            compressed,
            string.Empty,
            string.Empty);
    }

    /// <summary>Minimal CSV writer for non-entity-backed GameTables (legacy fallback).</summary>
    private static void WriteGameTableToStream(GameTable table, Stream stream)
    {
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvHelper.CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(
            System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        var columns = table.Columns.OrderBy(c => c.Order).ToArray();
        foreach (var column in columns)
            csv.WriteField(column.Name);
        csv.NextRecord();

        foreach (var row in table.Rows.OrderBy(r => r.Order))
        {
            foreach (var column in columns)
            {
                var cell = row.Cells.FirstOrDefault(c => c.ColumnName == column.Name);
                csv.WriteField(cell?.Value ?? string.Empty);
            }
            csv.NextRecord();
        }

        writer.Flush();
    }
}
