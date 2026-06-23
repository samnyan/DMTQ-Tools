using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using Bunit;

namespace DMTQ.Tools.UITests;

/// <summary>
/// Base class for Blazor UI tests that registers all services
/// needed by the component pages.
/// </summary>
public abstract class BlazorUITestBase : Bunit.TestContext
{
    protected void RegisterAllServices(GameTableManagerTestState state)
    {
        Services.AddSingleton<IProjectState>(state);
        Services.AddSingleton<IProjectWorkflow>(new FakeWorkflow(state));
        Services.AddSingleton<IFolderPicker>(new FakeFolderPicker());
        Services.AddSingleton<IProjectFilePicker>(new FakeFilePicker());
        Services.AddSingleton<LogicalTableService>();
        Services.AddSingleton<SongCatalogService>();
        Services.AddSingleton<SongEditService>();
        Services.AddFluentUIComponents();

        // FluentUI components invoke JS interop in OnAfterRenderAsync (v=... is build-specific).
        // Use Loose mode so unregistered JS calls return empty/default instead of throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Renders a page that uses FluentDataGrid (needs FluentMenuProvider ancestor).
    /// </summary>
    protected IRenderedComponent<TComponent> RenderWithProviders<TComponent>()
        where TComponent : IComponent
    {
        var fragment = Render(builder =>
        {
            builder.OpenComponent<FluentMenuProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TComponent>(1);
            builder.CloseComponent();
        });
        // Return the page portion (second component) as IRenderedComponent
        return (IRenderedComponent<TComponent>)fragment.FindComponents<TComponent>().Single();
    }

    protected GameTableManagerTestState CreateStateWithEmptyPackage()
    {
        return new GameTableManagerTestState();
    }

    protected sealed class GameTableManagerTestState : IProjectState
    {
        public string? ProjectRoot { get; set; }
        public PatchPackage? CurrentPackage { get; set; }
        public PatchManifest? LastExportManifest { get; set; }
        public PatchValidationResult? LastValidationResult { get; set; }
        public string ExportCompressionMode { get; set; } = "Keep";
        public PackageExportOptions? RestoredExportOptions { get; set; }
        public List<string> Diagnostics { get; } = [];
        public PlatformExportResult? LastPlatformExportResult { get; set; }
        public string SelectedExportPlatform { get; set; } = "android";
        public PlatformExportMode PlatformExportMode { get; set; } = PlatformExportMode.Full;
        public bool HasProject => !string.IsNullOrWhiteSpace(ProjectRoot);
        public bool HasPackage => CurrentPackage is not null;
        public bool IsDirty { get; set; }
        public event Action? StateChanged;
        public IReadOnlyList<string> ImportIntegrityErrors => [];

        public PackageExportOptions CreateExportOptions() => new();

        public void SetPackage(PatchPackage package)
        {
            CurrentPackage = package;
        }

        public void SetProjectRoot(string root)
        {
            ProjectRoot = root;
        }
    }

    private sealed class FakeWorkflow(GameTableManagerTestState state) : IProjectWorkflow
    {
        public Task CreateProjectAsync(string projectRoot) { state.SetProjectRoot(projectRoot); return Task.CompletedTask; }
        public Task ImportPlatformPackageAsync(string packageRoot, string platform, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportPlatformPackageAsync(string exportRoot, string platform, PlatformExportMode mode, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveProjectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task OpenProjectAsync(string projectRoot, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddOrReplaceResourceAsync(string s, string p, string? pl, IReadOnlyCollection<string> ip, bool c, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveResourceAsync(string p, string? pl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetResourceCompressionAsync(string p, string? pl, bool c, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetPreviewIncludedPlatformsAsync(string p, IReadOnlyCollection<string> ip, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeFolderPicker : IFolderPicker
    {
        public string? PickResult { get; set; }
        public Task<string?> PickFolderAsync(CancellationToken ct = default) => Task.FromResult(PickResult);
    }

    private sealed class FakeFilePicker : IProjectFilePicker
    {
        public string? PickResult { get; set; }
        public Task<string?> PickFileAsync(CancellationToken ct = default) => Task.FromResult(PickResult);
    }
}
