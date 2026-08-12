using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ProductEditServiceTests
{
    [TestMethod]
    public void CreateDraft_CopiesProductWithoutSharingCategories()
    {
        var source = new Product { Id = "PROD_001", ItemId = "ITEM_001" };
        source.CategoryIds.Add("CAT_SONG");

        var draft = new ProductEditService().CreateDraft(source);

        draft.Should().NotBeSameAs(source);
        draft.CategoryIds.Should().Equal("CAT_SONG");
        draft.CategoryIds.Should().NotBeSameAs(source.CategoryIds);

        draft.CategoryIds.Add("CAT_PREMIUM");
        source.CategoryIds.Should().Equal("CAT_SONG");
    }

    [TestMethod]
    public void UpdateProduct_AppliesDetachedDraft()
    {
        var package = CreatePackage();
        package.Products.Add(new Product { Id = "PROD_001", Status = "N" });
        var draft = new ProductEditService().CreateDraft(package.Products[0]);
        draft.Status = "Y";
        draft.CategoryIds.Add("CAT_SONG");

        new ProductEditService().UpdateProduct(package, draft);

        package.Products[0].Status.Should().Be("Y");
        package.Products[0].CategoryIds.Should().Equal("CAT_SONG");
        package.Products[0].Should().NotBeSameAs(draft);
    }

    [TestMethod]
    public void AddAndRemoveProduct_ManagePackageCollection()
    {
        var package = CreatePackage();
        var service = new ProductEditService();

        service.AddProduct(package, new Product { Id = "PROD_001", ItemId = "ITEM_001" });
        service.RemoveProduct(package, "prod_001");

        package.Products.Should().BeEmpty();
    }

    [TestMethod]
    public void AddProduct_RejectsDuplicateIdIgnoringCase()
    {
        var package = CreatePackage();
        package.Products.Add(new Product { Id = "PROD_001" });

        var action = () => new ProductEditService().AddProduct(package,
            new Product { Id = "prod_001" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Product 'prod_001' already exists.");
    }

    [TestMethod]
    public void ApplyIngameItemDrafts_ReplacesOnlyRowsLinkedToProduct()
    {
        var package = CreatePackage();
        package.IngameItems.Add(new IngameItem { Id = "AB_1", ItemType = "AB", ItemLevel = "1", ProductId = "P1" });
        package.IngameItems.Add(new IngameItem { Id = "FP_1", ItemType = "FP", ItemLevel = "1", ProductId = "P2" });
        var service = new ProductEditService();

        service.ApplyIngameItemDrafts(package, "P1",
            [new IngameItem { Id = "draft", ItemType = "AB", ItemLevel = "2", Update = "1" }]);

        package.IngameItems.Should().Contain(item => item.Id == "AB_2" && item.ProductId == "P1" && item.Update == "1");
        package.IngameItems.Should().Contain(item => item.Id == "FP_1" && item.ProductId == "P2");
        package.IngameItems.Should().NotContain(item => item.Id == "AB_1");
    }

    [TestMethod]
    public void UpdateProductAggregate_DoesNotMutateProductWhenChildValidationFails()
    {
        var package = CreatePackage();
        package.Products.Add(new Product { Id = "P1", Status = "N" });
        package.IngameItems.Add(new IngameItem { Id = "FP_1", ItemType = "FP", ItemLevel = "1", ProductId = "P2" });
        var draft = new ProductEditService().CreateDraft(package.Products[0]);
        draft.Status = "Y";

        var action = () => new ProductEditService().UpdateProduct(package, draft,
            [new IngameItem { Id = "draft", ItemType = "FP", ItemLevel = "1" }]);

        action.Should().Throw<InvalidOperationException>();
        package.Products[0].Status.Should().Be("N");
        package.IngameItems.Should().ContainSingle(item => item.ProductId == "P2");
    }

    private static PatchPackage CreatePackage()
        => new() { ProjectInfo = new ProjectInfo("project", null, "1.003.005", null) };
}
