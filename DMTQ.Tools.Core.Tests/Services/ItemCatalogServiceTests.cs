using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ItemCatalogServiceTests
{
    [TestMethod]
    public void BuildCatalog_SortsByItemIdAndResolvesLocalizedFallbacks()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, "1.003.005", null)
        };
        var second = new Item { Id = "ITEM_002", ItemName = "Base 2" };
        var first = new Item { Id = "ITEM_001", ItemName = "Base 1" };
        first.NamesByLanguage["cn"] = "名称1";
        package.Items.Add(second);
        package.Items.Add(first);

        var service = new ItemCatalogService();
        var result = service.BuildCatalog(package);

        result.Select(item => item.Id).Should().Equal("ITEM_001", "ITEM_002");
        service.GetDisplayName(first, "CN").Should().Be("名称1");
        service.GetDisplayName(second, "JP").Should().Be("Base 2");
    }
}
