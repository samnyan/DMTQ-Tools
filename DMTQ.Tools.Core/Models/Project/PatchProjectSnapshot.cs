using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;

namespace DMTQ.Tools.Core.Models.Project;

public sealed record PatchProjectSnapshot(
    PatchPackage Package,
    string ExportCompressionMode,
    PackageExportOptions ExportOptions);
