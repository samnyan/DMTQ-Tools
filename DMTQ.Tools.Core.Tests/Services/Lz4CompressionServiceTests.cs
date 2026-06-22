using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class Lz4CompressionServiceTests
{
    [TestMethod]
    public async Task CompressThenDecompressAsync_RestoresOriginalBytes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dmtq-lz4-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var source = Path.Combine(tempRoot, "sample.csv");
            var compressed = Path.Combine(tempRoot, "sample.csv.lz4");
            var restored = Path.Combine(tempRoot, "sample-restored.csv");
            await File.WriteAllTextAsync(source, "id,name\r\n1,oblivion\r\n");

            var service = new Lz4CompressionService();
            await service.CompressFileAsync(source, compressed);
            await service.DecompressFileAsync(compressed, restored);

            var originalBytes = await File.ReadAllBytesAsync(source);
            var restoredBytes = await File.ReadAllBytesAsync(restored);
            restoredBytes.Should().Equal(originalBytes);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
