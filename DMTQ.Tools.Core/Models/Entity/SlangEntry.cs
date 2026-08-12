using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>An editable row in table/slang/slang.csv.</summary>
public sealed class SlangEntry
{
    /// <summary>Stable project-only identifier; it is not written to the CSV.</summary>
    [JsonInclude]
    public required string Id { get; init; }

    /// <summary>Gets or sets the blocked word or phrase.</summary>
    public string Value { get; set; } = string.Empty;

    [SetsRequiredMembers]
    public SlangEntry()
    {
        Id = Guid.NewGuid().ToString("N");
    }
}
