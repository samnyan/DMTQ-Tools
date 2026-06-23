using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
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
        package.Resources.Add(new ResourceFile
        {
            FileName = "preview/a.opus",
            Category = "preview",
            Compressed = false,
            PlatformManifest =
            {
                new PlatformManifestEntry { Platform = "android", Exist = true },
                new PlatformManifestEntry { Platform = "ios", Exist = true }
            }
        });
        package.Resources.Add(new ResourceFile
        {
            FileName = "dlc/a.bundle",
            Category = "dlc",
            Compressed = true,
            PlatformManifest =
            {
                new PlatformManifestEntry { Platform = "android", Exist = true }
            }
        });

        var catalog = new ResourceManagerService().BuildCatalog(package);

        catalog.Should().HaveCount(2);
        catalog[0].Category.Should().Be("dlc");
        catalog[0].PlatformManifest.Should().Contain(m => m.Platform == "android");
        catalog[1].Category.Should().Be("preview");
        catalog[1].PlatformManifest.Should().HaveCount(2);
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
        resource.FileName.Should().Be("preview/new.opus");
        resource.Category.Should().Be("preview");
        resource.PlatformManifest.Should().Contain(m => m.Platform == "android" && m.Exist);
        var manifest = resource.PlatformManifest.First(m => m.Platform == "android");
        manifest.Checksum.Should().MatchRegex("^[0-9a-f]{32}$");
        manifest.SourceFileSize.Should().Be("preview-bytes".Length);
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
        resource.FileName.Should().Be("dlc/new.bundle");
        resource.PlatformManifest.Should().Contain(m => m.Platform == "ios" && m.Exist);
        var manifest = resource.PlatformManifest.First(m => m.Platform == "ios");
        manifest.Checksum.Should().MatchRegex("^[0-9a-f]{32}$");
        manifest.SourceFileSize.Should().Be("dlc-bytes".Length);
        resource.Compressed.Should().BeTrue();
        File.ReadAllText(Path.Combine(projectRoot, "resources", "ios", "dlc", "new.bundle")).Should().Be("dlc-bytes");
        Directory.Delete(projectRoot, recursive: true);
    }

    [TestMethod]
    public void SetCompressionAndPreviewPlatforms_UpdateExistingResource()
    {
        var package = CreatePackage();
        package.Resources.Add(new ResourceFile
        {
            FileName = "preview/a.opus",
            Category = "preview",
            Compressed = false,
            PlatformManifest =
            {
                new PlatformManifestEntry { Platform = "android", Exist = true }
            }
        });

        var service = new ResourceManagerService();
        service.SetCompression(package, "preview/a.opus", platform: null, compressed: true);
        service.SetPreviewIncludedPlatforms(package, "preview/a.opus", ["ios"]);

        var resource = package.Resources.Single();
        resource.Compressed.Should().BeTrue();
        resource.PlatformManifest.Should().Contain(m => m.Platform == "ios" && m.Exist);
    }

    [TestMethod]
    public void RemoveResource_RemovesOnlyMatchingPlatformResource()
    {
        var package = CreatePackage();
        package.Resources.Add(new ResourceFile
        {
            FileName = "dlc/a.bundle",
            Category = "dlc",
            Compressed = true,
            PlatformManifest =
            {
                new PlatformManifestEntry { Platform = "android", Exist = true }
            }
        });
        package.Resources.Add(new ResourceFile
        {
            FileName = "dlc/a.bundle",
            Category = "dlc",
            Compressed = true,
            PlatformManifest =
            {
                new PlatformManifestEntry { Platform = "ios", Exist = true }
            }
        });

        new ResourceManagerService().RemoveResource(package, "dlc/a.bundle", "android");

        // Since we now key by FileName only, removing by name removes all entries with that name
        package.Resources.Should().BeEmpty();
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
