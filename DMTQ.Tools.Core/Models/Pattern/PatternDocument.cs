namespace DMTQ.Tools.Core.Models.Pattern;

/// <summary>
/// Format-neutral representation of a DJMAX pattern.
/// </summary>
public sealed class PatternDocument
{
    /// <summary>Gets or sets the source format used to read this document.</summary>
    public PatternFormat SourceFormat { get; set; }

    /// <summary>Gets or sets whether the source PT file was encrypted.</summary>
    public bool WasEncrypted { get; set; }

    /// <summary>Gets the pattern header.</summary>
    public PatternHeader Header { get; } = new();

    /// <summary>Gets the sound table.</summary>
    public List<PatternSound> Sounds { get; } = [];

    /// <summary>Gets the tracks in file order.</summary>
    public List<PatternTrack> Tracks { get; } = [];

    /// <summary>Gets the number of non-track-start commands in the document.</summary>
    public int CommandCount => Tracks.Sum(track => track.Commands.Count);
}

/// <summary>
/// Header values shared by both binary formats.
/// </summary>
public sealed class PatternHeader
{
    /// <summary>Gets or sets the bytes-format leading marker.</summary>
    public int BytesMagic { get; set; }

    /// <summary>Gets or sets the PT format version. Version 1 is the padded layout.</summary>
    public short PtVersion { get; set; } = 1;

    /// <summary>Gets or sets the number of positions in one measure.</summary>
    public short PositionsPerMeasure { get; set; }

    /// <summary>Gets or sets the initial BPM.</summary>
    public float InitialBpm { get; set; }

    /// <summary>Gets or sets the total pattern end position.</summary>
    public int EndPosition { get; set; }

    /// <summary>Gets or sets the format-specific tag B value.</summary>
    public int TagB { get; set; }

    /// <summary>Gets or sets the bytes-format tag C value.</summary>
    public int TagC { get; set; }

    /// <summary>Gets or sets the bytes-format stored total command count.</summary>
    public int DeclaredCommandCount { get; set; }
}

/// <summary>
/// An entry in the pattern sound table.
/// </summary>
public sealed class PatternSound
{
    /// <summary>Gets or sets the sound table identifier.</summary>
    public ushort Id { get; set; }

    /// <summary>Gets or sets the format-specific sound flag or command value.</summary>
    public ushort Flags { get; set; }

    /// <summary>Gets or sets the fixed-width sound file name.</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// A track and its commands.
/// </summary>
public sealed class PatternTrack
{
    /// <summary>Gets or sets the track identifier.</summary>
    public short Id { get; set; }

    /// <summary>Gets or sets the track name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the bytes-format track-start position.</summary>
    public int StartPosition { get; set; }

    /// <summary>Gets or sets the PT track tick value.</summary>
    public int EndPosition { get; set; }

    /// <summary>Gets or sets the count stored in the source track header.</summary>
    public int DeclaredCommandCount { get; set; }

    /// <summary>Gets or sets the bytes-format shifted command count.</summary>
    public int DeclaredShiftedCommandCount { get; set; }

    /// <summary>Gets or sets the data byte count stored in the PT track header.</summary>
    public int DeclaredDataSize { get; set; }

    /// <summary>Gets the commands in file order.</summary>
    public List<PatternCommand> Commands { get; } = [];
}

/// <summary>
/// One format-neutral pattern command.
/// </summary>
public sealed class PatternCommand
{
    private byte[] _rawParameters = new byte[8];

    /// <summary>Gets or sets the absolute command position.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the command identifier, including unknown identifiers.</summary>
    public byte Type { get; set; }

    /// <summary>Gets or sets the sound identifier for a note command.</summary>
    public ushort SoundIndex { get; set; }

    /// <summary>Gets or sets the note or volume value.</summary>
    public byte Volume { get; set; }

    /// <summary>Gets or sets the note pan value.</summary>
    public byte Pan { get; set; }

    /// <summary>Gets or sets the note attribute.</summary>
    public byte Attribute { get; set; }

    /// <summary>Gets or sets the note duration.</summary>
    public byte Length { get; set; }

    /// <summary>Gets or sets the note's unknown 16-bit value.</summary>
    public ushort NoteUnknown { get; set; }

    /// <summary>Gets or sets the BPM value for a BPM change command.</summary>
    public float Bpm { get; set; }

    /// <summary>Gets or sets the beat value for a beat command.</summary>
    public ushort Beat { get; set; }

    /// <summary>
    /// Gets or sets the eight canonical parameter bytes. Unknown commands use these
    /// bytes verbatim; known command fields are projected from and written back to them.
    /// </summary>
    public byte[] RawParameters
    {
        get => _rawParameters;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length != 8)
            {
                throw new ArgumentException("Pattern command parameters must contain exactly 8 bytes.", nameof(value));
            }

            _rawParameters = [.. value];
        }
    }

    /// <summary>Gets the command type when it is one of the known command identifiers.</summary>
    public PatternCommandType? KnownType => Enum.IsDefined((PatternCommandType)Type)
        ? (PatternCommandType)Type
        : null;

    /// <summary>Creates a note command.</summary>
    public static PatternCommand CreateNote(
        int position,
        ushort soundIndex,
        byte volume,
        byte pan,
        byte attribute,
        byte length,
        ushort noteUnknown = 0)
        => new()
        {
            Position = position,
            Type = (byte)PatternCommandType.Note,
            SoundIndex = soundIndex,
            Volume = volume,
            Pan = pan,
            Attribute = attribute,
            Length = length,
            NoteUnknown = noteUnknown
        };

    /// <summary>Creates a volume command.</summary>
    public static PatternCommand CreateVolume(int position, byte volume)
        => new()
        {
            Position = position,
            Type = (byte)PatternCommandType.Volume,
            Volume = volume
        };

    /// <summary>Creates a BPM change command.</summary>
    public static PatternCommand CreateBpmChange(int position, float bpm)
        => new()
        {
            Position = position,
            Type = (byte)PatternCommandType.BpmChange,
            Bpm = bpm
        };

    /// <summary>Creates a beat command.</summary>
    public static PatternCommand CreateBeat(int position, ushort beat)
        => new()
        {
            Position = position,
            Type = (byte)PatternCommandType.Beat,
            Beat = beat
        };
}
