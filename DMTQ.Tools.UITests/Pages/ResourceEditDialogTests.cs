using DMTQ.Tools.Components.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
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

            var root = Render(builder =>
            {
                builder.OpenComponent<FluentProviders>(0);
                builder.CloseComponent();
                builder.OpenComponent<ResourceDialogLauncher>(1);
                builder.AddAttribute(2, nameof(ResourceDialogLauncher.Content), content);
                builder.CloseComponent();
            });

            var launch = root.FindComponent<ResourceDialogLauncher>();
            Task? openTask = null;
            root.InvokeAsync(() =>
            {
                openTask = launch.Instance.OpenAsync();
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
            root.WaitForAssertion(() => root.FindComponents<ResourceEditDialog>().Should().ContainSingle());
            var dialog = root.FindComponent<ResourceEditDialog>();
            dialog.Find("code").TextContent.Should().Be("preview/song.opus");

            root.InvokeAsync(() => dialog.FindComponents<FluentButton>()
                .Single(button => button.Markup.Contains("Select File", StringComparison.Ordinal))
                .Instance.OnClick.InvokeAsync(new())).GetAwaiter().GetResult();

            content.Platforms.Should().ContainSingle(card =>
                card.Platform == "share"
                && card.IsNew
                && card.PendingFilePath == sourcePath);

            root.InvokeAsync(() => dialog.Instance.DialogInstance.CancelAsync()).GetAwaiter().GetResult();
            openTask!.GetAwaiter().GetResult();
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private sealed class ResourceDialogLauncher : ComponentBase
    {
        [Inject] private IDialogService DialogService { get; set; } = default!;
        [Parameter] public ResourceDialogData Content { get; set; } = default!;

        public Task OpenAsync()
            => DialogService.ShowDialogAsync<ResourceEditDialog>(options =>
            {
                options.Header.Title = "Resource";
                options.Parameters = new Dictionary<string, object?>
                {
                    [nameof(ResourceEditDialog.Content)] = Content
                };
            });

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, "Resource dialog test host");
    }
}
