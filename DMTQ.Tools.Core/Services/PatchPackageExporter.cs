using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageExporter(
    CsvTableWriter tableWriter,
    PatchManifestWriter manifestWriter,
    Lz4CompressionService compressionService,
    PatchChecksumService checksumService,
    SongTableProjector songTableProjector)
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

        // Project Song entities back to GameTables for export.
        var existingPaths = package.Tables.Tables
            .Select(t => t.PackageRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projectedTables = songTableProjector.ProjectTables(package)
            .Where(t => !existingPaths.Contains(t.PackageRelativePath))
            .ToArray();
        foreach (var table in projectedTables)
        {
            package.Tables.Tables.Add(table);
        }

        try
        {
            foreach (var sourceEntry in package.Manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = PathClassifier.NormalizePackageRelativePath(sourceEntry.FileName);

            if (PathClassifier.IsCsvTable(relativePath))
            {
                var table = package.Tables.Tables.Single(t => t.PackageRelativePath == relativePath);
                var uncompressedPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(uncompressedPath) ?? exportRoot);

                await using (var stream = File.Create(uncompressedPath))
                {
                    await tableWriter.WriteAsync(table, stream, cancellationToken).ConfigureAwait(false);
                }

                var shouldCompress = options.ShouldCompress(sourceEntry);
                var compressedPath = shouldCompress ? uncompressedPath + ".lz4" : null;
                if (compressedPath is not null)
                {
                    await compressionService.CompressFileAsync(uncompressedPath, compressedPath, cancellationToken)
                        .ConfigureAwait(false);
                }

                exportedManifest.Entries.Add(await CreateExportEntryAsync(
                    sourceEntry,
                    relativePath,
                    uncompressedPath,
                    compressedPath,
                    shouldCompress,
                    cancellationToken).ConfigureAwait(false));
            }
            else
            {
                var resource = package.Resources.Single(r => r.PackageRelativePath == relativePath);
                var archivedPath = Path.Combine(package.ProjectInfo.ProjectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var exportPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? exportRoot);
                File.Copy(archivedPath, exportPath, overwrite: true);

                var shouldCompress = options.ShouldCompress(sourceEntry);
                var compressedPath = shouldCompress ? exportPath + ".lz4" : null;
                if (compressedPath is not null)
                {
                    await compressionService.CompressFileAsync(exportPath, compressedPath, cancellationToken)
                        .ConfigureAwait(false);
                }

                exportedManifest.Entries.Add(await CreateExportEntryAsync(
                    sourceEntry,
                    relativePath,
                    exportPath,
                    compressedPath,
                    shouldCompress,
                    cancellationToken).ConfigureAwait(false));
            }
        }

        var manifestPath = Path.Combine(exportRoot, "patch_new.csv");
        await using (var manifestStream = File.Create(manifestPath))
        {
            await manifestWriter.WriteAsync(exportedManifest, manifestStream, cancellationToken).ConfigureAwait(false);
        }

        await compressionService.CompressFileAsync(manifestPath, manifestPath + ".lz4", cancellationToken).ConfigureAwait(false);
        return exportedManifest;
        }
        finally
        {
            foreach (var table in projectedTables)
            {
                package.Tables.Tables.Remove(table);
            }
        }
    }

    private async Task<PatchFileEntry> CreateExportEntryAsync(
        PatchFileEntry sourceEntry,
        string relativePath,
        string filePath,
        string? compressedPath,
        bool compressed,
        CancellationToken cancellationToken)
    {
        var fileSize = checksumService.GetFileSize(filePath);
        var checksum = await checksumService.ComputeMd5Async(filePath, cancellationToken).ConfigureAwait(false);
        var compressedFileSize = compressedPath is null
            ? 0
            : checksumService.GetFileSize(compressedPath);
        var compressedChecksum = compressedPath is null
            ? string.Empty
            : await checksumService.ComputeMd5Async(compressedPath, cancellationToken).ConfigureAwait(false);

        return new PatchFileEntry(
            relativePath,
            fileSize,
            checksum,
            compressedFileSize,
            compressedChecksum,
            sourceEntry.AcquireOnDemand,
            compressed,
            sourceEntry.Platform,
            sourceEntry.Tag);
    }
}
