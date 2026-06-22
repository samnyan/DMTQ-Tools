using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Services;
using DMTQ_Tools.Services;

namespace DMTQ.Tools.UITests;

/// <summary>
/// Base class for Blazor UI tests that registers all services
/// needed by MAUI pages (mirrors MauiProgram.cs registration).
/// </summary>
public abstract class BlazorUITestBase : Bunit.TestContext
{
    protected void RegisterAllServices(GameTableManagerState state)
    {
        Services.AddSingleton(state);
        Services.AddSingleton<Lz4CompressionService>();
        Services.AddSingleton<PatchManifestReader>();
        Services.AddSingleton<CsvTableReader>();
        Services.AddSingleton<CsvTableWriter>();
        Services.AddSingleton<PatchManifestWriter>();
        Services.AddSingleton<PatchChecksumService>();
        Services.AddSingleton<PatchPackageImporter>();
        Services.AddSingleton<PatchPackageExporter>();
        Services.AddSingleton<PatchPackageValidator>();
        Services.AddSingleton<LogicalTableService>();
        Services.AddSingleton<SongCatalogService>();
        Services.AddSingleton<SongEditService>();
        Services.AddSingleton<ResourceManagerService>();
        Services.AddSingleton<PackageQaService>();
        Services.AddSingleton<PlatformPackageImporter>();
        Services.AddSingleton<PlatformPackageExporter>();
        Services.AddSingleton<IPatchProjectRepository>(new FakeRepository());
        Services.AddSingleton<GameTableManagerWorkflow>();
    }

    private sealed class FakeRepository : IPatchProjectRepository
    {
        public Task SaveAsync(PatchPackage package, string exportCompressionMode, PackageExportOptions exportOptions, string projectRoot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PatchProjectSnapshot> LoadAsync(string projectRoot, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
