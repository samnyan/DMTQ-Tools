using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using System.Reflection;
using Microsoft.FluentUI.AspNetCore.Components;

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

        cut.Markup.Should().Contain("PROD_001");
        cut.Markup.Should().Contain("platform.sku");
    }

    [TestMethod]
    public void ProductEditor_RendersExistingProductForm()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreateSamplePackage();
        package.Products[0].Status = "N";
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<ProductEditor>(parameters => parameters.Add(p => p.ProductId, "PROD_001"));

        cut.Markup.Should().Contain("PROD_001");
        cut.Markup.Should().Contain("platform.sku");
        cut.Markup.Should().Contain("CAT_SONG");
        cut.Markup.Should().Contain("cash — Unmapped type code");
        cut.Markup.Should().Contain("N — No badge");
        cut.Markup.Should().Contain("fluent-button type=\"submit\"");
    }

    [TestMethod]
    public void ItemPage_RendersLocalizedNameAndColumns()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Items>();

        cut.Markup.Should().Contain("ITEM_001");
        cut.Markup.Should().Contain("中文道具");
    }

    [TestMethod]
    public void ItemEditor_RendersAllDefaultLanguageSections()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<ItemEditor>(parameters => parameters.Add(p => p.ItemId, "ITEM_001"));

        cut.Markup.Should().Contain("ITEM_001");
        foreach (var language in new[] { "CN", "JP", "KR", "TW", "US" })
            cut.Markup.Should().Contain(language);
        cut.Markup.Should().NotContain("礼物");
        cut.Markup.Should().NotContain("Gift");
        cut.Markup.Should().Contain("L — Level restriction unlock price tier");
        cut.Markup.Should().Contain("fluent-button type=\"submit\"");
    }

    [TestMethod]
    public void ItemEditor_UsesCsvEffectCodesInTheLocalizedSelector()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<ItemEditor>(parameters => parameters.Add(p => p.ItemId, "ITEM_001"));

        cut.Markup.Should().Contain("E — Experience increase");
        cut.Markup.Should().Contain("P — Maximum points");
        cut.Markup.Should().Contain("F — Fever increase");
        cut.Markup.Should().NotContain("fever —");

        var effectDraft = GetPrivateField<IngameItemEffect>(cut.Instance, "effectDraft");
        cut.Instance.GetType().GetMethod("SetEffectType", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, ["R — Recovery"]);
        effectDraft.EffectType.Should().Be("R");
    }

    [TestMethod]
    public async Task ItemEditor_SyncOriginalText_CopiesAndLiveUpdatesLocalizedFields()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);
        var cut = Render<ItemEditor>(parameters => parameters.Add(p => p.ItemId, "ITEM_001"));

        await cut.InvokeAsync(async () =>
        {
            await cut.FindComponents<FluentSwitch>()
                .Single()
                .Instance.ValueChanged.InvokeAsync(true);
            await cut.FindComponents<FluentTextInput>()
                .ElementAt(1)
                .Instance.ValueChanged.InvokeAsync("Live item");
            await cut.FindComponents<FluentTextArea>()
                .ElementAt(0)
                .Instance.ValueChanged.InvokeAsync("Live description");
            await cut.FindComponents<FluentTextArea>()
                .ElementAt(1)
                .Instance.ValueChanged.InvokeAsync("Live summary");
        });

        var draft = GetPrivateField<Item>(cut.Instance, "currentItem");
        foreach (var language in new[] { "CN", "JP", "KR", "TW", "US" })
        {
            draft.NamesByLanguage[language].Should().Be("Live item");
            draft.DescriptionsByLanguage[language].Should().Be("Live description");
            draft.SummariesByLanguage[language].Should().Be("Live summary");
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
        => (T)(instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance) ?? throw new InvalidOperationException($"Field '{fieldName}' was not found."));

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
