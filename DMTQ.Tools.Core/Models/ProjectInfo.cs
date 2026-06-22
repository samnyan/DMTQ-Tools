namespace DMTQ.Tools.Core.Models;

public sealed record ProjectInfo(
    string ProjectRoot,
    string? SourcePackageRoot,
    string? Version,
    string? Platform);
