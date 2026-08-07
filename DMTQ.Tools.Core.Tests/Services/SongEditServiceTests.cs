using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongEditServiceTests
{
    [TestMethod]
    public void CreateDraft_CopiesSongWithoutSharingNestedState()
    {
        var source = new Song { Id = 1001, Name = "source" };
        source.Localizations["CN"] = new SongLocalization { SongId = 1001, FullName = "Source" };
        source.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001, Name = "pattern" });

        var draft = new SongEditService().CreateDraft(source);

        draft.Should().NotBeSameAs(source);
        draft.Localizations["CN"].Should().NotBeSameAs(source.Localizations["CN"]);
        draft.Patterns[0].Should().NotBeSameAs(source.Patterns[0]);
        draft.Name = "draft";
        draft.Localizations["CN"].FullName = "Draft";
        draft.Patterns[0].Name = "draft pattern";
        source.Name.Should().Be("source");
        source.Localizations["CN"].FullName.Should().Be("Source");
        source.Patterns[0].Name.Should().Be("pattern");
    }

    [TestMethod]
    public void UpdateSong_AppliesDetachedDraftToExistingSong()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001, Name = "before" });
        var draft = new SongEditService().CreateDraft(package.Songs[0]);
        draft.Name = "after";
        draft.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001 });

        new SongEditService().UpdateSong(package, draft);

        package.Songs[0].Name.Should().Be("after");
        package.Songs[0].Patterns.Should().ContainSingle();
        package.Songs[0].Should().NotBeSameAs(draft);
    }

    [TestMethod]
    public void UpdateSong_ValidatesSongExistsInSongsCollection()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });

        var action = () => new SongEditService().UpdateSong(package, new Song { Id = 1001 });
        action.Should().NotThrow();
    }

    [TestMethod]
    public void UpdateSong_ThrowsWhenSongDoesNotExist()
    {
        var package = CreatePackage();

        var action = () => new SongEditService().UpdateSong(package, new Song { Id = 9999 });
        action.Should().Throw<InvalidOperationException>().WithMessage("Song '9999' was not found.");
    }

    [TestMethod]
    public void AddSong_AppendsToSongsCollection()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });

        var song = new Song { Id = 1002 };
        song.Name = "NewSong";
        song.ArtistName = "NewArtist";
        song.PreviewPackageRelativePath = "preview/new.p.opus";
        song.Patterns.Add(new SongPattern { PatternId = 9002, SongId = 1002 });
        song.Patterns[0].Difficulty = 3;

        new SongEditService().AddSong(package, song);

        package.Songs.Should().HaveCount(2);
        var added = package.Songs.Single(s => s.Id == 1002);
        added.Name.Should().Be("NewSong");
        added.ArtistName.Should().Be("NewArtist");
        added.PreviewPackageRelativePath.Should().Be("preview/new.p.opus");
        added.Patterns.Should().ContainSingle();
        added.Patterns[0].PatternId.Should().Be(9002);
    }

    [TestMethod]
    public void AddSong_ThrowsWhenSongAlreadyExists()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });

        var action = () => new SongEditService().AddSong(package, new Song { Id = 1001 });
        action.Should().Throw<InvalidOperationException>().WithMessage("Song '1001' already exists.");
    }

    [TestMethod]
    public void AddPattern_AppendsToSongPatternsList()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });
        package.Songs[0].Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001 });

        var pattern = new SongPattern { PatternId = 9002, SongId = 1001 };
        pattern.Signature = 3;
        pattern.Line = 4;

        new SongEditService().AddPattern(package, 1001, pattern);

        package.Songs[0].Patterns.Should().HaveCount(2);
        var added = package.Songs[0].Patterns.Single(p => p.PatternId == 9002);
        added.Signature.Should().Be(3);
        added.Line.Should().Be(4);
    }

    [TestMethod]
    public void AddPattern_ThrowsWhenPatternAlreadyExists()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });
        package.Songs[0].Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001 });

        var action = () => new SongEditService().AddPattern(package, 1001,
            new SongPattern { PatternId = 9001, SongId = 1001 });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Pattern '9001' already exists for song '1001'.");
    }

    [TestMethod]
    public void AddPattern_ThrowsWhenSongDoesNotExist()
    {
        var package = CreatePackage();

        var action = () => new SongEditService().AddPattern(package, 9999,
            new SongPattern { PatternId = 9001, SongId = 9999 });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Song '9999' does not exist.");
    }

    [TestMethod]
    public void UpdatePattern_ValidatesPatternExists()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });
        package.Songs[0].Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001 });

        var updated = new SongPattern { PatternId = 9001, SongId = 1001 };
        updated.Difficulty = 3;

        var action = () => new SongEditService().UpdatePattern(package, 1001, 9001, updated);
        action.Should().NotThrow();
    }

    [TestMethod]
    public void UpdatePattern_OnDraftAppliesChangesWithoutMutatingOriginalPattern()
    {
        var service = new SongEditService();
        var song = new Song { Id = 1001 };
        song.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001, Difficulty = 1 });
        var updated = new SongPattern { PatternId = 9001, SongId = 1001, Difficulty = 5 };

        service.UpdatePattern(song, 9001, updated);

        song.Patterns[0].Difficulty.Should().Be(5);
    }

    [TestMethod]
    public void RemovePattern_OnDraftRemovesPattern()
    {
        var service = new SongEditService();
        var song = new Song { Id = 1001 };
        song.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001 });

        service.RemovePattern(song, 9001);

        song.Patterns.Should().BeEmpty();
    }

    [TestMethod]
    public void UpdatePattern_ThrowsWhenPatternNotFound()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });

        var action = () => new SongEditService().UpdatePattern(package, 1001, 9001,
            new SongPattern { PatternId = 9001, SongId = 1001 });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Pattern '9001' for song '1001' was not found.");
    }

    [TestMethod]
    public void RemoveSong_RemovesFromSongsCollection()
    {
        var package = CreatePackage();
        package.Songs.Add(new Song { Id = 1001 });
        package.Songs.Add(new Song { Id = 1002 });

        new SongEditService().RemoveSong(package, 1001);

        package.Songs.Should().HaveCount(1);
        package.Songs[0].Id.Should().Be(1002);
    }

    [TestMethod]
    public void RemoveSong_ThrowsWhenSongDoesNotExist()
    {
        var package = CreatePackage();

        var action = () => new SongEditService().RemoveSong(package, 9999);
        action.Should().Throw<InvalidOperationException>().WithMessage("Song '9999' was not found.");
    }

    private static PatchPackage CreatePackage()
        => new() { ProjectInfo = new ProjectInfo("project", null, "1.003.005", null) };
}
