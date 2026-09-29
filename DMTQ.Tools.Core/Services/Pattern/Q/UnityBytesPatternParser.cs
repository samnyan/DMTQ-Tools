using System.Buffers.Binary;
using DMTQ.Parser.Pattern;
using DMTQ.Tools.Core.Models.Pattern;

namespace DMTQ.Tools.Core.Services.Pattern.Q;

/// <summary>Maps the shared game-compatible parser output to the editor model.</summary>
public sealed class UnityBytesPatternParser
{
    private readonly PatternBytesParser _parser = new();

    public PatternDocument Parse(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new InvalidDataException("Pattern data is empty.");
        }

        var parsed = _parser.Parse(data.ToArray());
        var header = parsed.Header;
        var document = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        document.Header.BytesMagic = unchecked((int)parsed.VersionAndSignature);
        document.Header.BytesTick = header.Tick;
        document.Header.BytesPlayTime = header.PlayTime;
        document.Header.PositionsPerMeasure = checked((short)header.TicksPerMeasure);
        document.Header.InitialBpm = header.Tempo;
        document.Header.EndPosition = header.EndTick != 0 ? header.EndTick : header.Tick;
        document.Header.TagB = BitConverter.SingleToInt32Bits(header.PlayTime);
        document.Header.TagC = unchecked((int)header.EndTick);
        document.Header.DeclaredCommandCount = checked((int)header.TotalEventCount);

        foreach (var sourceSound in parsed.Sounds)
        {
            document.Sounds.Add(new PatternSound
            {
                Id = sourceSound.Id,
                Flags = sourceSound.Stream,
                FileName = sourceSound.Name
            });
        }

        foreach (var sourceTrack in parsed.Tracks)
        {
            var track = new PatternTrack
            {
                Id = checked((short)sourceTrack.Type),
                Name = sourceTrack.Name,
                EndPosition = sourceTrack.Length,
                DeclaredCommandCount = checked((int)sourceTrack.EventCount)
            };
            foreach (var sourceEvent in sourceTrack.Events)
            {
                var command = new PatternCommand
                {
                    Position = sourceEvent.Tick,
                    Type = sourceEvent.Type,
                    RawParameters = sourceEvent.RawData
                };
                if (sourceEvent.InstrumentId.HasValue)
                {
                    command.SoundIndex = sourceEvent.InstrumentId.Value;
                    command.Volume = sourceEvent.Velocity.GetValueOrDefault();
                    command.Pan = sourceEvent.Pan.GetValueOrDefault();
                    command.Attribute = sourceEvent.Attribute.GetValueOrDefault();
                    command.BytesDuration = sourceEvent.Duration;
                    command.Length = unchecked((byte)sourceEvent.Duration.GetValueOrDefault());
                    command.NoteUnknown = BinaryPrimitives.ReadUInt16LittleEndian(sourceEvent.RawData.AsSpan(6, 2));
                }
                if (sourceEvent.Volume.HasValue) command.Volume = sourceEvent.Volume.Value;
                if (sourceEvent.Tempo.HasValue) command.Bpm = sourceEvent.Tempo.Value;
                if (sourceEvent.Beat.HasValue) command.Beat = sourceEvent.Beat.Value;
                command.BytesUnknownData1 = sourceEvent.UnknownData1;
                command.BytesUnknownData2 = sourceEvent.UnknownData2;
                track.Commands.Add(command);
            }
            track.StartPosition = track.Commands.Count == 0 ? 0 : track.Commands[0].Position;
            document.Tracks.Add(track);
        }

        return document;
    }
}
