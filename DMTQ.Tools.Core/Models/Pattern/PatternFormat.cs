namespace DMTQ.Tools.Core.Models.Pattern;

/// <summary>
/// Binary pattern formats supported by the application.
/// </summary>
public enum PatternFormat
{
    /// <summary>DJMAX Technika Q .bytes format.</summary>
    Bytes,

    /// <summary>DJMAX Technika Q .pt format.</summary>
    Pt
}

/// <summary>
/// Command identifiers shared by the bytes and PT formats.
/// </summary>
public enum PatternCommandType : byte
{
    /// <summary>Track-start marker used by the bytes format.</summary>
    TrackStart = 0,

    /// <summary>Playable note command.</summary>
    Note = 1,

    /// <summary>Volume change command.</summary>
    Volume = 2,

    /// <summary>BPM change command.</summary>
    BpmChange = 3,

    /// <summary>Beat or time-signature command.</summary>
    Beat = 4
}
