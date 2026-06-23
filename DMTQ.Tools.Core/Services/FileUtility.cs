using System.Security.Cryptography;
using K4os.Compression.LZ4.Legacy;

namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Stateless file-level utilities: hashing, LZ4 compression, and path classification
/// shared across import/export/validation workflows.
/// </summary>
public static class FileUtility
{
    // ── File size & hashing (was PatchChecksumService) ──

    public static long GetFileSize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileInfo(path).Length;
    }

    public static async Task<string> ComputeMd5Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        var hash = await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── LZ4 compression (was Lz4CompressionService) ──

    public static async Task CompressFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await using var lz4 = LZ4Legacy.Encode(destination, leaveOpen: false);
        await source.CopyToAsync(lz4, cancellationToken).ConfigureAwait(false);
    }

    public static async Task DecompressFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        await using var source = File.OpenRead(sourcePath);
        await using var lz4 = LZ4Legacy.Decode(source, leaveOpen: false);
        await using var destination = File.Create(destinationPath);
        await lz4.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    // ── Path classification (was PathClassifier) ──

    public static bool IsCsvTable(string packageRelativePath)
    {
        var path = NormalizePackageRelativePath(packageRelativePath);
        return path.StartsWith("table/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResourceCategory(string packageRelativePath)
    {
        var path = NormalizePackageRelativePath(packageRelativePath);
        var firstSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment switch
        {
            "dlc" => "dlc",
            "preview" => "preview",
            "Fonts" => "Fonts",
            "fonts" => "Fonts",
            _ => "other"
        };
    }

    public static string Normalize(string path)
        => NormalizePackageRelativePath(path);

    public static string NormalizePackageRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"Manifest contains unsafe package path '{path}'.");
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new InvalidDataException($"Manifest contains unsafe package path '{path}'.");
        }

        return string.Join('/', parts);
    }
}
