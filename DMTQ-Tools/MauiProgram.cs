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

            // IMPORTANT: Must use default ServiceLifetime.Scoped for FluentUI in MAUI BlazorWebView.
            // Singleton causes MessageService to resolve before NavigationManager is initialized
            // → "WebViewNavigationManager has not been initialized" crash.
            // DO NOT change to ServiceLifetime.Singleton.
            builder.Services.AddFluentUIComponents();

            builder.Services.AddSingleton<CsvTableReader>();
            builder.Services.AddSingleton<IPatchProjectRepository, JsonPatchProjectRepository>();
            builder.Services.AddSingleton<LogicalTableService>();
            builder.Services.AddSingleton<SongCatalogService>();
            builder.Services.AddSingleton<SongEditService>();

            builder.Services.AddSingleton<ResourceManagerService>();
            builder.Services.AddSingleton<PackageQaService>();
            builder.Services.AddSingleton<PlatformPackageImporter>();
            builder.Services.AddSingleton<PlatformPackageExporter>();

            builder.Services.AddSingleton<IFolderPicker, MauiFolderPicker>();
            builder.Services.AddSingleton<IProjectFilePicker, MauiFilePicker>();

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
