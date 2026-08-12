using System.Text;
using DMTQ.Tools.Core.Models.Csv;
using DMTQ.Tools.Core.Models.Entity;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Models.Csv;

[TestClass]
public sealed class SlangCsvSchemaTests
{
    [TestMethod]
    public void ReadAndWrite_PreservesCurrentPatchShape()
    {
        var schema = new SlangCsvSchema();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("slang,\r\n\"alpha\",\r\n\"two words\",\r\n"));

        var entries = schema.ReadCsv(input);

        entries.Select(entry => entry.Value).Should().Equal("alpha", "two words");
        using var output = new MemoryStream();
        schema.WriteCsv(output, entries);
        Encoding.UTF8.GetString(output.ToArray()).Should().Be(
            "slang,\r\n\"alpha\",\r\n\"two words\",\r\n");
    }

    [TestMethod]
    public void WriteCsv_QuotesCommasAndKeepsTrailingColumn()
    {
        using var output = new MemoryStream();

        new SlangCsvSchema().WriteCsv(output,
            [new SlangEntry { Id = "1", Value = "alpha,beta" }]);

        Encoding.UTF8.GetString(output.ToArray()).Should().Contain("\"alpha,beta\",\r\n");
    }
}
