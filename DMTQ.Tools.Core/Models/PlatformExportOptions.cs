namespace DMTQ.Tools.Core.Models;

public sealed class PlatformExportOptions
{
    public required string Platform { get; init; }
    public PlatformExportMode Mode { get; init; } = PlatformExportMode.Delta;
    public PackageExportOptions PackageOptions { get; init; } = new();
}
