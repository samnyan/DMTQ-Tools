using DMTQ.Tools.Core.Services;
using DMTQ_Tools.Services;
using Microsoft.Extensions.Logging;

namespace DMTQ_Tools
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

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
            builder.Services.AddSingleton<PlatformPackageImporter>();
            builder.Services.AddSingleton<PlatformPackageExporter>();
            builder.Services.AddSingleton<GameTableManagerState>();
            builder.Services.AddSingleton<GameTableManagerWorkflow>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
