using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class PathClassifierTests
{
    [TestMethod]
    public void NormalizePackageRelativePath_ConvertsBackslashes()
    {
        var path = PathClassifier.NormalizePackageRelativePath(@"table\us\song_song.csv");

        path.Should().Be("table/us/song_song.csv");
    }

    [TestMethod]
    public void NormalizePackageRelativePath_RejectsParentTraversal()
    {
        var action = () => PathClassifier.NormalizePackageRelativePath("../outside.bin");

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*unsafe package path*");
    }

    [TestMethod]
    public void NormalizePackageRelativePath_RejectsRootedPaths()
    {
        var rooted = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "outside.bin"));

        var action = () => PathClassifier.NormalizePackageRelativePath(rooted);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*unsafe package path*");
    }
}
