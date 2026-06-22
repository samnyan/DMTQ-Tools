using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ResourceManagerServiceTests
{
    [TestMethod]
    public void BuildCatalog_GroupsResourcesByCategoryAndPlatformMetadata()
    {
        var package = CreatePackage();
        package.Resources.Add(new ResourceFile("preview/a.opus", "resources/preview/a.opus", "preview", false, null, null, ["android", "ios"]));
        package.Resources.Add(new ResourceFile("dlc/a.bundle", "resources/android/dlc/a.bundle", "dlc", true, null, "android", null));

        var catalog = new ResourceManagerService().BuildCatalog(package);

        catalog.Should().HaveCount(2);
        catalog[0].Category.Should().Be("dlc");
        catalog[0].Platform.Should().Be("android");
        catalog[1].Category.Should().Be("preview");
        catalog[1].IncludedPlatforms.Should().BeEquivalentTo("android", "ios");
    }

    [TestMethod]
    public async Task AddOrReplaceResourceAsync_AddsSharedPreviewToProjectArchive()
    {
        var projectRoot = CreateTempDirectory();
        var sourcePath = Path.Combine(projectRoot, "source.opus");
        await File.WriteAllTextAsync(sourcePath, "preview-bytes");
        var package = CreatePackage(projectRoot);

        await new ResourceManagerService().AddOrReplaceResourceAsync(
            package,
            sourcePath,
            "preview/new.opus",
            platform: null,
            includedPlatforms: ["android"],
            compressed: false);

        package.Resources.Should().ContainSingle();
        var resource = package.Resources[0];
        resource.PackageRelativePath.Should().Be("preview/new.opus");
        resource.ProjectRelativePath.Should().Be("resources/preview/new.opus");
        resource.Category.Should().Be("preview");
        resource.Platform.Should().BeNull();
        resource.IncludedPlatforms.Should().BeEquivalentTo("android");
        File.ReadAllText(Path.Combine(projectRoot, "resources", "preview", "new.opus")).Should().Be("preview-bytes");
        Directory.Delete(projectRoot, recursive: true);
    }

    [TestMethod]
    public async Task AddOrReplaceResourceAsync_AddsPlatformResourceToPlatformArchive()
    {
        var projectRoot = CreateTempDirectory();
        var sourcePath = Path.Combine(projectRoot, "dlc.bin");
        await File.WriteAllTextAsync(sourcePath, "dlc-bytes");
        var package = CreatePackage(projectRoot);

        await new ResourceManagerService().AddOrReplaceResourceAsync(
            package,
            sourcePath,
            "dlc/new.bundle",
            platform: "ios",
            includedPlatforms: [],
            compressed: true);

        var resource = package.Resources.Single();
        resource.ProjectRelativePath.Should().Be("resources/ios/dlc/new.bundle");
        resource.Platform.Should().Be("ios");
        resource.Compressed.Should().BeTrue();
        File.ReadAllText(Path.Combine(projectRoot, "resources", "ios", "dlc", "new.bundle")).Should().Be("dlc-bytes");
        Directory.Delete(projectRoot, recursive: true);
    }

    [TestMethod]
    public void SetCompressionAndPreviewPlatforms_UpdateExistingResource()
    {
        var package = CreatePackage();
        package.Resources.Add(new ResourceFile("preview/a.opus", "resources/preview/a.opus", "preview", false, null, null, ["android"]));

        var service = new ResourceManagerService();
        service.SetCompression(package, "preview/a.opus", platform: null, compressed: true);
        service.SetPreviewIncludedPlatforms(package, "preview/a.opus", ["ios"]);

        var resource = package.Resources.Single();
        resource.Compressed.Should().BeTrue();
        resource.IncludedPlatforms.Should().BeEquivalentTo("ios");
    }

    [TestMethod]
    public void RemoveResource_RemovesOnlyMatchingPlatformResource()
    {
        var package = CreatePackage();
        package.Resources.Add(new ResourceFile("dlc/a.bundle", "resources/android/dlc/a.bundle", "dlc", true, null, "android", null));
        package.Resources.Add(new ResourceFile("dlc/a.bundle", "resources/ios/dlc/a.bundle", "dlc", true, null, "ios", null));

        new ResourceManagerService().RemoveResource(package, "dlc/a.bundle", "android");

        package.Resources.Should().ContainSingle();
        package.Resources[0].Platform.Should().Be("ios");
    }

    private static PatchPackage CreatePackage(string? projectRoot = null)
        => new()
        {
            ProjectInfo = new ProjectInfo(projectRoot ?? "project", null, "1.003.005", null)
        };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dmtq-resource-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
