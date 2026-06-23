using System.Globalization;
using System.Security.Cryptography;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class PlatformPackageExporter
{
    public async Task<PlatformExportResult> ExportPlatformAsync(
        PatchPackage package,
        string exportRoot,
        PlatformExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(exportRoot);

        var result = new PlatformExportResult
        {
            Platform = options.Platform,
            ExportRoot = exportRoot
        };

        // ── Export entity-backed CSV tables ──
        await ExportEntityTablesAsync(package, exportRoot, options, result, cancellationToken)
            .ConfigureAwait(false);

        // ── Export non-entity tables from raw GameTables ──
        foreach (var table in package.Tables.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = table.PackageRelativePath;
            await using var ms = new MemoryStream();
            WriteGameTableToStream(table, ms);
            ms.Position = 0;
            await WriteTableBytesAsync(ms, relativePath, exportRoot, null, options, result, cancellationToken)
                .ConfigureAwait(false);
        }

        // ── Export resources (non-table files) for this platform ──
        var platformResources = package.Resources
            .Where(r => r.PlatformManifest.Any(m =>
                m.Platform.Equals(options.Platform, StringComparison.OrdinalIgnoreCase)
                || m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var resource in platformResources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = FileUtility.NormalizePackageRelativePath(resource.FileName);

            if (FileUtility.IsCsvTable(relativePath))
            {
                // Table files are handled by ExportEntityTablesAsync above; skip here
                continue;
            }

            await ExportResourceFileAsync(
                package.ProjectInfo.ProjectRoot,
                resource,
                relativePath,
                exportRoot,
                options,
                result,
                cancellationToken)
                .ConfigureAwait(false);
        }

        await WriteManifestAsync(exportRoot, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task ExportEntityTablesAsync(
        PatchPackage package,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        // Always export tables for all 5 languages
        var languages = new[] { "cn", "jp", "kr", "tw", "us" };

        foreach (var lang in languages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] tablePaths =
            [
                $"table/{lang}/song_song.csv",
                $"table/{lang}/song_songPattern.csv",
                $"table/{lang}/song_desc_{lang}.csv",
                $"table/{lang}/quest_achievement.csv",
                $"table/{lang}/acievement_desc_{lang}.csv",
                $"table/{lang}/quest_desc_{lang}.csv",
                $"table/{lang}/quest_mission_desc_{lang}.csv",
                $"table/{lang}/product_product.csv",
                $"table/{lang}/category_categoryproduct.csv",
                $"table/{lang}/product_item.csv",
                $"table/{lang}/item_desc_{lang}.csv",
                $"table/{lang}/ingameitem_ingameitem.csv",
                $"table/{lang}/ingameitem_itemeffect.csv",
            ];

            foreach (var relativePath in tablePaths)
            {
                await using var ms = new MemoryStream();
                if (ExportSchemaRegistry.TryWriteTable(relativePath, package, ms))
                {
                    await WriteTableBytesAsync(ms, relativePath, exportRoot, null, options, result, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private async Task WriteTableBytesAsync(
        MemoryStream writtenStream,
        string relativePath,
        string exportRoot,
        ResourceFile? resource,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);

        var bytes = writtenStream.ToArray();
        var checksum = ComputeMd5(bytes);

        // Find the platform manifest entry for checksum comparison (only when resource exists)
        var platformEntry = resource?.PlatformManifest.FirstOrDefault(m =>
            m.Platform.Equals(options.Platform, StringComparison.OrdinalIgnoreCase)
            || m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase));

        if (options.Mode == PlatformExportMode.Delta
            && platformEntry is not null
            && !string.IsNullOrWhiteSpace(platformEntry.SourceChecksum)
            && string.Equals(checksum, platformEntry.SourceChecksum, StringComparison.OrdinalIgnoreCase))
        {
            // Unchanged — add baseline entry to manifest
            result.Manifest.Entries.Add(new PatchFileEntry(
                relativePath,
                platformEntry.SourceFileSize,
                platformEntry.SourceChecksum,
                platformEntry.SourceCompressedFileSize,
                platformEntry.SourceCompressedChecksum,
                resource.AcquireOnDemand,
                resource.Compressed,
                string.Empty,
                string.Empty));
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped unchanged baseline file: {relativePath}");
            return;
        }

        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;

        var acquireOnDemand = resource?.AcquireOnDemand ?? 0;
        var compressed = resource?.Compressed ?? true;

        var manifestEntry = new PatchFileEntry(
            relativePath,
            bytes.Length,
            checksum,
            compressed ? 0 : 0,
            string.Empty,
            acquireOnDemand,
            compressed,
            string.Empty,
            string.Empty);

        if (compressed)
        {
            var compressedPath = destinationPath + ".lz4";
            await FileUtility.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedChecksum = await FileUtility.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedFileSize = FileUtility.GetFileSize(compressedPath);
            manifestEntry = manifestEntry with
            {
                CompressedFileSize = compressedFileSize,
                CompressedChecksum = compressedChecksum
            };
            result.FilesWritten++;
        }

        result.Manifest.Entries.Add(manifestEntry);
    }

    private async Task ExportResourceFileAsync(
        string projectRoot,
        ResourceFile resource,
        string relativePath,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        // Resolve source path: try platform-specific path first, then generic
        var sourcePath = ResolveResourceSourcePath(projectRoot, resource.FileName, options.Platform);

        if (!File.Exists(sourcePath))
        {
            if (options.Mode == PlatformExportMode.Delta)
            {
                // Add baseline entry for missing file
                var platformEntry = resource.PlatformManifest.FirstOrDefault(m =>
                    m.Platform.Equals(options.Platform, StringComparison.OrdinalIgnoreCase)
                    || m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase));
                if (platformEntry is not null)
                {
                    result.Manifest.Entries.Add(new PatchFileEntry(
                        relativePath,
                        platformEntry.SourceFileSize,
                        platformEntry.SourceChecksum,
                        platformEntry.SourceCompressedFileSize,
                        platformEntry.SourceCompressedChecksum,
                        resource.AcquireOnDemand,
                        resource.Compressed,
                        string.Empty,
                        string.Empty));
                    result.FilesSkippedAsBaseline++;
                    result.Messages.Add($"Skipped baseline-only file: {relativePath}");
                }
            }
            else
            {
                result.MissingCurrentFiles++;
                result.Validation.Errors.Add($"Missing current file for full export: {relativePath}");
            }
            return;
        }

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var checksum = ComputeMd5(bytes);

        // Check for delta skip
        var platformEntry2 = resource.PlatformManifest.FirstOrDefault(m =>
            m.Platform.Equals(options.Platform, StringComparison.OrdinalIgnoreCase)
            || m.Platform.Equals("share", StringComparison.OrdinalIgnoreCase));

        if (options.Mode == PlatformExportMode.Delta
            && platformEntry2 is not null
            && !string.IsNullOrWhiteSpace(platformEntry2.SourceChecksum)
            && string.Equals(checksum, platformEntry2.SourceChecksum, StringComparison.OrdinalIgnoreCase)
            && resource.Compressed == platformEntry2.SourceCompressedFileSize > 0)
        {
            result.Manifest.Entries.Add(new PatchFileEntry(
                relativePath,
                platformEntry2.SourceFileSize,
                platformEntry2.SourceChecksum,
                platformEntry2.SourceCompressedFileSize,
                platformEntry2.SourceCompressedChecksum,
                resource.AcquireOnDemand,
                resource.Compressed,
                string.Empty,
                string.Empty));
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped unchanged baseline file: {relativePath}");
            return;
        }

        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);
        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;

        var manifestEntry = new PatchFileEntry(
            relativePath,
            bytes.Length,
            checksum,
            resource.Compressed ? 0 : 0,
            string.Empty,
            resource.AcquireOnDemand,
            resource.Compressed,
            string.Empty,
            string.Empty);

        if (resource.Compressed)
        {
            var compressedPath = destinationPath + ".lz4";
            await FileUtility.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedChecksum = await FileUtility.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedFileSize = FileUtility.GetFileSize(compressedPath);
            manifestEntry = manifestEntry with
            {
                CompressedFileSize = compressedFileSize,
                CompressedChecksum = compressedChecksum
            };
            result.FilesWritten++;
        }

        result.Manifest.Entries.Add(manifestEntry);
    }

    private static string ResolveResourceSourcePath(string projectRoot, string fileName, string? targetPlatform = null)
    {
        var candidates = new List<string>();

        // Platform-specific path first (if target platform is known)
        if (!string.IsNullOrWhiteSpace(targetPlatform))
            candidates.Add(Path.Combine(projectRoot, "resources", targetPlatform, fileName.Replace('/', Path.DirectorySeparatorChar)));

        // Generic resources path
        candidates.Add(Path.Combine(projectRoot, "resources", fileName.Replace('/', Path.DirectorySeparatorChar)));

        // Other platform paths as fallback
        if (!string.Equals(targetPlatform, "android", StringComparison.OrdinalIgnoreCase))
            candidates.Add(Path.Combine(projectRoot, "resources", "android", fileName.Replace('/', Path.DirectorySeparatorChar)));
        if (!string.Equals(targetPlatform, "ios", StringComparison.OrdinalIgnoreCase))
            candidates.Add(Path.Combine(projectRoot, "resources", "ios", fileName.Replace('/', Path.DirectorySeparatorChar)));

        // Raw project root path
        candidates.Add(Path.Combine(projectRoot, fileName.Replace('/', Path.DirectorySeparatorChar)));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return candidates[0];
    }

    private static string ComputeMd5(byte[] bytes)
        => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    /// <summary>Minimal CSV writer for non-entity-backed GameTables (legacy fallback).</summary>
    private static void WriteGameTableToStream(GameTable table, Stream stream)
    {
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
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

    private async Task WriteManifestAsync(
        string exportRoot,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var csvPath = Path.Combine(exportRoot, "patch_new.csv");
        await using (var csvStream = File.Create(csvPath))
        {
            await PatchManifestIO.WriteAsync(result.Manifest, csvStream, cancellationToken).ConfigureAwait(false);
        }

        result.FilesWritten++;

        var lz4Path = Path.Combine(exportRoot, "patch_new.csv.lz4");
        await FileUtility.CompressFileAsync(csvPath, lz4Path, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;
    }
}
