using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatchChecksumServiceTests
{
    [TestMethod]
    public async Task GetFileSizeAsync_ReturnsExactByteLength()
    {
        var path = Path.Combine(Path.GetTempPath(), "dmtq-size-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03, 0x04]);

            var service = new PatchChecksumService();

            var size = service.GetFileSize(path);

            size.Should().Be(4);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task ComputeMd5Async_ReturnsLowercaseHexDigest()
    {
        var path = Path.Combine(Path.GetTempPath(), "dmtq-md5-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, "abc");

            var service = new PatchChecksumService();

            var checksum = await service.ComputeMd5Async(path);

            checksum.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
