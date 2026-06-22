namespace DMTQ.Tools.Core.Models;

public sealed record PatchProjectSnapshot(
    PatchPackage Package,
    string ExportCompressionMode,
    PackageExportOptions ExportOptions);
