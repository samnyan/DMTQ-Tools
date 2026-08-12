using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Core.Models.Csv;

/// <summary>Reads and writes the shared table/slang/slang.csv table.</summary>
public sealed class SlangCsvSchema
{
    /// <summary>Reads slang rows while ignoring the format's trailing empty column.</summary>
    /// <param name="stream">CSV input stream.</param>
    /// <returns>Editable slang entries in source order.</returns>
    public List<SlangEntry> ReadCsv(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            return [];
        }

        var result = new List<SlangEntry>();
        while (csv.Read())
        {
            var value = csv.GetField(0) ?? string.Empty;
            result.Add(new SlangEntry { Id = Guid.NewGuid().ToString("N"), Value = value });
        }

        return result;
    }

    /// <summary>Writes the exact two-column shape used by current client patches.</summary>
    /// <param name="stream">CSV output stream.</param>
    /// <param name="entries">Entries to write in order.</param>
    public void WriteCsv(Stream stream, IEnumerable<SlangEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(entries);

        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        });

        csv.WriteField("slang");
        csv.WriteField(string.Empty);
        csv.NextRecord();

        foreach (var entry in entries)
        {
            csv.WriteField(entry.Value, shouldQuote: true);
            csv.WriteField(string.Empty);
            csv.NextRecord();
        }

        writer.Flush();
    }
}
