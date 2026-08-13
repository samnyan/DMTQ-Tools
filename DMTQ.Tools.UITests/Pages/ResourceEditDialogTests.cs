using DMTQ.Tools.Components.Models;
using FluentAssertions;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class ResourceEditDialogTests : BlazorUITestBase
{
    [TestMethod]
    public void NewPreviewResource_ShowsFixedPrefixAndStagesSharedFile()
    {
        var sourcePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(sourcePath, "preview-bytes");
            var state = CreateStateWithEmptyPackage();
            RegisterAllServices(state);
            FilePicker.PickResult = sourcePath;
            var content = new ResourceDialogData
            {
                IsNew = true,
                Category = "preview",
                FileName = "song.opus"
            };

            var cut = Render<ResourceEditDialog>(parameters => parameters
                .Add(component => component.Content, content));
            cut.Find("code").TextContent.Should().Be("preview/song.opus");

            cut.FindAll("fluent-button")
                .Single(button => button.TextContent.Contains("Select File", StringComparison.Ordinal))
                .Click();

            content.Platforms.Should().ContainSingle(card =>
                card.Platform == "share"
                && card.IsNew
                && card.PendingFilePath == sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }
}
