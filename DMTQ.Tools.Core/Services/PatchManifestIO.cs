using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Reads and writes patch_new.csv manifest files (the compact CSV format
/// used by every platform-specific patch package).
/// </summary>
public static class PatchManifestIO
{
    /// <summary>Read a <see cref="PatchManifest"/> from a CSV stream.</summary>
    public static async Task<PatchManifest> ReadAsync(
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

    /// <summary>Write a <see cref="PatchManifest"/> to a CSV stream.</summary>
    public static async Task WriteAsync(
        PatchManifest manifest,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(stream);

        await using var textWriter = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        await using var csv = new CsvWriter(textWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        csv.WriteField("file_name");
        csv.WriteField("file_size");
        csv.WriteField("checksum");
        csv.WriteField("compressed_file_size");
        csv.WriteField("compressed_checksum");
        csv.WriteField("acquire_on_demand");
        csv.WriteField("compressed");
        csv.WriteField("platform");
        csv.WriteField("tag");
        csv.WriteField(""); // trailing empty column (game client expects exact column count)
        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.WriteField(entry.FileName);
            csv.WriteField(entry.FileSize);
            csv.WriteField(entry.Checksum);
            csv.WriteField(entry.CompressedFileSize);
            csv.WriteField(entry.CompressedChecksum);
            csv.WriteField(entry.AcquireOnDemand);
            csv.WriteField(entry.Compressed ? 1 : 0);
            csv.WriteField(entry.Platform);
            csv.WriteField(entry.Tag);
            csv.WriteField(""); // trailing empty column
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await textWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
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
