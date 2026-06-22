namespace DMTQ.Tools.Core.Models;

public sealed record ResourceFile(
    string PackageRelativePath,
    string ProjectRelativePath,
    string Category,
    bool Compressed,
    string? SourcePackagePath);
