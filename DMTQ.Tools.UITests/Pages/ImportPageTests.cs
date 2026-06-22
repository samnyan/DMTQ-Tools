namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class ImportPageTests : BlazorUITestBase
{
    [TestMethod]
    public void RendersPlatformSelectorAndImportButton()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<Import>();

        cut.Markup.Should().Contain("android");
        cut.Markup.Should().Contain("ios");
        cut.Markup.Should().Contain("Import Platform");
    }

    [TestMethod]
    public void ShowsCreateOrOpenHint()
    {
        var state = CreateStateWithEmptyPackage();
        RegisterAllServices(state);

        var cut = Render<Import>();

        cut.Markup.Should().Contain("Create or open a project");
    }
}
