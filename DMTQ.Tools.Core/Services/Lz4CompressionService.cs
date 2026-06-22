using K4os.Compression.LZ4.Legacy;

namespace DMTQ.Tools.Core.Services;

public sealed class Lz4CompressionService
{
    public async Task CompressFileAsync(
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

    public async Task DecompressFileAsync(
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
}
