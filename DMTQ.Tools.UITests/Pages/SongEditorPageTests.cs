using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using DMTQ_Tools.Components.Dialogs;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class SongEditorPageTests : BlazorUITestBase
{
    [TestMethod]
    public void RendersCreateFormForNewSong()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "new"));

        cut.Markup.Should().Contain("song-editor-form");
        cut.Markup.Should().Contain("<fluent-text-input");
        cut.Markup.Should().Contain("fluent-button type=\"submit\"");
    }

    [TestMethod]
    public void CreateStandardSongProducts_UsesItemIdAndAssignsStoreCategories()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreateSamplePackage();
        var tables = package.GetPlatformTables(state.SelectedExportPlatform);
        tables.Items.Add(new Item { Id = "2000" });
        tables.Products.Add(new Product { Id = "9000000", PlatformProductId = "40" });
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "new"));
        GetPrivateField<Song>(cut.Instance, "currentSong").Name = "New Song";
        cut.InvokeAsync(() =>
        {
            InvokePrivateMethod(cut.Instance, "SetSongIdInput", "2000");
            InvokePrivateMethod(cut.Instance, "CreateSongItem");
            InvokePrivateMethod(cut.Instance, "CreateStandardSongProducts");
        }).GetAwaiter().GetResult();

        var item = GetPrivateField<Item>(cut.Instance, "songItemDraft");
        item.Id.Should().Be("2001", "the song ID already belongs to another item");
        var products = GetPrivateField<List<Product>>(cut.Instance, "songProductDrafts");
        products.Select(product => product.Id).Should().Equal("2001", "1002001");
        products.Select(product => product.StoreProductId).Should().Equal(
            "com.neowizInternet.game.dmtq.newsong",
            "com.neowizInternet.game.dmtq_a.newsong");
        products.Select(product => product.PlatformProductId).Should().Equal("41", "42");
        products[0].CategoryIds.Should().Equal("3");
        products[1].CategoryIds.Should().Equal("103");
    }

    [TestMethod]
    public void ReopeningSongAfterProductListDeletion_DropsOnlyDeletedProductDraft()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreateSamplePackage();
        package.Songs[0].ItemId = 2000;
        var tables = package.GetPlatformTables(state.SelectedExportPlatform);
        tables.Items.Add(new Item { Id = "2000", ItemType = "S" });
        tables.Products.Add(new Product { Id = "2000", ItemId = "2000" });
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var firstVisit = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));
        GetPrivateField<List<Product>>(firstVisit.Instance, "songProductDrafts")
            .Select(product => product.Id).Should().Equal("2000");
        firstVisit.InvokeAsync(() => InvokePrivateMethod(firstVisit.Instance, "AddSongProduct"))
            .GetAwaiter().GetResult();
        new ProductEditService().RemoveProduct(package, "2000", state.SelectedExportPlatform);

        var reopened = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        GetPrivateField<List<Product>>(reopened.Instance, "songProductDrafts")
            .Select(product => product.Id).Should().ContainSingle()
            .Which.Should().Be("2001", "a newly added unsaved product draft is retained");
        package.GetPlatformTables(state.SelectedExportPlatform).Products.Should().BeEmpty();
    }

    [TestMethod]
    public void ReopeningSongAfterItemListDeletion_DoesNotRestoreItemOrItsProductCards()
    {
        var state = CreateStateWithEmptyPackage();
        var package = CreateSamplePackage();
        package.Songs[0].ItemId = 2000;
        var tables = package.GetPlatformTables(state.SelectedExportPlatform);
        tables.Items.Add(new Item { Id = "2000", ItemType = "S" });
        tables.Products.Add(new Product { Id = "2000", ItemId = "2000" });
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var firstVisit = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));
        GetPrivateField<Item>(firstVisit.Instance, "songItemDraft").Id.Should().Be("2000");
        new ItemEditService().RemoveItem(package, "2000", state.SelectedExportPlatform);

        var reopened = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        GetPrivateFieldValue(reopened.Instance, "songItemDraft").Should().BeNull();
        GetPrivateField<List<Product>>(reopened.Instance, "songProductDrafts").Should().BeEmpty();
        tables.Products.Should().ContainSingle("deleting an item does not delete its product table row");
    }

    [TestMethod]
    public void RendersEditFormForExistingSong()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        cut.Markup.Should().Contain("1001");
        cut.Markup.Should().Contain("value=\"T\"");
        cut.Markup.Should().Contain("value=\"G\"");
        cut.Markup.Should().Contain("fluent-button type=\"submit\"");
        cut.Markup.Should().Contain("fluent-data-grid");
    }

    [TestMethod]
    public void ShowsNoPackageWarningWhenPackageMissing()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        cut.Find("fluent-message-bar").TextContent.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void PatternDialog_UsesGamePointTypesAndPreservesUnknownCodes()
    {
        var state = CreateStateWithEmptyPackage();
        RegisterAllServices(state);

        var content = new SongPattern
        {
            PatternId = 1,
            PointType = 7
        };
        var root = Render(builder =>
        {
            builder.OpenComponent<FluentProviders>(0);
            builder.CloseComponent();
            builder.OpenComponent<PatternDialogLauncher>(1);
            builder.AddAttribute(2, nameof(PatternDialogLauncher.Content), content);
            builder.CloseComponent();
        });
        var launcher = root.FindComponent<PatternDialogLauncher>();
        Task? openTask = null;
        root.InvokeAsync(() =>
        {
            openTask = launcher.Instance.OpenAsync();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        root.WaitForAssertion(() => root.FindComponents<PatternDialog>().Should().ContainSingle());
        var cut = root.FindComponent<PatternDialog>();

        cut.Markup.Should().Contain("0 — Q point");
        cut.Markup.Should().Contain("1 — MAX point");
        cut.Markup.Should().Contain("7 — Existing value");
        cut.Markup.Should().NotContain("2 — Q point");

        root.InvokeAsync(() => cut.Instance.DialogInstance.CancelAsync()).GetAwaiter().GetResult();
        openTask!.GetAwaiter().GetResult();
    }

    [TestMethod]
    public void SyncOriginalText_CopiesAndLiveUpdatesMatchingLocalizedFields()
    {
        var state = CreateStateWithEmptyPackage();
        state.SetPackage(CreateSamplePackage());
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);
        var cut = Render<SongEditor>(parameters => parameters.Add(p => p.SongId, "1001"));

        var syncSwitch = cut.FindComponents<FluentSwitch>().Last();
        cut.InvokeAsync(async () =>
        {
            await syncSwitch.Instance.ValueChanged.InvokeAsync(true);
            await cut.FindComponents<FluentTextInput>()[2].Instance.ValueChanged.InvokeAsync("Live full name");
            await cut.FindComponents<FluentTextInput>()[4].Instance.ValueChanged.InvokeAsync("Live artist");
        }).GetAwaiter().GetResult();

        var draft = GetPrivateField<Song>(cut.Instance, "currentSong");
        draft.Localizations.Should().ContainKeys("CN", "JP", "KR", "TW", "US");
        draft.Localizations.Values.Should().OnlyContain(localization =>
            localization.FullName == "Live full name"
            && localization.ArtistName == "Live artist"
            && localization.Genre == "G");

        cut.InvokeAsync(async () =>
        {
            await syncSwitch.Instance.ValueChanged.InvokeAsync(false);
            await cut.FindComponents<FluentTextInput>()[2].Instance.ValueChanged.InvokeAsync("Original only");
        }).GetAwaiter().GetResult();
        draft.Localizations.Values.Should().OnlyContain(localization =>
            localization.FullName == "Live full name");
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
        => (T)(GetPrivateFieldValue(instance, fieldName)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was null."));

    private static object? GetPrivateFieldValue(object instance, string fieldName)
        => instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance);

    private static void InvokePrivateMethod(object instance, string methodName, params object?[] arguments)
        => instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(instance, arguments);

    private sealed class PatternDialogLauncher : ComponentBase
    {
        [Inject] private IDialogService DialogService { get; set; } = default!;
        [Parameter] public SongPattern Content { get; set; } = default!;

        public Task OpenAsync()
            => DialogService.ShowDialogAsync<PatternDialog>(options =>
            {
                options.Header.Title = "Pattern";
                options.Parameters = new Dictionary<string, object?>
                {
                    [nameof(PatternDialog.Content)] = Content
                };
            });

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, "Pattern dialog test host");
    }

    private static PatchPackage CreateSamplePackage()
    {
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test", null, "1.0", null) };
        var st = new GameTable { PackageRelativePath = "table/us/song_song.csv", TableName = "song_song", LanguageCode = "us" };
        st.Columns.Add(new GameTableColumn("song_id", 0)); st.Columns.Add(new GameTableColumn("name", 1)); st.Columns.Add(new GameTableColumn("genre", 2)); st.Columns.Add(new GameTableColumn("artist_name", 3));
        var sr = new GameTableRow { Order = 0 }; sr.Cells.Add(new GameTableCell("song_id", "1001")); sr.Cells.Add(new GameTableCell("name", "T")); sr.Cells.Add(new GameTableCell("genre", "G")); sr.Cells.Add(new GameTableCell("artist_name", "A"));
        st.Rows.Add(sr); package.Tables.Tables.Add(st);

        var pt = new GameTable { PackageRelativePath = "table/us/song_songPattern.csv", TableName = "song_songPattern", LanguageCode = "us" };
        pt.Columns.Add(new GameTableColumn("pattern_id", 0)); pt.Columns.Add(new GameTableColumn("song_id", 1)); pt.Columns.Add(new GameTableColumn("line", 2)); pt.Columns.Add(new GameTableColumn("difficulty", 3));
        var pr = new GameTableRow { Order = 0 }; pr.Cells.Add(new GameTableCell("pattern_id", "9001")); pr.Cells.Add(new GameTableCell("song_id", "1001")); pr.Cells.Add(new GameTableCell("line", "2")); pr.Cells.Add(new GameTableCell("difficulty", "1"));
        pt.Rows.Add(pr); package.Tables.Tables.Add(pt);

        // Also add entity Song so BuildCatalog can find it
        var song = new Song { Id = 1001, Name = "T", Genre = "G", ArtistName = "A" };
        song.Patterns.Add(new SongPattern { PatternId = 9001, SongId = 1001, Line = 2, Difficulty = 1 });
        package.Songs.Add(song);

        return package;
    }
}
