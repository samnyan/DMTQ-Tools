namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Options controlling binary pattern serialization.
/// </summary>
public sealed class PatternSerializationOptions
{
    /// <summary>Gets or sets the PT version to emit. The default is the padded version 1.</summary>
    public short PtVersion { get; init; } = 1;

    /// <summary>Gets or sets the bytes-format leading marker.</summary>
    public int BytesMagic { get; init; }
}
