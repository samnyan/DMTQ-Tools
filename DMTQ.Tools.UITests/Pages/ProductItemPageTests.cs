using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class ProductItemPageTests : BlazorUITestBase
{
    [TestMethod]
    public void ProductPage_RendersProductRowsAndColumns()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Products>();

        cut.Markup.Should().Contain("Products");
        cut.Markup.Should().Contain("PROD_001");
        cut.Markup.Should().Contain("Product ID");
        cut.Markup.Should().Contain("Add Product");
    }

    [TestMethod]
    public void ProductEditor_RendersExistingProductForm()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<ProductEditor>(parameters => parameters.Add(p => p.ProductId, "PROD_001"));

        cut.Markup.Should().Contain("Edit: PROD_001");
        cut.Markup.Should().Contain("Platform Product ID");
        cut.Markup.Should().Contain("Product categories");
        cut.Markup.Should().Contain("In-game configurations");
        cut.Markup.Should().Contain("CAT_SONG");
        cut.Markup.Should().Contain("Save Product");
    }

    [TestMethod]
    public void ItemPage_RendersLocalizedNameAndColumns()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Items>();

        cut.Markup.Should().Contain("Items");
        cut.Markup.Should().Contain("ITEM_001");
        cut.Markup.Should().Contain("中文道具");
        cut.Markup.Should().Contain("Localized names");
    }

    [TestMethod]
    public void ItemEditor_RendersAllDefaultLanguageSections()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<ItemEditor>(parameters => parameters.Add(p => p.ItemId, "ITEM_001"));

        cut.Markup.Should().Contain("Edit: ITEM_001");
        cut.Markup.Should().Contain("Localized Descriptions");
        cut.Markup.Should().Contain("Item effect");
        cut.Markup.Should().Contain("Effect point");
        foreach (var language in new[] { "CN", "JP", "KR", "TW", "US" })
            cut.Markup.Should().Contain(language);
        cut.Markup.Should().Contain("Save Item");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("test-project", null, "1.0", null)
        };
        var product = new Product
        {
            Id = "PROD_001",
            ItemId = "ITEM_001",
            PlatformProductId = "platform.sku",
            StoreProductId = "store.sku",
            ProductType = "cash",
            Status = "Y"
        };
        product.CategoryIds.Add("CAT_SONG");
        package.Products.Add(product);
        var item = new Item { Id = "ITEM_001", ItemName = "Base Item", ItemType = "boost", Status = "Y" };
        item.NamesByLanguage["CN"] = "中文道具";
        item.DescriptionsByLanguage["JP"] = "日本語説明";
        package.Items.Add(item);
        package.IngameItems.Add(new IngameItem { Id = "AB_1", ItemType = "AB", ItemLevel = "1", ProductId = "PROD_001" });
        package.IngameItemEffects.Add(new IngameItemEffect { Id = "ITEM_001", EffectType = "F", EffectPoint = "1.5" });
        return package;
    }
}
