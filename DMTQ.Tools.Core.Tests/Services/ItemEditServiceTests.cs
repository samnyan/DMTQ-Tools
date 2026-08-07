using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class ItemEditServiceTests
{
    [TestMethod]
    public void CreateDraft_CopiesLocalizedFieldsWithoutSharingDictionaries()
    {
        var source = new Item { Id = "ITEM_001", ItemName = "Base" };
        source.NamesByLanguage["CN"] = "名称";
        source.DescriptionsByLanguage["JP"] = "説明";
        source.SummariesByLanguage["US"] = "Summary";

        var draft = new ItemEditService().CreateDraft(source);

        draft.Should().NotBeSameAs(source);
        draft.NamesByLanguage.Should().NotBeSameAs(source.NamesByLanguage);
        draft.DescriptionsByLanguage["JP"].Should().Be("説明");
        draft.SummariesByLanguage["US"].Should().Be("Summary");

        draft.NamesByLanguage["CN"] = "新名称";
        source.NamesByLanguage["CN"].Should().Be("名称");
    }

    [TestMethod]
    public void UpdateItem_AppliesDetachedDraftAndLocalizedValues()
    {
        var package = CreatePackage();
        package.Items.Add(new Item { Id = "ITEM_001", ItemName = "Before" });
        var draft = new ItemEditService().CreateDraft(package.Items[0]);
        draft.ItemName = "After";
        draft.NamesByLanguage["CN"] = "之后";

        new ItemEditService().UpdateItem(package, draft);

        package.Items[0].ItemName.Should().Be("After");
        package.Items[0].NamesByLanguage["CN"].Should().Be("之后");
        package.Items[0].Should().NotBeSameAs(draft);
    }

    [TestMethod]
    public void AddAndRemoveItem_ManagePackageCollection()
    {
        var package = CreatePackage();
        var service = new ItemEditService();

        service.AddItem(package, new Item { Id = "ITEM_001" });
        service.RemoveItem(package, "item_001");

        package.Items.Should().BeEmpty();
    }

    [TestMethod]
    public void AddItem_RejectsDuplicateIdIgnoringCase()
    {
        var package = CreatePackage();
        package.Items.Add(new Item { Id = "ITEM_001" });

        var action = () => new ItemEditService().AddItem(package,
            new Item { Id = "item_001" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Item 'item_001' already exists.");
    }

    private static PatchPackage CreatePackage()
        => new() { ProjectInfo = new ProjectInfo("project", null, "1.003.005", null) };
}
