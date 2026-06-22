using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchManifestReader
{
    public async Task<PatchManifest> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var textReader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null
        });

        var manifest = new PatchManifest();
        await foreach (var row in csv.GetRecordsAsync<PatchManifestCsvRow>(cancellationToken).ConfigureAwait(false))
        {
            manifest.Entries.Add(new PatchFileEntry(
                NormalizePath(row.FileName),
                row.FileSize,
                row.Checksum ?? string.Empty,
                row.CompressedFileSize,
                row.CompressedChecksum ?? string.Empty,
                row.AcquireOnDemand,
                row.Compressed == 1,
                row.Platform ?? string.Empty,
                row.Tag ?? string.Empty));
        }

        return manifest;
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private sealed class PatchManifestCsvRow
    {
        [CsvHelper.Configuration.Attributes.Name("file_name")]
        public string FileName { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("file_size")]
        public long FileSize { get; set; }

        [CsvHelper.Configuration.Attributes.Name("checksum")]
        public string? Checksum { get; set; }

        [CsvHelper.Configuration.Attributes.Name("compressed_file_size")]
        public long CompressedFileSize { get; set; }

        [CsvHelper.Configuration.Attributes.Name("compressed_checksum")]
        public string? CompressedChecksum { get; set; }

        [CsvHelper.Configuration.Attributes.Name("acquire_on_demand")]
        public int AcquireOnDemand { get; set; }

        [CsvHelper.Configuration.Attributes.Name("compressed")]
        public int Compressed { get; set; }

        [CsvHelper.Configuration.Attributes.Name("platform")]
        public string? Platform { get; set; }

        [CsvHelper.Configuration.Attributes.Name("tag")]
        public string? Tag { get; set; }
    }
}
