using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageExporter(
    PatchManifestWriter manifestWriter,
    Lz4CompressionService compressionService,
    PatchChecksumService checksumService)
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

        foreach (var sourceEntry in package.Manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = PathClassifier.NormalizePackageRelativePath(sourceEntry.FileName);

            if (PathClassifier.IsCsvTable(relativePath))
            {
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
