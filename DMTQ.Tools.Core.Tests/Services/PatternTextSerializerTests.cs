using System.Buffers.Binary;
using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatternTextSerializerTests
{
    private readonly PatternTextSerializer _serializer = new();

    [TestMethod]
    public void Roundtrip_PreservesMetadataFlagsTrackNamesAndRawParameters()
    {
        var source = CreatePattern();

        var text = _serializer.Serialize(source);
        var actual = _serializer.Deserialize(text);

        actual.SourceFormat.Should().Be(PatternFormat.Text);
        actual.Header.BytesMagic.Should().Be(source.Header.BytesMagic);
        actual.Header.PtVersion.Should().Be(source.Header.PtVersion);
        actual.Header.DeclaredCommandCount.Should().Be(source.Header.DeclaredCommandCount);
        actual.Sounds[0].Flags.Should().Be(source.Sounds[0].Flags);
        actual.Tracks[0].Name.Should().Be(source.Tracks[0].Name);
        actual.Tracks[0].EndPosition.Should().Be(source.Tracks[0].EndPosition);
        actual.Tracks[0].Commands.Should().HaveSameCount(source.Tracks[0].Commands);
        actual.Tracks[0].Commands[0].RawParameters.Should().Equal(source.Tracks[0].Commands[0].RawParameters);
        actual.Tracks[0].Commands[1].RawParameters.Should().Equal(source.Tracks[0].Commands[1].RawParameters);
    }

    [TestMethod]
    public void Deserialize_AcceptsLegacyBytesTextSyntax()
    {
        const string text = """
            #SOUND_COUNT 1
            #TRACK_COUNT 1
            #POSITION_PER_MEASURE 192
            #BPM 120
            #END_POSITION 3840
            #TAGB 7
            #TAGC 3840
            #TOTOAL_CMD_COUNT 3
            #WAV0001 kick.ogg
            POSITION COMMAND PARAMETER
            #0 TRACK_START 0 'Main Lane' 3
            #192 NOTE 0001 127 64 5 6 48879
            #384 127 -123
            #768 BPM_CHANGE 140 1234
            """;

        var actual = _serializer.Deserialize(text);

        actual.Sounds.Should().ContainSingle().Which.FileName.Should().Be("kick.ogg");
        actual.Tracks.Should().ContainSingle();
        actual.Tracks[0].Name.Should().Be("Main Lane");
        actual.Tracks[0].Commands.Should().HaveCount(3);
        actual.Tracks[0].Commands[0].KnownType.Should().Be(PatternCommandType.Note);
        actual.Tracks[0].Commands[1].Type.Should().Be(127);
        BinaryPrimitives.ReadInt64LittleEndian(actual.Tracks[0].Commands[1].RawParameters).Should().Be(-123);
        actual.Tracks[0].Commands[2].Bpm.Should().Be(140);
        BinaryPrimitives.ReadInt32LittleEndian(actual.Tracks[0].Commands[2].RawParameters.AsSpan(4)).Should().Be(1234);
    }

    [TestMethod]
    public void TextToBytesToText_PreservesEditableContent()
    {
        var source = CreatePattern();
        var bytesSerializer = new PatternBinarySerializer();

        var text = _serializer.Serialize(source);
        var bytes = bytesSerializer.Serialize(_serializer.Deserialize(text), PatternFormat.Bytes);
        var fromBytes = bytesSerializer.Deserialize(bytes, PatternFormat.Bytes);
        var actual = _serializer.Deserialize(_serializer.Serialize(fromBytes));

        actual.Sounds[0].FileName.Should().Be(source.Sounds[0].FileName);
        actual.Tracks[0].Commands.Should().HaveSameCount(source.Tracks[0].Commands);
        actual.Tracks[0].Commands[1].RawParameters.Should().Equal(source.Tracks[0].Commands[1].RawParameters);
    }

    [TestMethod]
    public void Deserialize_RejectsInvalidRawParameterExtension()
    {
        const string text = "#0 TRACK_START 0 '' 1\n#1 99 0 raw=ABC";

        var action = () => _serializer.Deserialize(text);

        action.Should().Throw<InvalidDataException>();
    }

    private static PatternDocument CreatePattern()
    {
        var pattern = new PatternDocument
        {
            SourceFormat = PatternFormat.Bytes
        };
        pattern.Header.BytesMagic = 42;
        pattern.Header.PtVersion = 1536;
        pattern.Header.PositionsPerMeasure = 192;
        pattern.Header.InitialBpm = 128.5f;
        pattern.Header.EndPosition = 4096;
        pattern.Header.TagB = 7;
        pattern.Header.TagC = 4096;
        pattern.Header.DeclaredCommandCount = 2;
        pattern.Sounds.Add(new PatternSound { Id = 1, Flags = 3, FileName = "kick.ogg" });

        var track = new PatternTrack
        {
            Id = 2,
            Name = "Main 'Lane'",
            StartPosition = 0,
            EndPosition = 4096,
            DeclaredCommandCount = 2,
            DeclaredShiftedCommandCount = 32
        };
        var note = PatternCommand.CreateNote(192, 1, 127, 64, 5, 6, 48879);
        note.RawParameters = [1, 0, 127, 64, 5, 6, 0xEF, 0xBE];
        track.Commands.Add(note);
        track.Commands.Add(new PatternCommand
        {
            Position = 384,
            Type = 127,
            RawParameters = [1, 2, 3, 4, 5, 6, 7, 8]
        });
        pattern.Tracks.Add(track);
        return pattern;
    }
}
