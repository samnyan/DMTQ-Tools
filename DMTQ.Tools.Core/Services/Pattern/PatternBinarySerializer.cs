using System.Buffers.Binary;
using System.Text;
using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern.Q;

namespace DMTQ.Tools.Core.Services.Pattern;

/// <summary>
/// Reads and writes both supported binary pattern formats through the common
/// <see cref="PatternDocument"/> domain model.
/// </summary>
public sealed class PatternBinarySerializer
{
    private const int PtHeaderSize = 0x18;
    private const int PtSoundEntrySizePadded = 0x44;
    private const int PtSoundEntrySizeUnpadded = 0x42;
    private const int PtTrackHeaderSizePadded = 0x50;
    private const int PtTrackHeaderSizeUnpadded = 0x4E;
    private const int PtCommandSizePadded = 0x10;
    private const int PtCommandSizeUnpadded = 0x0B;
    private const int PtNameSize = 64;
    private static readonly Encoding PtNameEncoding = CreatePtNameEncoding();

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
            PtVersion = IsSupportedPtVersion(pattern.Header.PtVersion) ? pattern.Header.PtVersion : (short)1,
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

        var result = output.ToArray();
        return format == PatternFormat.Pt && options.EncryptPt ? PtCipher.Encrypt(result) : result;
    }

    private static PatternDocument ReadBytes(ReadOnlySpan<byte> data) => new UnityBytesPatternParser().Parse(data);

    private static PatternDocument ReadPt(ReadOnlySpan<byte> sourceData)
    {
        if (sourceData.Length < PtHeaderSize)
        {
            throw new InvalidDataException("PT pattern header is truncated.");
        }

        if (Encoding.ASCII.GetString(sourceData[..4]) != "PTFF")
        {
            throw new InvalidDataException("PT pattern does not start with PTFF.");
        }

        var version = BinaryPrimitives.ReadInt16LittleEndian(sourceData[4..]);
        if (!IsSupportedPtVersion(version))
        {
            throw new InvalidDataException($"Unsupported PT version {version}.");
        }

        try
        {
            return ReadPtData(sourceData, wasEncrypted: false);
        }
        catch (InvalidDataException plaintextError)
        {
            var decryptedData = PtCipher.Decrypt(sourceData);
            try
            {
                return ReadPtData(decryptedData, wasEncrypted: true);
            }
            catch (InvalidDataException decryptedError)
            {
                throw new InvalidDataException(
                    "PT pattern data could not be parsed.",
                    new AggregateException(plaintextError, decryptedError));
            }
        }
    }

    private static PatternDocument ReadPtData(ReadOnlySpan<byte> data, bool wasEncrypted)
    {
        var reader = new PatternByteReader(data.ToArray());
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
        if (!IsSupportedPtVersion(document.Header.PtVersion))
        {
            throw new InvalidDataException($"Unsupported PT version {document.Header.PtVersion}.");
        }

        var padded = IsPaddedPtVersion(document.Header.PtVersion);
        document.Header.PositionsPerMeasure = reader.ReadInt16();
        document.Header.InitialBpm = reader.ReadSingle();
        var declaredTrackCount = ReadNonNegativeCount(reader.ReadInt16(), "track");
        document.Header.EndPosition = reader.ReadUInt32();
        document.Header.TagB = reader.ReadInt32();
        var soundCount = ReadNonNegativeCount(reader.ReadInt16(), "sound");

        for (var i = 0; i < soundCount; i++)
        {
            EnsureRemaining(reader, padded ? PtSoundEntrySizePadded : PtSoundEntrySizeUnpadded, "PT sound table");
            var soundId = padded ? reader.ReadUInt16() : reader.ReadByte();
            if (soundId >= 1024)
            {
                throw new InvalidDataException($"Invalid PT instrument ID {soundId}.");
            }
            document.Sounds.Add(new PatternSound
            {
                Id = soundId,
                Flags = padded ? reader.ReadUInt16() : reader.ReadByte(),
                PtNameBytes = reader.ReadBytes(0x40)
            });
            document.Sounds[^1].FileName = DecodePtName(document.Sounds[^1].PtNameBytes!);
        }

        while (reader.Remaining > 0)
        {
            EnsureRemaining(reader, padded ? PtTrackHeaderSizePadded : PtTrackHeaderSizeUnpadded, "PT track");
            if (reader.ReadFixedAscii(4) != "EZTR")
            {
                throw new InvalidDataException($"PT track at offset {reader.Position - 4} does not start with EZTR.");
            }

            var trackField = reader.ReadUInt16();
            var track = new PatternTrack
            {
                Id = checked((short)document.Tracks.Count),
                PtHeaderField = trackField,
                PtNameBytes = reader.ReadBytes(0x40),
                EndPosition = reader.ReadUInt32(),
                DeclaredDataSize = reader.ReadInt32()
            };
            track.Name = DecodePtName(track.PtNameBytes!);
            if (padded)
            {
                track.PtTrailingField = reader.ReadUInt16();
            }

            if (track.DeclaredDataSize < 0 || track.DeclaredDataSize > reader.Remaining)
            {
                throw new InvalidDataException($"Invalid PT track data size {track.DeclaredDataSize}.");
            }

            var eventSize = GetPtCommandSize(padded);
            if (track.DeclaredDataSize % eventSize != 0)
            {
                throw new InvalidDataException("PT track data size is not a multiple of the version-specific event size.");
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
            writer.WriteUInt16(sound.Id);
            // Bytes stores a one-byte stream value while padded PT stores a 16-bit field.
            writer.WriteByte((byte)sound.Flags);
            writer.WriteFixedAscii(sound.FileName, 0x40);
        }

        foreach (var track in pattern.Tracks)
        {
            writer.WriteUInt16(unchecked((ushort)track.Id));
            writer.WriteFixedAscii(track.Name, 64);
            writer.WriteUInt32(checked((uint)(track.EndPosition != 0 ? track.EndPosition : GetTrackEndPosition(track))));
            writer.WriteUInt32(checked((uint)track.Commands.Count));

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
        writer.WriteUInt32(pattern.Header.BytesTick != 0
            ? pattern.Header.BytesTick
            : checked((uint)pattern.Header.EndPosition));
        writer.WriteSingle(pattern.Header.BytesPlayTime != 0
            ? pattern.Header.BytesPlayTime
            : BitConverter.Int32BitsToSingle(pattern.Header.TagB));
        writer.WriteUInt32(checked((uint)pattern.Header.EndPosition));
        writer.WriteUInt32(checked((uint)pattern.CommandCount));
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
        writer.WriteUInt32(checked((uint)pattern.Header.EndPosition));
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

            WritePtName(writer, sound.FileName, sound.PtNameBytes);
        }

        foreach (var track in pattern.Tracks)
        {
            var commandDataSize = checked(track.Commands.Count * GetPtCommandSize(padded));
            writer.WriteFixedAscii("EZTR", 4);
            writer.WriteUInt16(track.PtHeaderField);
            WritePtName(writer, track.Name, track.PtNameBytes);
            writer.WriteUInt32(checked((uint)(track.EndPosition != 0 ? track.EndPosition : GetTrackEndPosition(track))));
            writer.WriteInt32(commandDataSize);
            if (padded)
            {
                writer.WriteUInt16(track.PtTrailingField);
            }

            foreach (var command in track.Commands)
            {
                WritePtCommand(writer, command, padded);
            }
        }
    }

    private static PatternCommand ReadPtCommand(PatternByteReader reader, bool padded, int dataEnd)
    {
        EnsureRemaining(reader, 5, "PT command header");
        var command = new PatternCommand
        {
            Position = reader.ReadUInt32(),
            Type = reader.ReadByte()
        };

        if (padded)
        {
            EnsureRemaining(reader, 11, "padded PT command");
            command.PtPadding = reader.ReadBytes(3);
            command.RawParameters = reader.ReadBytes(8);
        }
        else
        {
            var parameters = new byte[8];
            const int parameterLength = 6; // All pre-1.0 events are exactly 11 bytes.

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
        writer.WriteUInt32(checked((uint)command.Position));
        writer.WriteByte(command.Type);
        writer.Write(BuildCanonicalParameters(command));
    }

    private static void WritePtCommand(PatternByteWriter writer, PatternCommand command, bool padded)
    {
        writer.WriteUInt32(checked((uint)command.Position));
        writer.WriteByte(command.Type);
        var parameters = BuildCanonicalParameters(command);
        if (padded)
        {
            writer.Write(command.PtPadding);
            writer.Write(parameters);
            return;
        }

        if (command.Type == (byte)PatternCommandType.Note)
        {
            EnsureByteSized(command.SoundIndex, "PT v0.6 note sound index");
            if (parameters[7] != 0)
            {
                throw new InvalidDataException("PT 0.6 cannot store the reserved final PT 1.0 note byte.");
            }
            writer.WriteByte((byte)command.SoundIndex);
            writer.Write(parameters.AsSpan(2, 4));
            writer.WriteByte(parameters[6]);
            return;
        }

        if (parameters[6] != 0 || parameters[7] != 0)
        {
            throw new InvalidDataException("PT 0.6 unknown/control payload exceeds six bytes.");
        }
        writer.Write(parameters.AsSpan(0, 6));
    }

    private static int GetPtCommandSize(bool padded)
        => padded ? PtCommandSizePadded : PtCommandSizeUnpadded;

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
                if (command.BytesDuration is { } duration)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(5, 2), duration);
                }
                else
                {
                    parameters[5] = command.Length;
                    BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(6, 2), command.NoteUnknown);
                }
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

    private static long GetTrackEndPosition(PatternTrack track)
        => track.Commands.Count == 0 ? track.StartPosition : track.Commands.Max(command => command.Position);

    private static Encoding CreatePtNameEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
    }

    private static string DecodePtName(byte[] raw)
    {
        var length = Array.IndexOf(raw, (byte)0);
        return PtNameEncoding.GetString(raw, 0, length < 0 ? raw.Length : length);
    }

    private static void WritePtName(PatternByteWriter writer, string name, byte[]? original)
    {
        if (original is { Length: PtNameSize } && DecodePtName(original) == name)
        {
            writer.Write(original);
            return;
        }
        ArgumentNullException.ThrowIfNull(name);
        var encoded = PtNameEncoding.GetBytes(name);
        if (encoded.Length > PtNameSize)
        {
            throw new InvalidDataException($"PT name exceeds {PtNameSize} bytes.");
        }

        writer.Write(encoded);
        writer.WriteZeros(PtNameSize - encoded.Length);
    }

    private static ushort GetPtVersionCode(short version)
        => BinaryPrimitives.ReverseEndianness(unchecked((ushort)version));

    private static bool IsPaddedPtVersion(short version) => GetPtVersionCode(version) == 0x0100;

    private static bool IsSupportedPtVersion(short version) => GetPtVersionCode(version) is 0x0100 or 0x0006;

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
        private readonly byte[] _data = data;

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

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(Position, 4));
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
            ArgumentNullException.ThrowIfNull(destination);
            Ensure(count);
            _data.AsSpan(Position, count).CopyTo(destination);
            Position += count;
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

        public void WriteUInt32(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
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
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            Span<byte> zeros = stackalloc byte[256];
            zeros.Clear();
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
