using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public interface IPatchProjectRepository
{
    Task SaveAsync(
        PatchPackage package,
        string exportCompressionMode,
        PackageExportOptions exportOptions,
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task<PatchProjectSnapshot> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}
