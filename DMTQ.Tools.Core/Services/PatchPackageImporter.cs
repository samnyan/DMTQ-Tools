using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageImporter(
    Lz4CompressionService compressionService,
    PatchManifestReader manifestReader,
    CsvTableReader tableReader)
{
    public async Task<PatchPackage> ImportAsync(
        string packageRoot,
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var manifestPath = Path.Combine(packageRoot, "patch_new.csv.lz4");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Could not find patch_new.csv.lz4.", manifestPath);
        }

        Directory.CreateDirectory(projectRoot);
        var tempRoot = Path.Combine(projectRoot, "temp", "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestCsvPath = Path.Combine(tempRoot, "patch_new.csv");
            await compressionService.DecompressFileAsync(manifestPath, manifestCsvPath, cancellationToken).ConfigureAwait(false);

            await using var manifestStream = File.OpenRead(manifestCsvPath);
            var manifest = await manifestReader.ReadAsync(manifestStream, cancellationToken).ConfigureAwait(false);

            var package = new PatchPackage
            {
                ProjectInfo = new ProjectInfo(projectRoot, packageRoot, TryGetVersion(packageRoot), TryGetPlatform(packageRoot))
            };
            package.Manifest.Entries.AddRange(manifest.Entries);

            foreach (var entry in manifest.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = PathClassifier.Normalize(entry.FileName);
                var sourcePath = ResolveSourcePath(packageRoot, relativePath, entry.Compressed);

                if (PathClassifier.IsCsvTable(relativePath))
                {
                    var csvPath = await EnsureCsvFileAsync(sourcePath, tempRoot, relativePath, entry.Compressed, cancellationToken)
                        .ConfigureAwait(false);
                    await using var csvStream = File.OpenRead(csvPath);
                    var table = await tableReader.ReadAsync(csvStream, relativePath, cancellationToken).ConfigureAwait(false);
                    package.Tables.Tables.Add(table);
                }
                else
                {
                    var projectRelativePath = Path.Combine("resources", relativePath).Replace('\\', '/');
                    var archivedPath = Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(archivedPath) ?? projectRoot);
                    await using (var source = File.OpenRead(sourcePath))
                    await using (var destination = File.Create(archivedPath))
                    {
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    }

                    package.Resources.Add(new ResourceFile(
                        relativePath,
                        projectRelativePath,
                        PathClassifier.ResourceCategory(relativePath),
                        entry.Compressed,
                        sourcePath));
                }
            }

            return package;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private async Task<string> EnsureCsvFileAsync(
        string sourcePath,
        string tempRoot,
        string relativePath,
        bool compressed,
        CancellationToken cancellationToken)
    {
        if (!compressed)
        {
            return sourcePath;
        }

        var destinationPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        await compressionService.DecompressFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        return destinationPath;
    }

    private static string ResolveSourcePath(string packageRoot, string relativePath, bool compressed)
    {
        var uncompressedPath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var compressedPath = uncompressedPath + ".lz4";
        if (compressed && File.Exists(compressedPath))
        {
            return compressedPath;
        }

        if (File.Exists(uncompressedPath))
        {
            return uncompressedPath;
        }

        if (File.Exists(compressedPath))
        {
            return compressedPath;
        }

        throw new FileNotFoundException($"Could not find package file '{relativePath}'.", uncompressedPath);
    }

    private static string? TryGetVersion(string packageRoot)
    {
        var parent = Directory.GetParent(packageRoot);
        return parent?.Name.Contains('.', StringComparison.Ordinal) == true ? parent.Name : null;
    }

    private static string? TryGetPlatform(string packageRoot)
        => new DirectoryInfo(packageRoot).Name;
}
