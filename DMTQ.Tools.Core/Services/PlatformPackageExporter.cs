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

        var platformRecord = package.Platforms.Single(p =>
            p.Platform.Equals(options.Platform, StringComparison.OrdinalIgnoreCase));
        Directory.CreateDirectory(exportRoot);

        var result = new PlatformExportResult
        {
            Platform = platformRecord.Platform,
            ExportRoot = exportRoot
        };

        foreach (var baselineEntry in platformRecord.BaselineManifestEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExportEntryAsync(package, baselineEntry, exportRoot, options, result, cancellationToken)
                .ConfigureAwait(false);
        }

        await ExportProjectOnlyResourcesAsync(package, exportRoot, options, result, cancellationToken)
            .ConfigureAwait(false);

        await WriteManifestAsync(exportRoot, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task ExportEntryAsync(
        PatchPackage package,
        PatchFileEntry baselineEntry,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var relativePath = FileUtility.NormalizePackageRelativePath(baselineEntry.FileName);

        if (FileUtility.IsCsvTable(relativePath))
        {
            // Try entity-backed schema first
            await using var ms = new MemoryStream();
            if (ExportSchemaRegistry.TryWriteTable(relativePath, package, ms))
            {
                await TryWriteTableFromStreamAsync(ms, baselineEntry, relativePath, exportRoot, options, result, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            // Fallback: look for a raw GameTable
            var table = package.Tables.Tables.FirstOrDefault(t =>
                t.PackageRelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

            if (table is not null)
            {
                await TryWriteTableAsync(table, baselineEntry, relativePath, exportRoot, options, result, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }
        else
        {
            var resource = package.Resources.FirstOrDefault(r =>
                r.PackageRelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
                && (r.Platform == options.Platform
                    || (r.Category == "preview" && r.IncludedPlatforms?.Contains(options.Platform) == true)));

            if (resource is not null)
            {
                await TryWriteResourceAsync(
                        package.ProjectInfo.ProjectRoot,
                        resource,
                        baselineEntry,
                        exportRoot,
                        options,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        await HandleMissingEntryAsync(baselineEntry, options, result).ConfigureAwait(false);
    }

    private async Task TryWriteTableFromStreamAsync(
        MemoryStream writtenStream,
        PatchFileEntry baselineEntry,
        string relativePath,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);

        var bytes = writtenStream.ToArray();

        var checksum = ComputeMd5(bytes);

        if (ShouldSkipAsBaseline(options, baselineEntry, checksum))
        {
            result.Manifest.Entries.Add(baselineEntry);
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped unchanged baseline file: {relativePath}");
            return;
        }

        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;

        var manifestEntry = new PatchFileEntry(
            relativePath,
            bytes.Length,
            checksum,
            baselineEntry.Compressed ? 0 : 0,
            string.Empty,
            baselineEntry.AcquireOnDemand,
            baselineEntry.Compressed,
            baselineEntry.Platform,
            baselineEntry.Tag);

        if (baselineEntry.Compressed)
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

    private async Task TryWriteTableAsync(
        GameTable table,
        PatchFileEntry baselineEntry,
        string relativePath,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);

        await using var ms = new MemoryStream();
        WriteGameTableToStream(table, ms);
        var bytes = ms.ToArray();

        var checksum = ComputeMd5(bytes);

        if (ShouldSkipAsBaseline(options, baselineEntry, checksum))
        {
            result.Manifest.Entries.Add(baselineEntry);
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped unchanged baseline file: {relativePath}");
            return;
        }

        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;

        var manifestEntry = new PatchFileEntry(
            relativePath,
            bytes.Length,
            checksum,
            baselineEntry.Compressed ? 0 : 0,
            string.Empty,
            baselineEntry.AcquireOnDemand,
            baselineEntry.Compressed,
            baselineEntry.Platform,
            baselineEntry.Tag);

        if (baselineEntry.Compressed)
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

    private async Task TryWriteResourceAsync(
        string projectRoot,
        ResourceFile resource,
        PatchFileEntry baselineEntry,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var sourcePath = ResolveResourceSourcePath(projectRoot, resource);

        if (!File.Exists(sourcePath))
        {
            await HandleMissingEntryAsync(baselineEntry, options, result).ConfigureAwait(false);
            return;
        }

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var checksum = ComputeMd5(bytes);

        if (ShouldSkipAsBaseline(options, baselineEntry, checksum))
        {
            if (resource.Compressed == baselineEntry.Compressed)
            {
                result.Manifest.Entries.Add(baselineEntry);
                result.FilesSkippedAsBaseline++;
                result.Messages.Add($"Skipped unchanged baseline file: {baselineEntry.FileName}");
                return;
            }

            // Compression flag changed — write resource with new compression, same content
        }

        var relativePath = FileUtility.NormalizePackageRelativePath(resource.PackageRelativePath);
        await WriteResourceFileAsync(exportRoot, relativePath, bytes, resource.Compressed, baselineEntry, result, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExportProjectOnlyResourcesAsync(
        PatchPackage package,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var exportedPaths = result.Manifest.Entries
            .Select(entry => FileUtility.NormalizePackageRelativePath(entry.FileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resources = package.Resources
            .Where(resource => BelongsToPlatform(resource, options.Platform))
            .Where(resource => !exportedPaths.Contains(FileUtility.NormalizePackageRelativePath(resource.PackageRelativePath)))
            .OrderBy(resource => resource.PackageRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var resource in resources)
        {
            await WriteCurrentResourceAsync(
                    package.ProjectInfo.ProjectRoot,
                    resource,
                    exportRoot,
                    resource.Compressed,
                    acquireOnDemand: 0,
                    platform: string.Empty,
                    tag: string.Empty,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool BelongsToPlatform(ResourceFile resource, string platform)
        => string.Equals(resource.Platform, platform, StringComparison.OrdinalIgnoreCase)
           || (resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase)
               && resource.IncludedPlatforms?.Contains(platform, StringComparer.OrdinalIgnoreCase) == true);

    private async Task WriteCurrentResourceAsync(
        string projectRoot,
        ResourceFile resource,
        string exportRoot,
        bool compressed,
        int acquireOnDemand,
        string platform,
        string tag,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var sourcePath = ResolveResourceSourcePath(projectRoot, resource);

        if (!File.Exists(sourcePath))
        {
            result.MissingCurrentFiles++;
            result.Validation.Errors.Add($"Missing current resource file: {resource.PackageRelativePath}");
            return;
        }

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var relativePath = FileUtility.NormalizePackageRelativePath(resource.PackageRelativePath);

        await WriteResourceFileAsync(exportRoot, relativePath, bytes, compressed, baselineEntry: null, result, cancellationToken)
            .ConfigureAwait(false);

        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var fileSize = FileUtility.GetFileSize(destinationPath);
        var checksum = await FileUtility.ComputeMd5Async(destinationPath, cancellationToken).ConfigureAwait(false);
        var compressedFileSize = 0L;
        var compressedChecksum = string.Empty;
        if (compressed)
        {
            var compressedPath = destinationPath + ".lz4";
            compressedFileSize = FileUtility.GetFileSize(compressedPath);
            compressedChecksum = await FileUtility.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);
            // File was already written and compressed by WriteResourceFileAsync
        }

        result.Manifest.Entries.Add(new PatchFileEntry(
            relativePath,
            fileSize,
            checksum,
            compressedFileSize,
            compressedChecksum,
            acquireOnDemand,
            compressed,
            platform,
            tag));
    }

    private async Task WriteResourceFileAsync(
        string exportRoot,
        string relativePath,
        byte[] bytes,
        bool compressed,
        PatchFileEntry? baselineEntry,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);
        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;

        if (compressed)
        {
            var compressedPath = destinationPath + ".lz4";
            await FileUtility.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            result.FilesWritten++;
        }

        if (baselineEntry is not null)
        {
            var fileSize = FileUtility.GetFileSize(destinationPath);
            var checksum = await FileUtility.ComputeMd5Async(destinationPath, cancellationToken).ConfigureAwait(false);
            var manifestEntry = new PatchFileEntry(
                relativePath,
                fileSize,
                checksum,
                baselineEntry.Compressed ? 0 : 0,
                string.Empty,
                baselineEntry.AcquireOnDemand,
                compressed,
                baselineEntry.Platform,
                baselineEntry.Tag);

            if (compressed)
            {
                var compressedPath = destinationPath + ".lz4";
                var compressedFileSize = FileUtility.GetFileSize(compressedPath);
                var compressedChecksum = await FileUtility.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);
                manifestEntry = manifestEntry with
                {
                    CompressedFileSize = compressedFileSize,
                    CompressedChecksum = compressedChecksum
                };
            }

            result.Manifest.Entries.Add(manifestEntry);
        }
    }

    private static string ResolveResourceSourcePath(string projectRoot, ResourceFile resource)
        => Path.Combine(projectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static Task HandleMissingEntryAsync(
        PatchFileEntry baselineEntry,
        PlatformExportOptions options,
        PlatformExportResult result)
    {
        if (options.Mode == PlatformExportMode.Delta)
        {
            result.Manifest.Entries.Add(baselineEntry);
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped baseline-only file: {baselineEntry.FileName}");
        }
        else
        {
            result.Manifest.Entries.Add(baselineEntry);
            result.MissingCurrentFiles++;
            result.Validation.Errors.Add($"Missing current file for full export: {baselineEntry.FileName}");
        }

        return Task.CompletedTask;
    }

    private static bool ShouldSkipAsBaseline(
        PlatformExportOptions options,
        PatchFileEntry baselineEntry,
        string currentChecksum)
        => options.Mode == PlatformExportMode.Delta
           && !string.IsNullOrWhiteSpace(baselineEntry.Checksum)
           && string.Equals(currentChecksum, baselineEntry.Checksum, StringComparison.OrdinalIgnoreCase);

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
