using System.Text;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ProductItemExportTests
{
    [TestMethod]
    public async Task ExportAsync_WritesProductItemAndLocalizedItemDescriptionTables()
    {
        var exportRoot = Path.Combine(Path.GetTempPath(), "dmtq-product-item-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var package = new PatchPackage
            {
                ProjectInfo = new ProjectInfo("project", null, "1.003.005", null)
            };
            var product = new Product
            {
                Id = "PROD_001",
                ItemId = "ITEM_001",
                ProductType = "cash"
            };
            product.CategoryIds.Add("CAT_SONG");
            package.Products.Add(product);

            var item = new Item { Id = "ITEM_001", ItemName = "Base Item" };
            item.NamesByLanguage["CN"] = "中文道具";
            item.NamesByLanguage["US"] = "English Item";
            package.Items.Add(item);

            var manifest = await new PatchPackageExporter().ExportAsync(package, exportRoot);

            var productPath = Path.Combine(exportRoot, "table", "cn", "product_product.csv");
            var itemPath = Path.Combine(exportRoot, "table", "cn", "product_item.csv");
            var descriptionPath = Path.Combine(exportRoot, "table", "cn", "item_desc_cn.csv");
            var usDescriptionPath = Path.Combine(exportRoot, "table", "us", "item_desc_us.csv");

            File.Exists(productPath).Should().BeTrue();
            File.Exists(itemPath).Should().BeTrue();
            File.Exists(descriptionPath).Should().BeTrue();
            File.Exists(usDescriptionPath).Should().BeTrue();
            manifest.Entries.Should().Contain(entry => entry.FileName == "table/cn/product_product.csv");

            (await File.ReadAllTextAsync(productPath, Encoding.UTF8)).Should().Contain("PROD_001");
            (await File.ReadAllTextAsync(itemPath, Encoding.UTF8)).Should().Contain("ITEM_001");
            (await File.ReadAllTextAsync(descriptionPath, Encoding.UTF8)).Should().Contain("中文道具");
            (await File.ReadAllTextAsync(usDescriptionPath, Encoding.UTF8)).Should().Contain("English Item");
        }
        finally
        {
            if (Directory.Exists(exportRoot))
                Directory.Delete(exportRoot, recursive: true);
        }
    }
}
