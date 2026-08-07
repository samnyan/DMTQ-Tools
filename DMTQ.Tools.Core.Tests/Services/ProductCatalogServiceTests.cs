using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ProductCatalogServiceTests
{
    [TestMethod]
    public void BuildCatalog_SortsByProductId()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", null, "1.003.005", null)
        };
        package.Products.Add(new Product { Id = "PROD_002" });
        package.Products.Add(new Product { Id = "PROD_001" });

        var result = new ProductCatalogService().BuildCatalog(package);

        result.Select(product => product.Id).Should().Equal("PROD_001", "PROD_002");
    }
}
