using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class SongPreviewLinkerTests
{
    [TestMethod]
    public void LinkPreviewResources_MatchesPreviewPathBuiltFromSongName()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, null, null)
        };
        var song = new Song { Id = 111, Name = "access" };
        package.Songs.Add(song);
        package.Resources.Add(new ResourceFile
        {
            FileName = "preview/access.p.opus",
            Category = "preview"
        });

        SongPreviewLinker.LinkPreviewResources(package);

        song.HasPreview.Should().BeTrue();
        song.PreviewPackageRelativePath.Should().Be("preview/access.p.opus");
    }

    [TestMethod]
    public void LinkPreviewResources_DoesNotUseFullNameOrSongIdAsFallback()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, null, null)
        };
        var song = new Song { Id = 111, Name = "access", FullName = "Access" };
        package.Songs.Add(song);
        package.Resources.Add(new ResourceFile
        {
            FileName = "preview/111.p.opus",
            Category = "preview"
        });

        SongPreviewLinker.LinkPreviewResources(package);

        song.HasPreview.Should().BeFalse();
    }
}
