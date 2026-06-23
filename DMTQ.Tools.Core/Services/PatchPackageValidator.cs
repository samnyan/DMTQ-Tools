using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchPackageValidator
{
    public async Task<PatchValidationResult> ValidateAsync(
        PatchManifest manifest,
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);

        var result = new PatchValidationResult();
        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = FileUtility.NormalizePackageRelativePath(entry.FileName);
            var filePath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var compressedPath = filePath + ".lz4";

            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Missing file: {relativePath}");
            }
            else
            {
                await ValidateFileAsync(result, relativePath, filePath, entry.FileSize, entry.Checksum, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (entry.Compressed)
            {
                if (!File.Exists(compressedPath))
                {
                    result.Errors.Add($"Missing compressed file: {relativePath}.lz4");
                }
                else
                {
                    await ValidateFileAsync(result, relativePath + ".lz4", compressedPath, entry.CompressedFileSize, entry.CompressedChecksum, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        return result;
    }

    private async Task ValidateFileAsync(
        PatchValidationResult result,
        string relativePath,
        string filePath,
        long expectedSize,
        string expectedChecksum,
        CancellationToken cancellationToken)
    {
        var actualSize = FileUtility.GetFileSize(filePath);
        if (actualSize != expectedSize)
        {
            result.Errors.Add($"Size mismatch for {relativePath}: expected {expectedSize}, actual {actualSize}");
        }

        var actualChecksum = await FileUtility.ComputeMd5Async(filePath, cancellationToken).ConfigureAwait(false);
        if (!actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Checksum mismatch for {relativePath}: expected {expectedChecksum}, actual {actualChecksum}");
        }
    }
}
