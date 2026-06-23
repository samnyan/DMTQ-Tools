namespace DMTQ.Tools.Core.Models.Project;

public sealed record ProjectInfo(
    string ProjectRoot,
    string? SourcePackageRoot,
    string? Version,
    string? Platform);
