using System.Buffers.Binary;
using System.Text;
using DMTQ.Tools.Core.Models.Pattern;

namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Reads and writes both supported binary pattern formats through the common
/// <see cref="PatternDocument"/> domain model.
/// </summary>
public sealed class PatternBinarySerializer
{
    private const int BytesHeaderSize = 8;
    private const int BytesSoundEntrySize = 0x43;
    private const int BytesTrackHeaderSize = 0x3D;
    private const int BytesCommandSize = 0x0D;
    private const int BytesInfoSize = 0x1A;
    private const int PtHeaderSize = 0x18;
    private const int PtSoundEntrySizePadded = 0x44;
    private const int PtSoundEntrySizeUnpadded = 0x42;
    private const int PtTrackHeaderSizePadded = 0x50;
    private const int PtTrackHeaderSizeUnpadded = 0x4E;
    private const int PtCommandSizePadded = 0x10;

    /// <summary>
    /// Deserializes a pattern from a stream.
    /// </summary>
    /// <param name="source">The stream containing the complete pattern.</param>
    /// <param name="format">The binary format of the source.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public PatternDocument Deserialize(Stream source, PatternFormat format)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return Deserialize(buffer.ToArray(), format);
    }

    /// <summary>
    /// Asynchronously deserializes a pattern from a stream.
    /// </summary>
    /// <param name="source">The stream containing the complete pattern.</param>
    /// <param name="format">The binary format of the source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public async Task<PatternDocument> DeserializeAsync(
        Stream source,
        PatternFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Deserialize(buffer.ToArray(), format);
    }

    /// <summary>
    /// Deserializes a pattern from an in-memory byte array.
    /// </summary>
    /// <param name="data">The complete pattern data.</param>
    /// <param name="format">The binary format of the source.</param>
    /// <returns>The format-neutral pattern document.</returns>
    public PatternDocument Deserialize(ReadOnlySpan<byte> data, PatternFormat format)
    {
        if (data.IsEmpty)
        {
            throw new InvalidDataException("Pattern data is empty.");
        }

        return format switch
        {
            PatternFormat.Bytes => ReadBytes(data),
            PatternFormat.Pt => ReadPt(data),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pattern format.")
        };
    }

    /// <summary>
    /// Serializes a pattern into a stream.
    /// </summary>
    /// <param name="pattern">The pattern document to serialize.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="format">The target binary format.</param>
    /// <param name="options">Optional target format settings.</param>
    public void Serialize(
        PatternDocument pattern,
        Stream destination,
        PatternFormat format,
        PatternSerializationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(destination);
        var data = Serialize(pattern, format, options);
        destination.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Asynchronously serializes a pattern into a stream.
    /// </summary>
    /// <param name="pattern">The pattern document to serialize.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="format">The target binary format.</param>
    /// <param name="options">Optional target format settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SerializeAsync(
        PatternDocument pattern,
        Stream destination,
        PatternFormat format,
        PatternSerializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(destination);
        var data = Serialize(pattern, format, options);
        await destination.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes a pattern into a new byte array.
    /// </summary>
    /// <param name="pattern">The pattern document to serialize.</param>
    /// <param name="format">The target binary format.</param>
    /// <param name="options">Optional target format settings.</param>
    /// <returns>The serialized pattern bytes.</returns>
    public byte[] Serialize(
        PatternDocument pattern,
        PatternFormat format,
        PatternSerializationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        options ??= new PatternSerializationOptions
        {
            PtVersion = pattern.Header.PtVersion is 1 or 1536 ? pattern.Header.PtVersion : (short)1,
            BytesMagic = pattern.Header.BytesMagic
        };

        using var output = new MemoryStream();
        switch (format)
        {
            case PatternFormat.Bytes:
                WriteBytes(pattern, output, options);
                break;
            case PatternFormat.Pt:
                WritePt(pattern, output, options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pattern format.");
        }

        return output.ToArray();
    }

    private static PatternDocument ReadBytes(ReadOnlySpan<byte> data)
    {
        var reader = new PatternByteReader(data.ToArray());
        var document = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        document.Header.BytesMagic = reader.ReadInt32();
        var infoOffset = reader.ReadInt32();

        if (infoOffset < BytesHeaderSize || infoOffset > reader.Length - BytesInfoSize)
        {
            throw new InvalidDataException($"Invalid bytes info offset {infoOffset}.");
        }

        reader.Position = infoOffset;
        var soundCount = ReadNonNegativeCount(reader.ReadInt16(), "sound");
        var trackCount = ReadNonNegativeCount(reader.ReadInt16(), "track");
        document.Header.PositionsPerMeasure = reader.ReadInt16();
        document.Header.InitialBpm = reader.ReadSingle();
        document.Header.EndPosition = reader.ReadInt32();
        document.Header.TagB = reader.ReadInt32();
        document.Header.TagC = reader.ReadInt32();
        document.Header.DeclaredCommandCount = reader.ReadInt32();

        reader.Position = BytesHeaderSize;
        for (var i = 0; i < soundCount; i++)
        {
            EnsureRemaining(reader, BytesSoundEntrySize, "sound table");
            document.Sounds.Add(new PatternSound
            {
                Id = reader.ReadUInt16(),
                Flags = reader.ReadByte(),
                FileName = reader.ReadFixedAscii(0x40)
            });
        }

        while (reader.Position < infoOffset)
        {
            EnsureRemaining(reader, BytesTrackHeaderSize + BytesCommandSize, "track");
            var track = new PatternTrack
            {
                Id = reader.ReadInt16(),
                Name = reader.ReadFixedAscii(0x3B)
            };

            track.StartPosition = reader.ReadInt32();
            var startType = reader.ReadByte();
            if (startType != (byte)PatternCommandType.TrackStart)
            {
                throw new InvalidDataException($"Bytes track at offset {reader.Position - 1} has no track-start marker.");
            }

            track.DeclaredShiftedCommandCount = reader.ReadInt32();
            track.DeclaredCommandCount = reader.ReadInt32();
            if (track.DeclaredCommandCount < 0 || track.DeclaredCommandCount > reader.Remaining / BytesCommandSize)
            {
                throw new InvalidDataException($"Invalid command count {track.DeclaredCommandCount} in bytes track.");
            }

            for (var commandIndex = 0; commandIndex < track.DeclaredCommandCount; commandIndex++)
            {
                track.Commands.Add(ReadFixedCommand(reader, padded: false));
            }

            track.EndPosition = track.Commands.Count == 0
                ? track.StartPosition
                : track.Commands.Max(command => command.Position);
            document.Tracks.Add(track);
        }

        if (reader.Position != infoOffset)
        {
            throw new InvalidDataException("Bytes track data does not end at the info block.");
        }

        if (document.Tracks.Count != trackCount)
        {
            throw new InvalidDataException($"Bytes header declares {trackCount} tracks but contains {document.Tracks.Count}.");
        }

        return document;
    }

    private static PatternDocument ReadPt(ReadOnlySpan<byte> sourceData)
    {
        var data = sourceData.ToArray();
        var wasEncrypted = data.Length > 0x18 && data[0x18] != 1;
        if (wasEncrypted)
        {
            data = PtCipher.Decrypt(data);
        }

        var reader = new PatternByteReader(data);
        var document = new PatternDocument
        {
            SourceFormat = PatternFormat.Pt,
            WasEncrypted = wasEncrypted
        };

        if (reader.ReadFixedAscii(4) != "PTFF")
        {
            throw new InvalidDataException("PT pattern does not start with PTFF.");
        }

        document.Header.PtVersion = reader.ReadInt16();
        var padded = IsPaddedPtVersion(document.Header.PtVersion);
        document.Header.PositionsPerMeasure = reader.ReadInt16();
        document.Header.InitialBpm = reader.ReadSingle();
        var declaredTrackCount = ReadNonNegativeCount(reader.ReadInt16(), "track");
        document.Header.EndPosition = reader.ReadInt32();
        document.Header.TagB = reader.ReadInt32();
        var soundCount = ReadNonNegativeCount(reader.ReadInt16(), "sound");

        for (var i = 0; i < soundCount; i++)
        {
            EnsureRemaining(reader, padded ? PtSoundEntrySizePadded : PtSoundEntrySizeUnpadded, "PT sound table");
            document.Sounds.Add(new PatternSound
            {
                Id = padded ? reader.ReadUInt16() : reader.ReadByte(),
                Flags = padded ? reader.ReadUInt16() : reader.ReadByte(),
                FileName = reader.ReadFixedAscii(0x40)
            });
        }

        while (reader.Remaining > 0)
        {
            EnsureRemaining(reader, padded ? PtTrackHeaderSizePadded : PtTrackHeaderSizeUnpadded, "PT track");
            if (reader.ReadFixedAscii(4) != "EZTR")
            {
                throw new InvalidDataException($"PT track at offset {reader.Position - 4} does not start with EZTR.");
            }

            _ = reader.ReadUInt16();
            var track = new PatternTrack
            {
                Id = checked((short)document.Tracks.Count),
                Name = reader.ReadFixedAscii(0x40),
                EndPosition = reader.ReadInt32(),
                DeclaredDataSize = reader.ReadInt32()
            };
            if (padded)
            {
                _ = reader.ReadUInt16();
            }

            if (track.DeclaredDataSize < 0 || track.DeclaredDataSize > reader.Remaining)
            {
                throw new InvalidDataException($"Invalid PT track data size {track.DeclaredDataSize}.");
            }

            var dataEnd = reader.Position + track.DeclaredDataSize;
            while (reader.Position < dataEnd)
            {
                var command = ReadPtCommand(reader, padded, dataEnd);
                track.Commands.Add(command);
            }

            if (reader.Position != dataEnd)
            {
                throw new InvalidDataException("PT track command data is not aligned to the track data size.");
            }

            track.DeclaredCommandCount = track.Commands.Count;
            track.StartPosition = track.Commands.Count == 0 ? 0 : track.Commands[0].Position;
            document.Tracks.Add(track);
        }

        if (document.Tracks.Count != declaredTrackCount)
        {
            throw new InvalidDataException($"PT header declares {declaredTrackCount} tracks but contains {document.Tracks.Count}.");
        }

        document.Header.DeclaredCommandCount = document.CommandCount;
        return document;
    }

    private static void WriteBytes(PatternDocument pattern, MemoryStream output, PatternSerializationOptions options)
    {
        var writer = new PatternByteWriter(output);
        writer.WriteInt32(options.BytesMagic);
        var infoOffsetPosition = writer.Position;
        writer.WriteInt32(0);

        foreach (var sound in pattern.Sounds)
        {
            EnsureByteSized(sound.Flags, "bytes sound flags");
            writer.WriteUInt16(sound.Id);
            writer.WriteByte((byte)sound.Flags);
            writer.WriteFixedAscii(sound.FileName, 0x40);
        }

        foreach (var track in pattern.Tracks)
        {
            writer.WriteInt16(track.Id);
            writer.WriteFixedAscii(track.Name, 0x3B);
            writer.WriteInt32(track.StartPosition);
            writer.WriteByte((byte)PatternCommandType.TrackStart);
            writer.WriteInt32(checked(track.Commands.Count << 4));
            writer.WriteInt32(track.Commands.Count);

            foreach (var command in track.Commands)
            {
                WriteFixedCommand(writer, command);
            }
        }

        var infoOffset = writer.Position;
        if (infoOffset > int.MaxValue)
        {
            throw new InvalidDataException("Pattern is too large for the bytes format.");
        }

        writer.WriteInt16(checked((short)pattern.Sounds.Count));
        writer.WriteInt16(checked((short)pattern.Tracks.Count));
        writer.WriteInt16(pattern.Header.PositionsPerMeasure);
        writer.WriteSingle(pattern.Header.InitialBpm);
        writer.WriteInt32(pattern.Header.EndPosition);
        writer.WriteInt32(pattern.Header.TagB);
        writer.WriteInt32(pattern.Header.TagC);
        writer.WriteInt32(pattern.CommandCount);
        writer.PatchInt32(infoOffsetPosition, checked((int)infoOffset));
    }

    private static void WritePt(PatternDocument pattern, MemoryStream output, PatternSerializationOptions options)
    {
        if (!IsSupportedPtVersion(options.PtVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.PtVersion, "Only PT versions 1 and 1536 are supported.");
        }

        var padded = IsPaddedPtVersion(options.PtVersion);
        var writer = new PatternByteWriter(output);
        writer.WriteFixedAscii("PTFF", 4);
        writer.WriteInt16(options.PtVersion);
        writer.WriteInt16(pattern.Header.PositionsPerMeasure);
        writer.WriteSingle(pattern.Header.InitialBpm);
        writer.WriteInt16(checked((short)pattern.Tracks.Count));
        writer.WriteInt32(pattern.Header.EndPosition);
        writer.WriteInt32(pattern.Header.TagB);
        writer.WriteInt16(checked((short)pattern.Sounds.Count));

        foreach (var sound in pattern.Sounds)
        {
            if (padded)
            {
                writer.WriteUInt16(sound.Id);
                writer.WriteUInt16(sound.Flags);
            }
            else
            {
                EnsureByteSized(sound.Id, "PT v0.6 sound id");
                EnsureByteSized(sound.Flags, "PT v0.6 sound flags");
                writer.WriteByte((byte)sound.Id);
                writer.WriteByte((byte)sound.Flags);
            }

            writer.WriteFixedAscii(sound.FileName, 0x40);
        }

        foreach (var track in pattern.Tracks)
        {
            var commandDataSize = track.Commands.Sum(command => GetPtCommandSize(command.Type, padded));
            writer.WriteFixedAscii("EZTR", 4);
            writer.WriteUInt16(0);
            writer.WriteFixedAscii(track.Name, 0x40);
            writer.WriteInt32(track.EndPosition != 0 ? track.EndPosition : GetTrackEndPosition(track));
            writer.WriteInt32(commandDataSize);
            if (padded)
            {
                writer.WriteUInt16(0);
            }

            foreach (var command in track.Commands)
            {
                WritePtCommand(writer, command, padded);
            }
        }
    }

    private static PatternCommand ReadFixedCommand(PatternByteReader reader, bool padded)
    {
        var command = new PatternCommand
        {
            Position = reader.ReadInt32(),
            Type = reader.ReadByte()
        };

        if (padded)
        {
            _ = reader.ReadBytes(3);
        }

        command.RawParameters = reader.ReadBytes(8);
        PopulateKnownFields(command);
        return command;
    }

    private static PatternCommand ReadPtCommand(PatternByteReader reader, bool padded, int dataEnd)
    {
        EnsureRemaining(reader, 5, "PT command header");
        var command = new PatternCommand
        {
            Position = reader.ReadInt32(),
            Type = reader.ReadByte()
        };

        if (padded)
        {
            EnsureRemaining(reader, 11, "padded PT command");
            _ = reader.ReadBytes(3);
            command.RawParameters = reader.ReadBytes(8);
        }
        else
        {
            var parameters = new byte[8];
            var parameterLength = command.Type switch
            {
                (byte)PatternCommandType.Note => 6,
                (byte)PatternCommandType.Volume => 6,
                (byte)PatternCommandType.BpmChange => 6,
                (byte)PatternCommandType.Beat => 6,
                _ => 8
            };

            if (reader.Position + parameterLength > dataEnd)
            {
                throw new InvalidDataException("PT command extends past its track data.");
            }

            if (command.Type == (byte)PatternCommandType.Note)
            {
                parameters[0] = reader.ReadByte();
                parameters[2] = reader.ReadByte();
                parameters[3] = reader.ReadByte();
                parameters[4] = reader.ReadByte();
                parameters[5] = reader.ReadByte();
                parameters[6] = reader.ReadByte();
            }
            else
            {
                reader.ReadBytesInto(parameters, parameterLength);
            }

            command.RawParameters = parameters;
        }

        PopulateKnownFields(command);
        return command;
    }

    private static void WriteFixedCommand(PatternByteWriter writer, PatternCommand command)
    {
        writer.WriteInt32(command.Position);
        writer.WriteByte(command.Type);
        writer.Write(BuildCanonicalParameters(command));
    }

    private static void WritePtCommand(PatternByteWriter writer, PatternCommand command, bool padded)
    {
        writer.WriteInt32(command.Position);
        writer.WriteByte(command.Type);
        var parameters = BuildCanonicalParameters(command);
        if (padded)
        {
            writer.WriteZeros(3);
            writer.Write(parameters);
            return;
        }

        if (command.Type == (byte)PatternCommandType.Note)
        {
            EnsureByteSized(command.SoundIndex, "PT v0.6 note sound index");
            writer.WriteByte((byte)command.SoundIndex);
            writer.Write(parameters.AsSpan(2, 4));
            writer.WriteByte(parameters[6]);
            return;
        }

        writer.Write(parameters.AsSpan(0, command.Type is
            (byte)PatternCommandType.Volume or
            (byte)PatternCommandType.BpmChange or
            (byte)PatternCommandType.Beat ? 6 : 8));
    }

    private static int GetPtCommandSize(byte commandType, bool padded)
        => padded
            ? 16
            : commandType is
                ((byte)PatternCommandType.Note or
                 (byte)PatternCommandType.Volume or
                 (byte)PatternCommandType.BpmChange or
                 (byte)PatternCommandType.Beat)
                ? 11
                : 13;

    private static byte[] BuildCanonicalParameters(PatternCommand command)
    {
        var parameters = command.RawParameters.ToArray();
        switch (command.Type)
        {
            case (byte)PatternCommandType.Note:
                BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(0, 2), command.SoundIndex);
                parameters[2] = command.Volume;
                parameters[3] = command.Pan;
                parameters[4] = command.Attribute;
                parameters[5] = command.Length;
                BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(6, 2), command.NoteUnknown);
                break;
            case (byte)PatternCommandType.Volume:
                parameters[0] = command.Volume;
                break;
            case (byte)PatternCommandType.BpmChange:
                BinaryPrimitives.WriteInt32LittleEndian(parameters.AsSpan(0, 4), BitConverter.SingleToInt32Bits(command.Bpm));
                break;
            case (byte)PatternCommandType.Beat:
                BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(0, 2), command.Beat);
                break;
        }

        return parameters;
    }

    private static void PopulateKnownFields(PatternCommand command)
    {
        var parameters = command.RawParameters.AsSpan();
        switch (command.Type)
        {
            case (byte)PatternCommandType.Note:
                command.SoundIndex = BinaryPrimitives.ReadUInt16LittleEndian(parameters[..2]);
                command.Volume = parameters[2];
                command.Pan = parameters[3];
                command.Attribute = parameters[4];
                command.Length = parameters[5];
                command.NoteUnknown = BinaryPrimitives.ReadUInt16LittleEndian(parameters[6..8]);
                break;
            case (byte)PatternCommandType.Volume:
                command.Volume = parameters[0];
                break;
            case (byte)PatternCommandType.BpmChange:
                command.Bpm = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(parameters[..4]));
                break;
            case (byte)PatternCommandType.Beat:
                command.Beat = BinaryPrimitives.ReadUInt16LittleEndian(parameters[..2]);
                break;
        }
    }

    private static int GetTrackEndPosition(PatternTrack track)
        => track.Commands.Count == 0 ? track.StartPosition : track.Commands.Max(command => command.Position);

    private static bool IsPaddedPtVersion(short version) => version == 1;

    private static bool IsSupportedPtVersion(short version) => version is 1 or 1536;

    private static int ReadNonNegativeCount(short value, string kind)
        => value >= 0 ? value : throw new InvalidDataException($"Invalid {kind} count {value}.");

    private static void EnsureRemaining(PatternByteReader reader, int count, string section)
    {
        if (reader.Remaining < count)
        {
            throw new InvalidDataException($"Pattern ended while reading {section}.");
        }
    }

    private static void EnsureByteSized(ushort value, string field)
    {
        if (value > byte.MaxValue)
        {
            throw new InvalidDataException($"{field} value {value} does not fit in one byte.");
        }
    }

    private sealed class PatternByteReader(byte[] data)
    {
        private readonly byte[] _data = data.ToArray();

        public int Length => _data.Length;
        public int Position { get; set; }
        public int Remaining => Length - Position;

        public byte ReadByte()
        {
            Ensure(1);
            return _data[Position++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(Position, 2));
            Position += 2;
            return value;
        }

        public short ReadInt16() => unchecked((short)ReadUInt16());

        public int ReadInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(Position, 4));
            Position += 4;
            return value;
        }

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public byte[] ReadBytes(int count)
        {
            Ensure(count);
            var value = _data.AsSpan(Position, count).ToArray();
            Position += count;
            return value;
        }

        public void ReadBytesInto(byte[] destination, int count)
        {
            var bytes = ReadBytes(count);
            bytes.CopyTo(destination, 0);
        }

        public string ReadFixedAscii(int count)
        {
            var value = Encoding.ASCII.GetString(ReadBytes(count));
            var nullIndex = value.IndexOf('\0');
            return (nullIndex >= 0 ? value[..nullIndex] : value).TrimEnd(' ');
        }

        private void Ensure(int count)
        {
            if (count < 0 || Position < 0 || count > Remaining)
            {
                throw new InvalidDataException("Pattern contains truncated data.");
            }
        }
    }

    private sealed class PatternByteWriter(MemoryStream stream)
    {
        public long Position => stream.Position;

        public void Write(ReadOnlySpan<byte> value) => stream.Write(value);

        public void WriteByte(byte value) => stream.WriteByte(value);

        public void WriteUInt16(ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            Write(bytes);
        }

        public void WriteInt16(short value) => WriteUInt16(unchecked((ushort)value));

        public void WriteInt32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            Write(bytes);
        }

        public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));

        public void WriteFixedAscii(string value, int count)
        {
            ArgumentNullException.ThrowIfNull(value);
            var bytes = Encoding.ASCII.GetBytes(value);
            if (bytes.Length > count)
            {
                throw new InvalidDataException($"Text value '{value}' exceeds the fixed width of {count} bytes.");
            }

            Write(bytes);
            WriteZeros(count - bytes.Length);
        }

        public void WriteZeros(int count)
        {
            Span<byte> zeros = stackalloc byte[Math.Min(count, 256)];
            while (count > 0)
            {
                var chunk = Math.Min(count, zeros.Length);
                Write(zeros[..chunk]);
                count -= chunk;
            }
        }

        public void PatchInt32(long position, int value)
        {
            var current = stream.Position;
            stream.Position = position;
            WriteInt32(value);
            stream.Position = current;
        }
    }
}
