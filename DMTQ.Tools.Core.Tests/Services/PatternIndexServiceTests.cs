using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PatternIndexServiceTests
{
    [TestMethod]
    public async Task ImportPatternFileAsync_WritesExtensionlessVariantAndDoesNotOverwriteExistingFile()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-import-").FullName;
        try
        {
            var service = new PatternIndexService(new PatternBinarySerializer());
            var expected = new byte[] { 1, 2, 3, 4 };
            await using (var content = new MemoryStream(expected))
                await service.ImportPatternFileAsync(root, 42, content, isHeadphone: true);

            var destination = Path.Combine(root, "Patterns", "42_EARPHONE");
            File.ReadAllBytes(destination).Should().Equal(expected);
            var replacement = new byte[] { 9 };
            var writeExisting = async () =>
            {
                await using var content = new MemoryStream(replacement);
                await service.ImportPatternFileAsync(root, 42, content, isHeadphone: true);
            };
            await writeExisting.Should().ThrowAsync<IOException>();
            File.ReadAllBytes(destination).Should().Equal(expected);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task RefreshAsync_GroupsVersionsByPatternIdAndPersistsBytesMetadata()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-index-").FullName;
        try
        {
            var patternDirectory = Directory.CreateDirectory(Path.Combine(root, "Patterns"));
            var serializer = new PatternBinarySerializer();
            var bytes = serializer.Serialize(CreatePattern(), PatternFormat.Bytes);
            await File.WriteAllBytesAsync(Path.Combine(patternDirectory.FullName, "42"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(patternDirectory.FullName, "42_EARPHONE"), bytes);
            await File.WriteAllTextAsync(Path.Combine(patternDirectory.FullName, "43"), "not a bytes pattern");
            await File.WriteAllTextAsync(Path.Combine(patternDirectory.FullName, "README.txt"), "ignored");

            var service = new PatternIndexService(serializer);
            var index = await service.RefreshAsync(root);

            index.Patterns.Should().HaveCount(2).And.ContainKeys(42, 43);
            index.Patterns[42].HasHeadphone.Should().BeTrue();
            index.Patterns[42].Standard.Should().BeEquivalentTo(index.Patterns[42].Headphone, options => options
                .Excluding(file => file!.RelativePath)
                .Excluding(file => file!.LastWriteTimeUtc));
            index.Patterns[42].Standard!.RelativePath.Should().Be("Patterns/42");
            index.Patterns[42].Headphone!.RelativePath.Should().Be("Patterns/42_EARPHONE");
            index.Patterns[42].Standard!.TrackCount.Should().Be(1);
            index.Patterns[42].Standard!.SoundCount.Should().Be(1);
            index.Patterns[43].HasHeadphone.Should().BeFalse();
            index.Patterns[43].Standard!.ParseError.Should().NotBeNullOrWhiteSpace();

            var persistedIndex = await service.LoadAsync(root);
            persistedIndex.LastIndexedAtUtc.Should().NotBeNull();
            persistedIndex.Patterns.Should().HaveCount(2).And.ContainKey(42);
            persistedIndex.Patterns[42].HasHeadphone.Should().BeTrue();
            using var savedJson = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "patterns.json")));
            savedJson.RootElement.GetProperty("Patterns").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
            savedJson.RootElement.GetProperty("Patterns").GetProperty("42")
                .GetProperty("HasHeadphone").GetBoolean().Should().BeTrue();
            File.Exists(Path.Combine(root, "patterns.json")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MigratesPriorVariantListIntoPatternIdKeys()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-index-legacy-").FullName;
        try
        {
            const string legacyJson = """
                {
                  "SchemaVersion": 1,
                  "LastIndexedAtUtc": "2026-09-29T00:00:00+00:00",
                  "Patterns": [
                    { "PatternId": 7, "IsEarphoneVariant": false, "RelativePath": "Patterns/7", "FileSize": 123 },
                    { "PatternId": 7, "IsEarphoneVariant": true, "RelativePath": "Patterns/7_EARPHONE", "FileSize": 124 }
                  ]
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(root, "patterns.json"), legacyJson);

            var index = await new PatternIndexService(new PatternBinarySerializer()).LoadAsync(root);

            index.SchemaVersion.Should().Be(3);
            index.Patterns.Should().ContainKey(7);
            index.Patterns[7].HasHeadphone.Should().BeTrue();
            index.Patterns[7].Standard!.RelativePath.Should().Be("Patterns/7");
            index.Patterns[7].Headphone!.RelativePath.Should().Be("Patterns/7_EARPHONE");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SyncSongPatternsAsync_CombinesSongRowsAndFilesAndRemovesStaleSongOnlyRows()
    {
        var root = Directory.CreateTempSubdirectory("dmtq-pattern-index-sync-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Patterns"));
            var serializer = new PatternBinarySerializer();
            await File.WriteAllBytesAsync(
                Path.Combine(root, "Patterns", "10"),
                serializer.Serialize(CreatePattern(), PatternFormat.Bytes));
            await File.WriteAllBytesAsync(
                Path.Combine(root, "Patterns", "30"),
                serializer.Serialize(CreatePattern(), PatternFormat.Bytes));
            var service = new PatternIndexService(serializer);
            await service.RefreshAsync(root);

            var synchronized = await service.SyncSongPatternsAsync(root,
            [
                new PatternSongReference(10, 100, "Song A"),
                new PatternSongReference(10, 101, "Song B"),
                new PatternSongReference(20, 200, "Song C")
            ]);

            synchronized.Patterns.Should().HaveCount(3);
            synchronized.Patterns[10].Standard.Should().NotBeNull();
            synchronized.Patterns[10].SongPatterns.Select(reference => reference.SongId)
                .Should().Equal(100, 101);
            synchronized.Patterns[20].Standard.Should().BeNull();
            synchronized.Patterns[20].SongPatterns.Should().ContainSingle()
                .Which.SongName.Should().Be("Song C");
            synchronized.Patterns[30].SongPatterns.Should().BeEmpty();
            synchronized.Patterns[30].Standard.Should().NotBeNull();

            var next = await service.SyncSongPatternsAsync(root,
            [
                new PatternSongReference(10, 100, "Song A Updated")
            ]);

            next.Patterns.Should().ContainKey(10).And.NotContainKey(20).And.ContainKey(30);
            next.Patterns[10].Standard.Should().NotBeNull();
            next.Patterns[10].SongPatterns.Single().SongName.Should().Be("Song A Updated");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PatternDocument CreatePattern()
    {
        var pattern = new PatternDocument { SourceFormat = PatternFormat.Bytes };
        pattern.Header.PositionsPerMeasure = 192;
        pattern.Header.InitialBpm = 128;
        pattern.Sounds.Add(new PatternSound { Id = 1, FileName = "sound.ogg" });
        var track = new PatternTrack { Id = 0, Name = "Top" };
        track.Commands.Add(PatternCommand.CreateNote(192, 1, 100, 64, 4, 0));
        pattern.Tracks.Add(track);
        return pattern;
    }
}
