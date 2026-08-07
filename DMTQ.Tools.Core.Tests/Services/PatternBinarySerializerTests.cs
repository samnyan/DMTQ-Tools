using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatternBinarySerializerTests
{
    private readonly PatternBinarySerializer _serializer = new();

    [TestMethod]
    public void BytesRoundtrip_PreservesCommonPatternModel()
    {
        var source = CreatePattern();

        var bytes = _serializer.Serialize(source, PatternFormat.Bytes);
        var actual = _serializer.Deserialize(bytes, PatternFormat.Bytes);

        actual.SourceFormat.Should().Be(PatternFormat.Bytes);
        actual.Header.PositionsPerMeasure.Should().Be(source.Header.PositionsPerMeasure);
        actual.Header.InitialBpm.Should().Be(source.Header.InitialBpm);
        actual.Header.EndPosition.Should().Be(source.Header.EndPosition);
        actual.Header.TagB.Should().Be(source.Header.TagB);
        actual.Header.TagC.Should().Be(source.Header.TagC);
        AssertPatternContent(source, actual);
    }

    [TestMethod]
    public void PtVersionOneRoundtrip_PreservesCommonPatternModel()
    {
        var source = CreatePattern();

        var pt = _serializer.Serialize(source, PatternFormat.Pt, new PatternSerializationOptions { PtVersion = 1 });
        var actual = _serializer.Deserialize(pt, PatternFormat.Pt);

        actual.SourceFormat.Should().Be(PatternFormat.Pt);
        actual.Header.PtVersion.Should().Be(1);
        actual.Header.TagB.Should().Be(source.Header.TagB);
        AssertPatternContent(source, actual);
    }

    [TestMethod]
    public void PtVersionZeroPointSixRoundtrip_UsesUnpaddedCommandLayout()
    {
        var source = CreatePattern(singleByteSoundIndex: true);

        var pt = _serializer.Serialize(source, PatternFormat.Pt, new PatternSerializationOptions { PtVersion = 1536 });
        var actual = _serializer.Deserialize(pt, PatternFormat.Pt);

        actual.Header.PtVersion.Should().Be(1536);
        actual.Tracks.Should().HaveSameCount(source.Tracks);
        actual.CommandCount.Should().Be(source.CommandCount);
        actual.Tracks[0].Commands[0].SoundIndex.Should().Be(source.Tracks[0].Commands[0].SoundIndex);
        actual.Tracks[0].Commands[0].Volume.Should().Be(source.Tracks[0].Commands[0].Volume);
        actual.Tracks[0].Commands[0].NoteUnknown.Should().Be((byte)source.Tracks[0].Commands[0].NoteUnknown);
    }

    [TestMethod]
    public void BytesToPtToBytes_PreservesEditableContent()
    {
        var source = CreatePattern();

        var bytes = _serializer.Serialize(source, PatternFormat.Bytes);
        var fromBytes = _serializer.Deserialize(bytes, PatternFormat.Bytes);
        var pt = _serializer.Serialize(fromBytes, PatternFormat.Pt);
        var fromPt = _serializer.Deserialize(pt, PatternFormat.Pt);
        var convertedBytes = _serializer.Serialize(fromPt, PatternFormat.Bytes);
        var final = _serializer.Deserialize(convertedBytes, PatternFormat.Bytes);

        AssertPatternContent(source, final);
    }

    [TestMethod]
    public void Deserialize_RejectsTruncatedPattern()
    {
        var action = () => _serializer.Deserialize([0, 0, 0, 0, 8, 0, 0, 0], PatternFormat.Bytes);

        action.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public async Task AsyncStreamApi_RoundtripsPattern()
    {
        var source = CreatePattern();
        await using var stream = new MemoryStream();

        await _serializer.SerializeAsync(source, stream, PatternFormat.Pt);
        stream.Position = 0;
        var actual = await _serializer.DeserializeAsync(stream, PatternFormat.Pt);

        AssertPatternContent(source, actual);
    }

    private static PatternDocument CreatePattern(bool singleByteSoundIndex = false)
    {
        var soundIndex = singleByteSoundIndex ? (ushort)17 : (ushort)300;
        var pattern = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        pattern.Header.BytesMagic = 0x1234;
        pattern.Header.PtVersion = 1;
        pattern.Header.PositionsPerMeasure = 192;
        pattern.Header.InitialBpm = 128.5f;
        pattern.Header.EndPosition = 3840;
        pattern.Header.TagB = 123456;
        pattern.Header.TagC = 3840;
        pattern.Sounds.Add(new PatternSound { Id = 1, Flags = 0, FileName = "kick.ogg" });
        pattern.Sounds.Add(new PatternSound { Id = soundIndex, Flags = 2, FileName = "lead.ogg" });

        var firstTrack = new PatternTrack { Id = 0, Name = "Top", StartPosition = 0, EndPosition = 3840 };
        firstTrack.Commands.Add(PatternCommand.CreateNote(192, soundIndex, 127, 64, 5, 6, 0xBEEF));
        firstTrack.Commands.Add(PatternCommand.CreateVolume(384, 96));
        firstTrack.Commands.Add(PatternCommand.CreateBpmChange(768, 140.25f));
        firstTrack.Commands.Add(PatternCommand.CreateBeat(960, 4));
        firstTrack.Commands.Add(new PatternCommand
        {
            Position = 1024,
            Type = 0x7F,
            RawParameters = [1, 2, 3, 4, 5, 6, 7, 8]
        });

        var secondTrack = new PatternTrack { Id = 1, Name = "Middle", StartPosition = 0, EndPosition = 2560 };
        secondTrack.Commands.Add(PatternCommand.CreateNote(256, 1, 100, 32, 0, 6));
        pattern.Tracks.Add(firstTrack);
        pattern.Tracks.Add(secondTrack);
        return pattern;
    }

    private static void AssertPatternContent(PatternDocument expected, PatternDocument actual)
    {
        actual.Sounds.Should().HaveSameCount(expected.Sounds);
        for (var index = 0; index < expected.Sounds.Count; index++)
        {
            actual.Sounds[index].Id.Should().Be(expected.Sounds[index].Id);
            actual.Sounds[index].Flags.Should().Be(expected.Sounds[index].Flags);
            actual.Sounds[index].FileName.Should().Be(expected.Sounds[index].FileName);
        }

        actual.Tracks.Should().HaveSameCount(expected.Tracks);
        for (var trackIndex = 0; trackIndex < expected.Tracks.Count; trackIndex++)
        {
            var expectedTrack = expected.Tracks[trackIndex];
            var actualTrack = actual.Tracks[trackIndex];
            actualTrack.Id.Should().Be(expectedTrack.Id);
            actualTrack.Name.Should().Be(expectedTrack.Name);
            actualTrack.Commands.Should().HaveSameCount(expectedTrack.Commands);

            for (var commandIndex = 0; commandIndex < expectedTrack.Commands.Count; commandIndex++)
            {
                var expectedCommand = expectedTrack.Commands[commandIndex];
                var actualCommand = actualTrack.Commands[commandIndex];
                actualCommand.Position.Should().Be(expectedCommand.Position);
                actualCommand.Type.Should().Be(expectedCommand.Type);
                actualCommand.SoundIndex.Should().Be(expectedCommand.SoundIndex);
                actualCommand.Volume.Should().Be(expectedCommand.Volume);
                actualCommand.Pan.Should().Be(expectedCommand.Pan);
                actualCommand.Attribute.Should().Be(expectedCommand.Attribute);
                actualCommand.Length.Should().Be(expectedCommand.Length);
                actualCommand.NoteUnknown.Should().Be(expectedCommand.NoteUnknown);
                actualCommand.Bpm.Should().BeApproximately(expectedCommand.Bpm, 0.0001f);
                actualCommand.Beat.Should().Be(expectedCommand.Beat);
                if (expectedCommand.KnownType is null)
                {
                    actualCommand.RawParameters.Should().Equal(expectedCommand.RawParameters);
                }
            }
        }
    }
}
