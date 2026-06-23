namespace DMTQ.Tools.Core.Models.Export;

public sealed class PlatformExportOptions
{
    public required string Platform { get; init; }
    public PlatformExportMode Mode { get; init; } = PlatformExportMode.Full;
    public PackageExportOptions PackageOptions { get; init; } = new();
}
