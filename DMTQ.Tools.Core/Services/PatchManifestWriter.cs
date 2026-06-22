using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class PatchManifestWriter
{
    public async Task WriteAsync(
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
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await textWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
