using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageExporter(
    CsvTableWriter tableWriter,
    PatchManifestWriter manifestWriter,
    Lz4CompressionService compressionService,
    PatchChecksumService checksumService)
{
    public async Task<PatchManifest> ExportAsync(
        PatchPackage package,
        string exportRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);

        Directory.CreateDirectory(exportRoot);
        var exportedManifest = new PatchManifest();

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

                var compressedPath = uncompressedPath + ".lz4";
                await compressionService.CompressFileAsync(uncompressedPath, compressedPath, cancellationToken).ConfigureAwait(false);

                exportedManifest.Entries.Add(await CreateExportEntryAsync(
                    sourceEntry,
                    relativePath,
                    uncompressedPath,
                    compressedPath,
                    compressed: true,
                    cancellationToken).ConfigureAwait(false));
            }
            else
            {
                var resource = package.Resources.Single(r => r.PackageRelativePath == relativePath);
                var archivedPath = Path.Combine(package.ProjectInfo.ProjectRoot, resource.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var exportPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? exportRoot);
                File.Copy(archivedPath, exportPath, overwrite: true);

                var compressedPath = sourceEntry.Compressed ? exportPath + ".lz4" : null;
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
                    sourceEntry.Compressed,
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

    private async Task<PatchFileEntry> CreateExportEntryAsync(
        PatchFileEntry sourceEntry,
        string relativePath,
        string filePath,
        string? compressedPath,
        bool compressed,
        CancellationToken cancellationToken)
    {
        var compressedFilePath = compressedPath ?? filePath;
        return new PatchFileEntry(
            relativePath,
            checksumService.GetFileSize(filePath),
            await checksumService.ComputeMd5Async(filePath, cancellationToken).ConfigureAwait(false),
            checksumService.GetFileSize(compressedFilePath),
            await checksumService.ComputeMd5Async(compressedFilePath, cancellationToken).ConfigureAwait(false),
            sourceEntry.AcquireOnDemand,
            compressed,
            sourceEntry.Platform,
            sourceEntry.Tag);
    }
}
