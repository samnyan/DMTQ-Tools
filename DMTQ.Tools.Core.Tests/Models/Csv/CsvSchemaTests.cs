using DMTQ.Tools.Core.Models.Csv;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Models.Csv;

[TestClass]
public sealed class CsvSchemaTests
{
    private sealed class TestRow
    {
        public string? Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private sealed class TestSchema : CsvSchema<TestRow>
    {
        public override string TableName => "test_table";

        public override IReadOnlyList<CsvColumn<TestRow>> Columns { get; } =
        [
            new CsvColumn<TestRow>("Id", 0,
                r => r.Id ?? "",
                (r, v) => r.Id = v),
            new CsvColumn<TestRow>("Name", 1,
                r => r.Name,
                (r, v) => r.Name = v),
            new CsvColumn<TestRow>("Age", 2,
                r => r.Age.ToString(),
                (r, v) => r.Age = int.TryParse(v, out var age) ? age : 0),
        ];
    }

    [TestMethod]
    public void WriteCsv_ThenReadCsv_RoundTrips()
    {
        // Arrange
        var schema = new TestSchema();
        var entities = new List<TestRow>
        {
            new() { Id = "1", Name = "Alice", Age = 30 },
            new() { Id = "2", Name = "Bob", Age = 25 },
            new() { Id = "3", Name = "Charlie", Age = 35 },
        };

        // Act — write
        using var writeStream = new MemoryStream();
        schema.WriteCsv(writeStream, entities);

        // Act — read back
        var csvBytes = writeStream.ToArray();
        using var readStream = new MemoryStream(csvBytes);
        var result = schema.ReadCsv(readStream);

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be("1");
        result[0].Name.Should().Be("Alice");
        result[0].Age.Should().Be(30);
        result[1].Id.Should().Be("2");
        result[1].Name.Should().Be("Bob");
        result[1].Age.Should().Be(25);
        result[2].Id.Should().Be("3");
        result[2].Name.Should().Be("Charlie");
        result[2].Age.Should().Be(35);
    }

    [TestMethod]
    public void WriteCsv_ProducesValidCsv()
    {
        // Arrange
        var schema = new TestSchema();
        var entities = new List<TestRow>
        {
            new() { Id = "1", Name = "Alice", Age = 30 },
        };

        using var stream = new MemoryStream();
        schema.WriteCsv(stream, entities);

        var csv = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Id,Name,Age");
        csv.Should().Contain("1,Alice,30");
    }

    [TestMethod]
    public void ReadCsv_DeduplicatesById()
    {
        // Arrange
        var schema = new TestSchema();
        var csv = "Id,Name,Age\r\n1,Alice,30\r\n2,Bob,25\r\n1,AliceDuplicate,99\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        var result = schema.ReadCsv(stream);

        // Assert — the third row with duplicate Id "1" is skipped
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("1");
        result[0].Name.Should().Be("Alice");
        result[1].Id.Should().Be("2");
        result[1].Name.Should().Be("Bob");
    }

    [TestMethod]
    public void ReadCsv_HandlesQuotedValues()
    {
        // Arrange
        var schema = new TestSchema();
        var csv = "Id,Name,Age\r\n1,\"Doe, John\",42\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        var result = schema.ReadCsv(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Doe, John");
    }

    [TestMethod]
    public void ReadCsv_ThrowsWhenEmpty()
    {
        var schema = new TestSchema();
        using var stream = new MemoryStream();

        Action act = () => schema.ReadCsv(stream);
        act.Should().Throw<InvalidDataException>().WithMessage("*empty*");
    }

    [TestMethod]
    public void ReadCsv_ThrowsWhenMissingColumn()
    {
        var schema = new TestSchema();
        var csv = "Id,Age\r\n1,30\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        Action act = () => schema.ReadCsv(stream);
        act.Should().Throw<InvalidDataException>().WithMessage("*missing column 'Name'*");
    }

    [TestMethod]
    public void ReadCsv_MapsByColumnNameCaseInsensitively()
    {
        // Arrange
        var schema = new TestSchema();
        var csv = "id,NAME,age\r\n1,Alice,30\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        var result = schema.ReadCsv(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
        result[0].Name.Should().Be("Alice");
        result[0].Age.Should().Be(30);
    }

    [TestMethod]
    public void WriteCsv_WritesColumnsInOrder()
    {
        // Arrange — define a schema with non-sequential Order values
        var schema = new OutOfOrderSchema();
        var entities = new List<OutOfOrderRow>
        {
            new() { First = "A", Second = "B", Third = "C" },
        };

        using var stream = new MemoryStream();
        schema.WriteCsv(stream, entities);

        var csv = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().StartWith("First,Second,Third");
        csv.Should().Contain("A,B,C");
    }

    private sealed class OutOfOrderRow
    {
        public string First { get; set; } = "";
        public string Second { get; set; } = "";
        public string Third { get; set; } = "";
    }

    private sealed class OutOfOrderSchema : CsvSchema<OutOfOrderRow>
    {
        public override string TableName => "out_of_order";

        public override IReadOnlyList<CsvColumn<OutOfOrderRow>> Columns { get; } =
        [
            new CsvColumn<OutOfOrderRow>("Second", 2,
                r => r.Second,
                (r, v) => r.Second = v),
            new CsvColumn<OutOfOrderRow>("First", 1,
                r => r.First,
                (r, v) => r.First = v),
            new CsvColumn<OutOfOrderRow>("Third", 3,
                r => r.Third,
                (r, v) => r.Third = v),
        ];
    }
}
