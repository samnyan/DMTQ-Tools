using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PlatformPackageImporter(
    Lz4CompressionService compressionService,
    PatchManifestReader manifestReader,
    CsvTableReader tableReader,
    SongCatalogService songCatalogService)
{
    public async Task ImportPlatformAsync(
        PatchPackage package,
        string packageRoot,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        var manifestPath = Path.Combine(packageRoot, "patch_new.csv.lz4");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Could not find patch_new.csv.lz4.", manifestPath);
        }

        var projectRoot = package.ProjectInfo.ProjectRoot;
        var tempRoot = Path.Combine(projectRoot, "temp", "platform-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestCsvPath = Path.Combine(tempRoot, "patch_new.csv");
            await compressionService.DecompressFileAsync(manifestPath, manifestCsvPath, cancellationToken).ConfigureAwait(false);

            await using var manifestStream = File.OpenRead(manifestCsvPath);
            var manifest = await manifestReader.ReadAsync(manifestStream, cancellationToken).ConfigureAwait(false);

            var record = new PlatformPackageRecord
            {
                Platform = platform,
                SourcePackageRoot = packageRoot,
                Version = TryGetVersion(packageRoot)
            };
            record.BaselineManifestEntries.AddRange(manifest.Entries);

            var preExistingTablePaths = package.Tables.Tables
                .Select(table => table.PackageRelativePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in manifest.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = PathClassifier.NormalizePackageRelativePath(entry.FileName);
                var sourcePath = TryResolveSourcePath(packageRoot, relativePath, entry.Compressed);

                if (sourcePath is null)
                {
                    record.MissingPhysicalFileCount++;
                    continue;
                }

                if (PathClassifier.IsCsvTable(relativePath))
                {
                    if (preExistingTablePaths.Contains(relativePath))
                    {
                        record.ImportedTableFileCount++;
                        continue;
                    }

                    var csvPath = await EnsureCsvFileAsync(sourcePath, tempRoot, relativePath, entry.Compressed, cancellationToken)
                        .ConfigureAwait(false);
                    await using var csvStream = File.OpenRead(csvPath);
                    var table = await tableReader.ReadAsync(csvStream, relativePath, cancellationToken).ConfigureAwait(false);
                    package.Tables.Tables.Add(table);
                    record.ImportedTableFileCount++;
                }
                else
                {
                    var category = PathClassifier.ResourceCategory(relativePath);
                    var projectRelativePath = category switch
                    {
                        "preview" => Path.Combine("resources", relativePath).Replace('\\', '/'),
                        _ => Path.Combine("resources", platform, relativePath).Replace('\\', '/')
                    };

                    var archivedPath = Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(archivedPath) ?? projectRoot);

                    if (entry.Compressed)
                    {
                        await compressionService.DecompressFileAsync(sourcePath, archivedPath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await using var source = File.OpenRead(sourcePath);
                        await using var destination = File.Create(archivedPath);
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    }

                    var existingPreview = category == "preview"
                        ? package.Resources.FirstOrDefault(r =>
                            r.PackageRelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
                            && r.Category == "preview")
                        : null;

                    if (existingPreview is not null)
                    {
                        var updatedIncludedPlatforms = new List<string>(existingPreview.IncludedPlatforms ?? []);
                        if (!updatedIncludedPlatforms.Contains(platform))
                        {
                            updatedIncludedPlatforms.Add(platform);
                        }

                        package.Resources.Remove(existingPreview);
                        package.Resources.Add(new ResourceFile(
                            existingPreview.PackageRelativePath,
                            existingPreview.ProjectRelativePath,
                            existingPreview.Category,
                            existingPreview.Compressed,
                            existingPreview.SourcePackagePath,
                            existingPreview.Platform,
                            updatedIncludedPlatforms));
                    }
                    else
                    {
                        var resource = new ResourceFile(
                            relativePath,
                            projectRelativePath,
                            category,
                            entry.Compressed,
                            sourcePath,
                            category != "preview" ? platform : null,
                            category == "preview" ? [platform] : null);
                        package.Resources.Add(resource);
                    }

                    record.ImportedResourceFileCount++;
                }
            }

            package.Platforms.Add(record);

            ExtractEntitiesFromTables(package);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private void ExtractEntitiesFromTables(PatchPackage package)
    {
        ExtractEntityType(package,
            songCatalogService.BuildCatalog(package, forceFromTables: true),
            package.Songs,
            s => s.Id,
            SongCatalogService.IsSongRelatedTable);

        ExtractEntityType(package,
            songCatalogService.BuildAchievementCatalog(package),
            package.Achievements,
            a => a.Id,
            SongCatalogService.IsAchievementRelatedTable);

        ExtractEntityType(package,
            songCatalogService.BuildQuestCatalog(package),
            package.Quests,
            q => q.Id,
            SongCatalogService.IsQuestRelatedTable);
    }

    private static void ExtractEntityType<T>(
        PatchPackage package,
        IReadOnlyList<T> built,
        List<T> target,
        Func<T, string> idSelector,
        Func<string, bool> isRelatedTable) where T : notnull
    {
        if (built.Count == 0) return;

        var existingIds = target.Select(idSelector)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in built)
        {
            if (!existingIds.Contains(idSelector(entity)))
            {
                target.Add(entity);
            }
        }

        var related = package.Tables.Tables
            .Where(t => isRelatedTable(t.TableName))
            .ToArray();
        foreach (var table in related)
            package.Tables.Tables.Remove(table);
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
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? tempRoot);
        await compressionService.DecompressFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        return destinationPath;
    }

    private static string? TryResolveSourcePath(string packageRoot, string relativePath, bool compressed)
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

        return null;
    }

    private static string? TryGetVersion(string packageRoot)
    {
        var parent = Directory.GetParent(packageRoot);
        return parent?.Name.Contains('.', StringComparison.Ordinal) == true ? parent.Name : null;
    }
}
