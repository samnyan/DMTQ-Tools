using System.Security.Cryptography;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchChecksumService
{
    public long GetFileSize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileInfo(path).Length;
    }

    public async Task<string> ComputeMd5Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        var hash = await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
