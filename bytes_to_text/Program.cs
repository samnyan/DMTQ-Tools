using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DMTQ.Parser.Pattern;

namespace bytes_to_text
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("DMTQ Tools - Bytes converter");
                Console.WriteLine("Usage: bytes_to_text.exe <file.bytes> [more files...]  - Convert Bytes to text.");
                Console.WriteLine("Usage: bytes_to_text.exe <file.txt> [more files...]     - Convert text to Unity Bytes.");
                return 0;
            }

            var failures = 0;
            foreach (var inputPath in args)
            {
                try
                {
                    var fullPath = Path.GetFullPath(inputPath);
                    if (String.Equals(Path.GetExtension(fullPath), ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        var pattern = ReadText(File.ReadAllText(fullPath, Encoding.UTF8));
                        var outputPath = Path.Combine(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath) + "_converted.bytes");
                        File.WriteAllBytes(outputPath, WriteBytes(pattern));
                        Console.WriteLine("Converted {0} -> {1}", Path.GetFileName(fullPath), Path.GetFileName(outputPath));
                    }
                    else
                    {
                        var pattern = ReadBytes(File.ReadAllBytes(fullPath));
                        var outputPath = Path.ChangeExtension(fullPath, ".txt");
                        File.WriteAllText(outputPath, WriteText(pattern), new UTF8Encoding(false));
                        Console.WriteLine("Converted {0} -> {1}", Path.GetFileName(fullPath), Path.GetFileName(outputPath));
                    }
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("Failed to convert {0}: {1}", inputPath, exception.Message);
                }
            }

            return failures == 0 ? 0 : 1;
        }

        private static Pattern ReadBytes(byte[] data)
        {
            var parsed = new PatternBytesParser().Parse(data);
            var header = parsed.Header;
            var pattern = new Pattern
            {
                Magic = unchecked((int)parsed.VersionAndSignature),
                SoundCount = header.InstrumentCount,
                TrackCount = header.TrackCount,
                PositionsPerMeasure = header.TicksPerMeasure,
                Bpm = header.Tempo,
                Tick = header.Tick,
                PlayTime = header.PlayTime,
                EndPosition = header.EndTick != 0 ? header.EndTick : header.Tick,
                DeclaredCommandCount = header.TotalEventCount,
                LegacyTagB = BitConverter.ToInt32(BitConverter.GetBytes(header.PlayTime), 0),
                LegacyTagC = unchecked((int)header.EndTick)
            };

            foreach (var sourceSound in parsed.Sounds)
                pattern.Sounds.Add(new Sound { Id = sourceSound.Id, Flags = sourceSound.Stream, Name = sourceSound.Name });

            foreach (var sourceTrack in parsed.Tracks)
            {
                var track = new Track
                {
                    Id = sourceTrack.Type,
                    Name = sourceTrack.Name,
                    EndPosition = sourceTrack.Length,
                    DeclaredCount = checked((int)sourceTrack.EventCount)
                };
                foreach (var sourceEvent in sourceTrack.Events)
                {
                    track.Events.Add(new Event
                    {
                        Position = unchecked((int)sourceEvent.Tick),
                        Type = sourceEvent.Type,
                        Data = sourceEvent.RawData
                    });
                }
                track.StartPosition = track.Events.Count == 0 ? 0 : track.Events[0].Position;
                pattern.Tracks.Add(track);
            }
            return pattern;
        }
        private static Pattern ReadText(string text)
        {
            var pattern = new Pattern();
            Track currentTrack = null;
            foreach (var sourceLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = sourceLine.Trim();
                if (!line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var tokens = Tokenize(line.Substring(1).TrimStart());
                if (tokens.Count == 0)
                    continue;

                var key = tokens[0].ToUpperInvariant();
                if (key.StartsWith("WAV", StringComparison.Ordinal))
                {
                    var id = ParseHex(key.Substring(3));
                    var nameStart = line.IndexOf(' ');
                    pattern.Sounds.Add(new Sound { Id = (ushort)id, Name = nameStart < 0 ? String.Empty : line.Substring(nameStart + 1).Trim() });
                    continue;
                }

                var value = tokens.Count > 1 ? tokens[1] : "0";
                switch (key)
                {
                    case "SOUND_COUNT": pattern.SoundCount = ParseUShort(value); break;
                    case "TRACK_COUNT": pattern.TrackCount = ParseUShort(value); break;
                    case "BYTES_MAGIC": pattern.Magic = ParseInt(value); break;
                    case "BYTES_TICK": pattern.Tick = ParseUInt(value); break;
                    case "BYTES_PLAY_TIME": pattern.PlayTime = ParseFloat(value); break;
                    case "POSITION_PER_MEASURE": pattern.PositionsPerMeasure = ParseUShort(value); break;
                    case "BPM": pattern.Bpm = ParseFloat(value); break;
                    case "END_POSITION": pattern.EndPosition = ParseUInt(value); break;
                    case "TAGB": pattern.LegacyTagB = ParseInt(value); break;
                    case "TAGC": pattern.LegacyTagC = ParseInt(value); break;
                    case "TOTOAL_CMD_COUNT":
                    case "TOTAL_CMD_COUNT": pattern.DeclaredCommandCount = ParseUInt(value); break;
                    case "SOUND_FLAGS":
                        if (tokens.Count > 2)
                        {
                            var sound = pattern.Sounds.FirstOrDefault(item => item.Id == ParseHex(tokens[1]));
                            if (sound != null) sound.Flags = checked((byte)ParseUInt(tokens[2]));
                        }
                        break;
                    default:
                        int position;
                        if (!Int32.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out position) || tokens.Count < 2)
                            break;
                        if (String.Equals(tokens[1], "TRACK_START", StringComparison.OrdinalIgnoreCase))
                        {
                            currentTrack = new Track
                            {
                                StartPosition = position,
                                Id = tokens.Count > 2 ? ParseUShort(tokens[2]) : (ushort)pattern.Tracks.Count,
                                Name = tokens.Count > 3 ? tokens[3] : String.Empty,
                                DeclaredCount = tokens.Count > 4 ? ParseInt(tokens[4]) : 0,
                                ShiftedCount = tokens.Count > 5 ? ParseInt(tokens[5]) : 0
                            };
                            foreach (var token in tokens.Skip(6))
                            {
                                if (token.StartsWith("end=", StringComparison.OrdinalIgnoreCase)) currentTrack.EndPosition = ParseUInt(token.Substring(4));
                            }
                            pattern.Tracks.Add(currentTrack);
                        }
                        else
                        {
                            if (currentTrack == null)
                                throw new InvalidDataException("A pattern event appears before TRACK_START.");
                            currentTrack.Events.Add(ParseEvent(position, tokens));
                        }
                        break;
                }
            }

            pattern.SoundCount = (ushort)pattern.Sounds.Count;
            pattern.TrackCount = (ushort)pattern.Tracks.Count;
            foreach (var track in pattern.Tracks)
            {
                if (track.EndPosition == 0)
                    track.EndPosition = track.Events.Count == 0 ? (uint)Math.Max(0, track.StartPosition) : (uint)track.Events.Max(item => item.Position);
                if (track.DeclaredCount == 0)
                    track.DeclaredCount = track.Events.Count;
                if (track.ShiftedCount == 0)
                    track.ShiftedCount = track.Events.Count << 4;
            }
            return pattern;
        }

        private static Event ParseEvent(int position, IList<string> tokens)
        {
            var typeName = tokens[1];
            var item = new Event { Position = position };
            var raw = tokens.FirstOrDefault(token => token.StartsWith("raw=", StringComparison.OrdinalIgnoreCase));
            if (raw != null)
            {
                item.Data = ParseHexBytes(raw.Substring(4));
                if (item.Data.Length != 8) throw new InvalidDataException("raw= must contain eight bytes.");
            }

            if (String.Equals(typeName, "NOTE", StringComparison.OrdinalIgnoreCase))
            {
                item.Type = 1;
                item.Data = new byte[8];
                WriteUShort(item.Data, 0, (ushort)ParseHex(tokens[2]));
                item.Data[2] = ParseByte(tokens[3]);
                item.Data[3] = ParseByte(tokens[4]);
                item.Data[4] = ParseByte(tokens[5]);
                item.Data[5] = ParseByte(tokens[6]);
                WriteUShort(item.Data, 6, ParseUShort(tokens[7]));
            }
            else if (String.Equals(typeName, "VOLUME", StringComparison.OrdinalIgnoreCase))
            {
                item.Type = 2;
                if (raw == null)
                {
                    item.Data = new byte[8];
                    item.Data[0] = ParseByte(tokens[2]);
                    for (var index = 0; index < 3 && tokens.Count > index + 3; index++) item.Data[index + 1] = ParseByte(tokens[index + 3]);
                    if (tokens.Count > 6) WriteInt(item.Data, 4, ParseInt(tokens[6]));
                }
                else item.Data[0] = ParseByte(tokens[2]);
            }
            else if (String.Equals(typeName, "BPM_CHANGE", StringComparison.OrdinalIgnoreCase))
            {
                item.Type = 3;
                if (raw == null) item.Data = new byte[8];
                WriteFloat(item.Data, 0, ParseFloat(tokens[2]));
            }
            else if (String.Equals(typeName, "BEAT", StringComparison.OrdinalIgnoreCase) || typeName == "4")
            {
                item.Type = 4;
                if (raw == null) item.Data = new byte[8];
                item.Data[0] = ParseByte(tokens[2]);
            }
            else
            {
                item.Type = checked((byte)ParseByte(typeName));
                if (raw == null)
                {
                    item.Data = new byte[8];
                    if (tokens.Count > 2) WriteLong(item.Data, 0, Int64.Parse(tokens[2], CultureInfo.InvariantCulture));
                }
            }
            return item;
        }

        private static byte[] WriteBytes(Pattern pattern)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(pattern.Magic);
                writer.Write(0);
                foreach (var sound in pattern.Sounds)
                {
                    writer.Write(sound.Id);
                    writer.Write(sound.Flags);
                    WriteFixedAscii(writer, sound.Name, 64);
                }
                foreach (var track in pattern.Tracks)
                {
                    writer.Write(track.Id);
                    WriteFixedAscii(writer, track.Name, 64);
                    writer.Write(track.EndPosition != 0 ? track.EndPosition : (uint)Math.Max(0, track.StartPosition));
                    writer.Write((uint)track.Events.Count);
                    foreach (var item in track.Events)
                    {
                        writer.Write(item.Position);
                        writer.Write(item.Type);
                        writer.Write(item.Data);
                    }
                }

                var infoOffset = checked((int)stream.Position);
                writer.Write(checked((ushort)pattern.Sounds.Count));
                writer.Write(checked((ushort)pattern.Tracks.Count));
                writer.Write(pattern.PositionsPerMeasure);
                writer.Write(pattern.Bpm);
                writer.Write(pattern.Tick != 0 ? pattern.Tick : pattern.EndPosition);
                writer.Write(pattern.PlayTime != 0 ? pattern.PlayTime : BitConverter.ToSingle(BitConverter.GetBytes(pattern.LegacyTagB), 0));
                writer.Write(pattern.EndPosition != 0 ? pattern.EndPosition : unchecked((uint)pattern.LegacyTagC));
                writer.Write(pattern.DeclaredCommandCount != 0 ? pattern.DeclaredCommandCount : (uint)pattern.Tracks.Sum(track => track.Events.Count));
                stream.Position = 4;
                writer.Write(infoOffset);
                return stream.ToArray();
            }
        }

        private static string WriteText(Pattern pattern)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#FORMAT DMTQ_PATTERN_TEXT 1");
            builder.AppendLine("#BYTES_MAGIC " + pattern.Magic.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#BYTES_TICK " + pattern.Tick.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#BYTES_PLAY_TIME " + pattern.PlayTime.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine("#SOUND_COUNT " + pattern.Sounds.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#TRACK_COUNT " + pattern.Tracks.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#POSITION_PER_MEASURE " + pattern.PositionsPerMeasure.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#BPM " + pattern.Bpm.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine("#END_POSITION " + pattern.EndPosition.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#TAGB " + BitConverter.ToInt32(BitConverter.GetBytes(pattern.PlayTime), 0).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#TAGC " + pattern.EndPosition.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("#TOTOAL_CMD_COUNT " + pattern.DeclaredCommandCount.ToString(CultureInfo.InvariantCulture));
            foreach (var sound in pattern.Sounds)
            {
                builder.AppendLine("#WAV" + sound.Id.ToString("X4", CultureInfo.InvariantCulture) + " " + sound.Name);
                builder.AppendLine("#SOUND_FLAGS " + sound.Id.ToString("X4", CultureInfo.InvariantCulture) + " " + sound.Flags.ToString(CultureInfo.InvariantCulture));
            }
            builder.AppendLine("POSITION COMMAND PARAMETER");
            foreach (var track in pattern.Tracks)
            {
                builder.Append('#').Append(track.StartPosition).Append(" TRACK_START ").Append(track.Id)
                    .Append(" '").Append(track.Name.Replace("'", "''")).Append("' ")
                    .Append(track.DeclaredCount).Append(' ').Append(track.ShiftedCount)
                    .Append(" end=").Append(track.EndPosition).AppendLine(" data=0");
                foreach (var item in track.Events) AppendEvent(builder, item);
            }
            return builder.ToString();
        }

        private static void AppendEvent(StringBuilder builder, Event item)
        {
            builder.Append('#').Append(item.Position).Append(' ');
            if (item.Type == 1)
            {
                builder.Append("NOTE ").Append(ReadUShort(item.Data, 0).ToString("X4", CultureInfo.InvariantCulture))
                    .Append(' ').Append(item.Data[2]).Append(' ').Append(item.Data[3]).Append(' ').Append(item.Data[4]).Append(' ')
                    .Append(item.Data[5]).Append(' ').Append(ReadUShort(item.Data, 6));
            }
            else if (item.Type == 2)
                builder.Append("VOLUME ").Append(item.Data[0]).Append(' ').Append(item.Data[1]).Append(' ').Append(item.Data[2]).Append(' ').Append(item.Data[3]).Append(' ').Append(BitConverter.ToInt32(item.Data, 4));
            else if (item.Type == 3)
                builder.Append("BPM_CHANGE ").Append(BitConverter.ToSingle(item.Data, 0).ToString("R", CultureInfo.InvariantCulture)).Append(' ').Append(BitConverter.ToInt32(item.Data, 4));
            else if (item.Type == 4)
                builder.Append("4 ").Append(item.Data[0]);
            else
                builder.Append(item.Type).Append(' ').Append(BitConverter.ToInt64(item.Data, 0));
            builder.Append(" raw=").Append(BitConverter.ToString(item.Data).Replace("-", String.Empty)).AppendLine();
        }

        private static List<string> Tokenize(string value)
        {
            var result = new List<string>();
            var token = new StringBuilder();
            var quote = '\0';
            var started = false;
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        if (index + 1 < value.Length && value[index + 1] == quote) { token.Append(quote); index++; }
                        else quote = '\0';
                    }
                    else token.Append(current);
                }
                else if (current == '\'' || current == '"') { quote = current; started = true; }
                else if (Char.IsWhiteSpace(current))
                {
                    if (started) { result.Add(token.ToString()); token.Length = 0; started = false; }
                }
                else { token.Append(current); started = true; }
            }
            if (quote != '\0') throw new InvalidDataException("Text contains an unterminated quoted value.");
            if (started) result.Add(token.ToString());
            return result;
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int length)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? String.Empty);
            var output = new byte[length];
            Array.Copy(bytes, output, Math.Min(bytes.Length, output.Length));
            writer.Write(output);
        }

        private static byte[] ParseHexBytes(string value)
        {
            if (value.Length % 2 != 0) throw new InvalidDataException("Hex data must contain complete bytes.");
            var bytes = new byte[value.Length / 2];
            for (var index = 0; index < bytes.Length; index++) bytes[index] = Byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
        private static int ParseHex(string value) { return Int32.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        private static int ParseInt(string value) { return Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static uint ParseUInt(string value) { return UInt32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static ushort ParseUShort(string value) { return UInt16.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static byte ParseByte(string value) { return Byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture); }
        private static float ParseFloat(string value) { return Single.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private static ushort ReadUShort(byte[] bytes, int offset) { return BitConverter.ToUInt16(bytes, offset); }
        private static void WriteUShort(byte[] bytes, int offset, ushort value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 2); }
        private static void WriteInt(byte[] bytes, int offset, int value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4); }
        private static void WriteLong(byte[] bytes, int offset, long value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 8); }
        private static void WriteFloat(byte[] bytes, int offset, float value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4); }

        private sealed class Pattern
        {
            public int Magic;
            public ushort SoundCount;
            public ushort TrackCount;
            public ushort PositionsPerMeasure;
            public float Bpm;
            public uint Tick;
            public float PlayTime;
            public uint EndPosition;
            public uint DeclaredCommandCount;
            public int LegacyTagB;
            public int LegacyTagC;
            public readonly List<Sound> Sounds = new List<Sound>();
            public readonly List<Track> Tracks = new List<Track>();
        }
        private sealed class Sound { public ushort Id; public byte Flags; public string Name = String.Empty; }
        private sealed class Track
        {
            public ushort Id;
            public string Name = String.Empty;
            public int StartPosition;
            public uint EndPosition;
            public int DeclaredCount;
            public int ShiftedCount;
            public readonly List<Event> Events = new List<Event>();
        }
        private sealed class Event { public int Position; public byte Type; public byte[] Data = new byte[8]; }
    }
}
