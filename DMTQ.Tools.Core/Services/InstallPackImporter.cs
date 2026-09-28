using System.Security.Cryptography;
using System.Text;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Imports resources listed by the game's StreamingAssets InstallPack index.</summary>
public sealed class InstallPackImporter
{
    /// <summary>Imports all usable files from <c>InstallPack/&lt;platform&gt;/InstallPack</c>.</summary>
    public async Task<InstallPackImportResult> ImportAsync(
        PatchPackage package,
        string streamingAssetsRoot,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamingAssetsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        var platformName = platform.Trim().ToLowerInvariant();
        if (platformName is not ("android" or "ios"))
            throw new ArgumentException("InstallPack platform must be android or ios.", nameof(platform));

        var root = Path.GetFullPath(streamingAssetsRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"StreamingAssets directory was not found: {root}");

        var manifestPath = Path.Combine(root, "InstallPack", platformName, "InstallPack");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"InstallPack index was not found for {platformName}.", manifestPath);

        var result = new InstallPackImportResult();
        var lines = await File.ReadAllLinesAsync(manifestPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var importedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var manifestPrefix = $"InstallPack/{platformName}/";

        foreach (var rawLine in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('.'))
            {
                result.SkippedEntries++;
                continue;
            }

            try
            {
                var sourceRelativePath = FileUtility.NormalizePackageRelativePath(line);
                if (!sourceRelativePath.StartsWith(manifestPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"InstallPack entry must be under '{manifestPrefix}': {line}");

                var sourcePath = Path.GetFullPath(Path.Combine(root, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!sourcePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"InstallPack entry escapes its source directory: {line}");
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException($"InstallPack file is missing: {line}", sourcePath);

                var packagePath = sourceRelativePath[manifestPrefix.Length..];
                if (packagePath.StartsWith("res/", StringComparison.OrdinalIgnoreCase))
                    packagePath = packagePath[4..];
                var sourceIsCompressed = packagePath.EndsWith(".lz4", StringComparison.OrdinalIgnoreCase);
                if (sourceIsCompressed)
                    packagePath = packagePath[..^4];

                packagePath = FileUtility.NormalizePackageRelativePath(packagePath);
                if (!importedPaths.Add(packagePath))
                {
                    result.SkippedEntries++;
                    continue;
                }

                var category = FileUtility.ResourceCategory(packagePath);
                var archivePath = Path.Combine(package.ProjectInfo.ProjectRoot, "resources", platformName,
                    packagePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath) ?? package.ProjectInfo.ProjectRoot);
                if (sourceIsCompressed)
                    await FileUtility.DecompressFileAsync(sourcePath, archivePath, cancellationToken).ConfigureAwait(false);
                else
                    await CopyFileAsync(sourcePath, archivePath, cancellationToken).ConfigureAwait(false);

                var checksum = await FileUtility.ComputeMd5Async(archivePath, cancellationToken).ConfigureAwait(false);
                var fileSize = FileUtility.GetFileSize(archivePath);
                var compressedChecksum = sourceIsCompressed
                    ? await FileUtility.ComputeMd5Async(sourcePath, cancellationToken).ConfigureAwait(false)
                    : string.Empty;
                var resource = package.Resources.FirstOrDefault(item =>
                    item.FileName.Equals(packagePath, StringComparison.OrdinalIgnoreCase));
                if (resource is null)
                {
                    resource = new ResourceFile
                    {
                        FileName = packagePath,
                        Category = category
                    };
                    package.Resources.Add(resource);
                }

                resource.Compressed = sourceIsCompressed;
                var platformEntry = resource.PlatformManifest.FirstOrDefault(item =>
                    item.Platform.Equals(platformName, StringComparison.OrdinalIgnoreCase));
                if (platformEntry is null)
                {
                    platformEntry = new PlatformManifestEntry { Platform = platformName };
                    resource.PlatformManifest.Add(platformEntry);
                }

                platformEntry.Exist = true;
                platformEntry.IsInstallPack = true;
                platformEntry.SourceFileSize = fileSize;
                platformEntry.SourceChecksum = checksum;
                platformEntry.SourceCompressedFileSize = sourceIsCompressed ? FileUtility.GetFileSize(sourcePath) : 0;
                platformEntry.SourceCompressedChecksum = compressedChecksum;
                platformEntry.Checksum = checksum;
                platformEntry.InstallPackBaselineChecksum = checksum;
                result.ImportedFiles++;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                result.Errors.Add($"{line}: {exception.Message}");
            }
        }

        return result;
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }
}
