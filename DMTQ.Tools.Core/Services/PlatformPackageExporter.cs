using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PlatformPackageExporter(
    CsvTableWriter tableWriter,
    PatchManifestWriter manifestWriter,
    Lz4CompressionService compressionService,
    PatchChecksumService checksumService)
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
        var relativePath = PathClassifier.NormalizePackageRelativePath(baselineEntry.FileName);

        if (PathClassifier.IsCsvTable(relativePath))
        {
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
        await tableWriter.WriteAsync(table, ms, cancellationToken).ConfigureAwait(false);
        var bytes = ms.ToArray();

        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

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
            await compressionService.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedBytes = await File.ReadAllBytesAsync(compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(compressedBytes)).ToLowerInvariant();
            manifestEntry = manifestEntry with
            {
                CompressedFileSize = compressedBytes.Length,
                CompressedChecksum = compressedChecksum
            };
            result.FilesWritten++;
        }

        result.Manifest.Entries.Add(manifestEntry);
    }

    private static async Task TryWriteResourceAsync(
        string projectRoot,
        ResourceFile resource,
        PatchFileEntry baselineEntry,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var sourcePath = resource.SourcePackagePath
            ?? Path.Combine(projectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(sourcePath))
        {
            await HandleMissingEntryAsync(baselineEntry, options, result).ConfigureAwait(false);
            return;
        }

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        if (ShouldSkipAsBaseline(options, baselineEntry, checksum))
        {
            result.Manifest.Entries.Add(baselineEntry);
            result.FilesSkippedAsBaseline++;
            result.Messages.Add($"Skipped unchanged baseline file: {baselineEntry.FileName}");
            return;
        }

        var relativePath = PathClassifier.NormalizePackageRelativePath(resource.PackageRelativePath);
        var destinationPath = Path.Combine(result.ExportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? result.ExportRoot);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        result.FilesWritten++;

        var fileSize = new FileInfo(destinationPath).Length;
        var manifestEntry = new PatchFileEntry(
            relativePath,
            fileSize,
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
            var lz4Service = new Lz4CompressionService();
            await lz4Service.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedBytes = await File.ReadAllBytesAsync(compressedPath, cancellationToken).ConfigureAwait(false);
            var compressedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(compressedBytes)).ToLowerInvariant();
            manifestEntry = manifestEntry with
            {
                CompressedFileSize = compressedBytes.Length,
                CompressedChecksum = compressedChecksum
            };
            result.FilesWritten++;
        }

        result.Manifest.Entries.Add(manifestEntry);
    }

    private async Task ExportProjectOnlyResourcesAsync(
        PatchPackage package,
        string exportRoot,
        PlatformExportOptions options,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var exportedPaths = result.Manifest.Entries
            .Select(entry => PathClassifier.NormalizePackageRelativePath(entry.FileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resources = package.Resources
            .Where(resource => BelongsToPlatform(resource, options.Platform))
            .Where(resource => !exportedPaths.Contains(PathClassifier.NormalizePackageRelativePath(resource.PackageRelativePath)))
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
        var sourcePath = resource.SourcePackagePath
            ?? Path.Combine(projectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(sourcePath))
        {
            result.MissingCurrentFiles++;
            result.Validation.Errors.Add($"Missing current resource file: {resource.PackageRelativePath}");
            return;
        }

        var relativePath = PathClassifier.NormalizePackageRelativePath(resource.PackageRelativePath);
        var destinationPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? exportRoot);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        result.FilesWritten++;

        var fileSize = new FileInfo(destinationPath).Length;
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            await File.ReadAllBytesAsync(destinationPath, cancellationToken).ConfigureAwait(false))).ToLowerInvariant();
        var compressedFileSize = 0L;
        var compressedChecksum = string.Empty;
        if (compressed)
        {
            var compressedPath = destinationPath + ".lz4";
            await compressionService.CompressFileAsync(destinationPath, compressedPath, cancellationToken).ConfigureAwait(false);
            compressedFileSize = new FileInfo(compressedPath).Length;
            compressedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(compressedPath, cancellationToken).ConfigureAwait(false))).ToLowerInvariant();
            result.FilesWritten++;
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

    private async Task WriteManifestAsync(
        string exportRoot,
        PlatformExportResult result,
        CancellationToken cancellationToken)
    {
        var csvPath = Path.Combine(exportRoot, "patch_new.csv");
        await using (var csvStream = File.Create(csvPath))
        {
            await manifestWriter.WriteAsync(result.Manifest, csvStream, cancellationToken).ConfigureAwait(false);
        }

        result.FilesWritten++;

        var lz4Path = Path.Combine(exportRoot, "patch_new.csv.lz4");
        await compressionService.CompressFileAsync(csvPath, lz4Path, cancellationToken).ConfigureAwait(false);
        result.FilesWritten++;
    }
}
