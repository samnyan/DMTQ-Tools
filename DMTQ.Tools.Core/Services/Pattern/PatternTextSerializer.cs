using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using DMTQ.Tools.Core.Models.Pattern;

namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Reads and writes the human-editable DMTQ pattern text format.
/// </summary>
/// <remarks>
/// The format is intentionally compatible with the legacy bytes/PT text tools.
/// Additional metadata and <c>raw=</c> command tails are emitted as optional
/// extensions so older tools can ignore them while this serializer preserves
/// more format-specific data.
/// </remarks>
public sealed class PatternTextSerializer
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Deserializes text from a stream.</summary>
    /// <param name="source">The UTF-8 text stream.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public PatternDocument Deserialize(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Utf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Deserialize(reader.ReadToEnd());
    }

    /// <summary>Asynchronously deserializes text from a stream.</summary>
    /// <param name="source">The UTF-8 text stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public async Task<PatternDocument> DeserializeAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Utf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Deserialize(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deserializes a pattern from its text representation.</summary>
    /// <param name="text">The complete pattern text.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public PatternDocument Deserialize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var document = new PatternDocument { SourceFormat = PatternFormat.Text };
        var soundsById = new Dictionary<ushort, PatternSound>();
        PatternTrack? currentTrack = null;

        foreach (var sourceLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (!line.StartsWith('#'))
                continue;

            var content = line[1..].TrimStart();
            if (content.StartsWith("WAV", StringComparison.OrdinalIgnoreCase))
            {
                ParseSound(content, document, soundsById);
                continue;
            }

            var tokens = Tokenize(content);
            if (tokens.Count == 0)
                continue;

            var keyword = tokens[0].ToUpperInvariant();
            switch (keyword)
            {
                case "SOUND_COUNT":
                case "TRACK_COUNT":
                case "FORMAT":
                case "SOURCE_FORMAT":
                    break;
                case "BYTES_MAGIC":
                    document.Header.BytesMagic = ParseInt(tokens, 1, keyword);
                    break;
                case "PT_VERSION":
                    document.Header.PtVersion = checked((short)ParseInt(tokens, 1, keyword));
                    break;
                case "POSITION_PER_MEASURE":
                    document.Header.PositionsPerMeasure = checked((short)ParseInt(tokens, 1, keyword));
                    break;
                case "BPM":
                    document.Header.InitialBpm = ParseFloat(tokens, 1, keyword);
                    break;
                case "END_POSITION":
                    document.Header.EndPosition = ParseInt(tokens, 1, keyword);
                    break;
                case "TAGB":
                    document.Header.TagB = ParseInt(tokens, 1, keyword);
                    break;
                case "TAGC":
                    document.Header.TagC = ParseInt(tokens, 1, keyword);
                    break;
                case "TOTOAL_CMD_COUNT":
                case "TOTAL_CMD_COUNT":
                    document.Header.DeclaredCommandCount = ParseInt(tokens, 1, keyword);
                    break;
                case "SOUND_FLAGS":
                    ParseSoundFlags(tokens, soundsById);
                    break;
                case "POSITION":
                    break;
                default:
                    if (IsInteger(tokens[0]))
                    {
                        if (tokens.Count < 2)
                            throw new InvalidDataException($"Pattern text command is missing its type: {sourceLine}");

                        if (tokens[1].Equals("TRACK_START", StringComparison.OrdinalIgnoreCase))
                        {
                            currentTrack = ParseTrackStart(tokens, document.Tracks.Count);
                            document.Tracks.Add(currentTrack);
                        }
                        else
                        {
                            if (currentTrack is null)
                                throw new InvalidDataException("Pattern text contains an event before TRACK_START.");

                            currentTrack.Commands.Add(ParseCommand(tokens));
                        }
                    }
                    break;
            }
        }

        foreach (var track in document.Tracks)
        {
            track.DeclaredCommandCount = track.DeclaredCommandCount == 0
                ? track.Commands.Count
                : track.DeclaredCommandCount;
            if (track.EndPosition == 0)
            {
                track.EndPosition = track.Commands.Count == 0
                    ? track.StartPosition
                    : track.Commands.Max(command => command.Position);
            }
        }

        return document;
    }

    /// <summary>Serializes a pattern to UTF-8 text.</summary>
    /// <param name="pattern">The pattern document.</param>
    /// <returns>The human-editable text representation.</returns>
    public string Serialize(PatternDocument pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var builder = new StringBuilder();
        builder.AppendLine("#FORMAT DMTQ_PATTERN_TEXT 1");
        builder.AppendLine($"#SOURCE_FORMAT {pattern.SourceFormat}");
        builder.AppendLine($"#BYTES_MAGIC {pattern.Header.BytesMagic}");
        builder.AppendLine($"#PT_VERSION {pattern.Header.PtVersion}");
        builder.AppendLine($"#SOUND_COUNT {pattern.Sounds.Count}");
        builder.AppendLine($"#TRACK_COUNT {pattern.Tracks.Count}");
        builder.AppendLine($"#POSITION_PER_MEASURE {pattern.Header.PositionsPerMeasure}");
        builder.AppendLine($"#BPM {pattern.Header.InitialBpm.ToString("R", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"#END_POSITION {pattern.Header.EndPosition}");
        builder.AppendLine($"#TAGB {pattern.Header.TagB}");
        builder.AppendLine($"#TAGC {pattern.Header.TagC}");
        var declaredCommandCount = pattern.Header.DeclaredCommandCount == 0
            ? pattern.CommandCount
            : pattern.Header.DeclaredCommandCount;
        builder.AppendLine($"#TOTOAL_CMD_COUNT {declaredCommandCount}");

        foreach (var sound in pattern.Sounds)
        {
            builder.Append("#WAV").Append(sound.Id.ToString("X4", CultureInfo.InvariantCulture));
            builder.Append(' ').AppendLine(sound.FileName);
            builder.Append("#SOUND_FLAGS ").Append(sound.Id.ToString("X4", CultureInfo.InvariantCulture));
            builder.Append(' ').AppendLine(sound.Flags.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("POSITION COMMAND PARAMETER");
        foreach (var track in pattern.Tracks)
        {
            var commandCount = track.DeclaredCommandCount == 0
                ? track.Commands.Count
                : track.DeclaredCommandCount;
            var shiftedCount = track.DeclaredShiftedCommandCount == 0
                ? checked(track.Commands.Count << 4)
                : track.DeclaredShiftedCommandCount;
            builder.Append('#').Append(track.StartPosition)
                .Append(" TRACK_START ").Append(track.Id)
                .Append(" '").Append(EscapeQuotedValue(track.Name)).Append("' ")
                .Append(commandCount).Append(' ').Append(shiftedCount)
                .Append(" end=").Append(track.EndPosition)
                .Append(" data=").AppendLine(track.DeclaredDataSize.ToString(CultureInfo.InvariantCulture));

            foreach (var command in track.Commands)
                AppendCommand(builder, command);
        }

        return builder.ToString();
    }

    /// <summary>Writes a pattern as UTF-8 text to a stream.</summary>
    /// <param name="pattern">The pattern document.</param>
    /// <param name="destination">The writable destination stream.</param>
    public void Serialize(PatternDocument pattern, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var data = Utf8.GetBytes(Serialize(pattern));
        destination.Write(data, 0, data.Length);
    }

    /// <summary>Asynchronously writes a pattern as UTF-8 text to a stream.</summary>
    /// <param name="pattern">The pattern document.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SerializeAsync(
        PatternDocument pattern,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var data = Utf8.GetBytes(Serialize(pattern));
        await destination.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    private static void ParseSound(
        string content,
        PatternDocument document,
        IDictionary<ushort, PatternSound> soundsById)
    {
        var separator = content.IndexOfAny([' ', '\t']);
        var idToken = separator < 0 ? content[3..] : content[3..separator];
        var id = ParseHexUShort(idToken, "WAV id");
        var fileName = separator < 0 ? string.Empty : content[(separator + 1)..].Trim();
        var sound = new PatternSound { Id = id, FileName = fileName };
        document.Sounds.Add(sound);
        soundsById[id] = sound;
    }

    private static void ParseSoundFlags(IReadOnlyList<string> tokens, IDictionary<ushort, PatternSound> soundsById)
    {
        if (tokens.Count < 3)
            throw new InvalidDataException("SOUND_FLAGS requires an id and flags value.");

        var id = ParseHexUShort(tokens[1], "sound id");
        var flags = ParseUInt16(tokens[2], "sound flags");
        if (soundsById.TryGetValue(id, out var sound))
        {
            sound.Flags = flags;
        }
    }

    private static PatternTrack ParseTrackStart(IReadOnlyList<string> tokens, int fallbackId)
    {
        var startPosition = ParseInt(tokens, 0, "track position");
        var id = tokens.Count > 2 ? checked((short)ParseInt(tokens, 2, "track id")) : checked((short)fallbackId);
        var name = tokens.Count > 3 ? tokens[3] : string.Empty;
        var declaredCount = tokens.Count > 4 ? ParseInt(tokens, 4, "track command count") : 0;
        var shiftedCount = tokens.Count > 5 ? ParseInt(tokens, 5, "shifted command count") : 0;
        var track = new PatternTrack
        {
            Id = id,
            Name = name,
            StartPosition = startPosition,
            DeclaredCommandCount = declaredCount,
            DeclaredShiftedCommandCount = shiftedCount
        };

        foreach (var token in tokens.Skip(6))
        {
            if (token.StartsWith("end=", StringComparison.OrdinalIgnoreCase))
                track.EndPosition = ParseIntValue(token[4..], "track end position");
            else if (token.StartsWith("data=", StringComparison.OrdinalIgnoreCase))
                track.DeclaredDataSize = ParseIntValue(token[5..], "track data size");
        }

        return track;
    }

    private static PatternCommand ParseCommand(IReadOnlyList<string> tokens)
    {
        var position = ParseInt(tokens, 0, "event position");
        var typeToken = tokens[1];
        var raw = ParseRaw(tokens);

        if (typeToken.Equals("NOTE", StringComparison.OrdinalIgnoreCase))
        {
            var command = new PatternCommand
            {
                Position = position,
                Type = (byte)PatternCommandType.Note,
                SoundIndex = ParseHexUShort(GetRequired(tokens, 2, "note sound id"), "note sound id"),
                Volume = ParseByte(GetRequired(tokens, 3, "note volume"), "note volume"),
                Pan = ParseByte(GetRequired(tokens, 4, "note pan"), "note pan"),
                Attribute = ParseByte(GetRequired(tokens, 5, "note attribute"), "note attribute"),
                Length = ParseByte(GetRequired(tokens, 6, "note length"), "note length"),
                NoteUnknown = ParseUInt16(GetRequired(tokens, 7, "note unknown"), "note unknown"),
                RawParameters = raw ?? new byte[8]
            };
            ApplyKnownFields(command);
            return command;
        }

        if (typeToken.Equals("VOLUME", StringComparison.OrdinalIgnoreCase))
        {
            var command = new PatternCommand
            {
                Position = position,
                Type = (byte)PatternCommandType.Volume,
                Volume = ParseByte(GetRequired(tokens, 2, "volume"), "volume"),
                RawParameters = raw ?? BuildVolumeRaw(tokens)
            };
            ApplyKnownFields(command);
            return command;
        }

        if (typeToken.Equals("BPM_CHANGE", StringComparison.OrdinalIgnoreCase))
        {
            var command = new PatternCommand
            {
                Position = position,
                Type = (byte)PatternCommandType.BpmChange,
                Bpm = ParseFloat(tokens, 2, "BPM"),
                RawParameters = raw ?? BuildBpmRaw(tokens)
            };
            ApplyKnownFields(command);
            return command;
        }

        if (typeToken.Equals("BEAT", StringComparison.OrdinalIgnoreCase)
            || typeToken == ((byte)PatternCommandType.Beat).ToString(CultureInfo.InvariantCulture))
        {
            var command = new PatternCommand
            {
                Position = position,
                Type = (byte)PatternCommandType.Beat,
                Beat = ParseUInt16(GetRequired(tokens, 2, "beat"), "beat"),
                RawParameters = raw ?? new byte[8]
            };
            ApplyKnownFields(command);
            return command;
        }

        var type = checked((byte)ParseInt(tokens, 1, "event type"));
        var unknownValue = tokens.Count > 2 && !tokens[2].StartsWith("raw=", StringComparison.OrdinalIgnoreCase)
            ? ParseInt64(tokens[2], "unknown event value")
            : 0;
        return new PatternCommand
        {
            Position = position,
            Type = type,
            RawParameters = raw ?? BitConverter.GetBytes(unknownValue)
        };
    }

    private static byte[] BuildVolumeRaw(IReadOnlyList<string> tokens)
    {
        var raw = new byte[8];
        for (var index = 0; index < 3 && tokens.Count > index + 3; index++)
            raw[index + 1] = ParseByte(tokens[index + 3], $"volume parameter {index + 1}");

        if (tokens.Count > 6)
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), ParseInt(tokens, 6, "volume unknown"));
        return raw;
    }

    private static byte[] BuildBpmRaw(IReadOnlyList<string> tokens)
    {
        var raw = new byte[8];
        if (tokens.Count > 3 && !tokens[3].StartsWith("raw=", StringComparison.OrdinalIgnoreCase))
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), ParseInt(tokens, 3, "BPM unknown"));
        return raw;
    }

    private static void AppendCommand(StringBuilder builder, PatternCommand command)
    {
        var raw = BuildCanonicalParameters(command);
        builder.Append('#').Append(command.Position).Append(' ');
        switch (command.KnownType)
        {
            case PatternCommandType.Note:
                builder.Append("NOTE ").Append(command.SoundIndex.ToString("X4", CultureInfo.InvariantCulture))
                    .Append(' ').Append(command.Volume)
                    .Append(' ').Append(command.Pan)
                    .Append(' ').Append(command.Attribute)
                    .Append(' ').Append(command.Length)
                    .Append(' ').Append(command.NoteUnknown)
                    .Append(' ');
                break;
            case PatternCommandType.Volume:
                builder.Append("VOLUME ").Append(command.Volume)
                    .Append(' ').Append(raw[1])
                    .Append(' ').Append(raw[2])
                    .Append(' ').Append(raw[3])
                    .Append(' ').Append(BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4)))
                    .Append(' ');
                break;
            case PatternCommandType.BpmChange:
                builder.Append("BPM_CHANGE ")
                    .Append(command.Bpm.ToString("R", CultureInfo.InvariantCulture)).Append(' ');
                break;
            case PatternCommandType.Beat:
                builder.Append("4 ").Append(command.Beat).Append(' ');
                break;
            default:
                builder.Append(command.Type).Append(' ')
                    .Append(BinaryPrimitives.ReadInt64LittleEndian(raw)).Append(' ');
                break;
        }

        builder.Append("raw=").AppendLine(Convert.ToHexString(raw));
    }

    private static byte[] BuildCanonicalParameters(PatternCommand command)
    {
        var raw = command.RawParameters.ToArray();
        switch (command.KnownType)
        {
            case PatternCommandType.Note:
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0, 2), command.SoundIndex);
                raw[2] = command.Volume;
                raw[3] = command.Pan;
                raw[4] = command.Attribute;
                raw[5] = command.Length;
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6, 2), command.NoteUnknown);
                break;
            case PatternCommandType.Volume:
                raw[0] = command.Volume;
                break;
            case PatternCommandType.BpmChange:
                BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0, 4), BitConverter.SingleToInt32Bits(command.Bpm));
                break;
            case PatternCommandType.Beat:
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0, 2), command.Beat);
                break;
        }

        return raw;
    }

    private static void ApplyKnownFields(PatternCommand command)
    {
        var raw = command.RawParameters.ToArray();
        switch (command.KnownType)
        {
            case PatternCommandType.Note:
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0, 2), command.SoundIndex);
                raw[2] = command.Volume;
                raw[3] = command.Pan;
                raw[4] = command.Attribute;
                raw[5] = command.Length;
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6, 2), command.NoteUnknown);
                break;
            case PatternCommandType.Volume:
                raw[0] = command.Volume;
                break;
            case PatternCommandType.BpmChange:
                BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0, 4), BitConverter.SingleToInt32Bits(command.Bpm));
                break;
            case PatternCommandType.Beat:
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0, 2), command.Beat);
                break;
        }

        command.RawParameters = raw;
    }

    private static string? GetRawToken(IReadOnlyList<string> tokens)
        => tokens.FirstOrDefault(token => token.StartsWith("raw=", StringComparison.OrdinalIgnoreCase))?[4..];

    private static byte[]? ParseRaw(IReadOnlyList<string> tokens)
    {
        var value = GetRawToken(tokens);
        if (value is null)
            return null;

        try
        {
            var raw = Convert.FromHexString(value);
            return raw.Length == 8
                ? raw
                : throw new InvalidDataException("raw command parameters must contain exactly 8 bytes.");
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("raw command parameters must be hexadecimal.", ex);
        }
    }

    private static List<string> Tokenize(string value)
    {
        var tokens = new List<string>();
        var token = new StringBuilder();
        char quote = '\0';

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    if (index + 1 < value.Length && value[index + 1] == quote)
                    {
                        token.Append(quote);
                        index++;
                    }
                    else
                    {
                        quote = '\0';
                    }
                }
                else
                {
                    token.Append(character);
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            else
            {
                token.Append(character);
            }
        }

        if (quote != '\0')
            throw new InvalidDataException("Pattern text contains an unterminated quoted value.");
        if (token.Length > 0)
            tokens.Add(token.ToString());
        return tokens;
    }

    private static string GetRequired(IReadOnlyList<string> tokens, int index, string name)
        => tokens.Count > index ? tokens[index] : throw new InvalidDataException($"Pattern text is missing {name}.");

    private static bool IsInteger(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private static int ParseInt(IReadOnlyList<string> tokens, int index, string name)
        => ParseIntValue(GetRequired(tokens, index, name), name);

    private static int ParseIntValue(string value, string name)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"Invalid {name} value '{value}'.");

    private static long ParseInt64(string value, string name)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"Invalid {name} value '{value}'.");

    private static float ParseFloat(IReadOnlyList<string> tokens, int index, string name)
        => float.TryParse(GetRequired(tokens, index, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"Invalid {name} value '{tokens[index]}'.");

    private static byte ParseByte(string value, string name)
        => byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"Invalid {name} value '{value}'.");

    private static ushort ParseUInt16(string value, string name)
        => ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"Invalid {name} value '{value}'.");

    private static ushort ParseHexUShort(string value, string name)
        => ushort.TryParse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"Invalid {name} value '{value}'.");

    private static string EscapeQuotedValue(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
