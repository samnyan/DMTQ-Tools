using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Project;

/// <summary>Reads legacy JSON integer values that may be numbers, numeric strings, or empty strings.</summary>
public sealed class FlexibleInt64JsonConverter : JsonConverter<long>
{
    /// <inheritdoc />
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => throw new JsonException($"Expected a number or numeric string, got {reader.TokenType}.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);

    private static long ParseString(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? 0
            : long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new JsonException($"Could not parse '{value}' as Int64.");
}
