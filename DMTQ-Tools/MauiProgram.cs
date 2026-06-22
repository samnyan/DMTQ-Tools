using CommunityToolkit.Maui;
using DMTQ.Tools.Core.Services;
using DMTQ_Tools.Services;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Blazor;

namespace DMTQ_Tools
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddFluentUIComponents();

            builder.Services.AddSingleton<Lz4CompressionService>();
            builder.Services.AddSingleton<PatchManifestReader>();
            builder.Services.AddSingleton<CsvTableReader>();
            builder.Services.AddSingleton<CsvTableWriter>();
            builder.Services.AddSingleton<PatchManifestWriter>();
            builder.Services.AddSingleton<PatchChecksumService>();
            builder.Services.AddSingleton<PatchPackageImporter>();
            builder.Services.AddSingleton<PatchPackageExporter>();
            builder.Services.AddSingleton<PatchPackageValidator>();
            builder.Services.AddSingleton<IPatchProjectRepository, JsonPatchProjectRepository>();
            builder.Services.AddSingleton<LogicalTableService>();
            builder.Services.AddSingleton<SongCatalogService>();
            builder.Services.AddSingleton<SongEditService>();
            builder.Services.AddSingleton<ResourceManagerService>();
            builder.Services.AddSingleton<PackageQaService>();
            builder.Services.AddSingleton<PlatformPackageImporter>();
            builder.Services.AddSingleton<PlatformPackageExporter>();

            builder.Services.AddSingleton<IFolderPicker, MauiFolderPicker>();

            var state = new GameTableManagerState();
            builder.Services.AddSingleton<IProjectState>(state);
            builder.Services.AddSingleton(state);
            builder.Services.AddSingleton<IProjectWorkflow, GameTableManagerWorkflow>();
            builder.Services.AddSingleton<GameTableManagerWorkflow>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();

            // DevFlow: MAUI automation toolkit (visual tree, element interaction, screenshots)
            builder.AddMauiDevFlowAgent();

            // DevFlow Blazor: Chrome DevTools Protocol bridge for BlazorWebView
            builder.AddMauiBlazorDevFlowTools();
#endif

            return builder.Build();
        }
    }
}
