namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Abstraction for picking a file. Implemented in MAUI host,
/// faked in bUnit UI tests.
/// </summary>
public interface IProjectFilePicker
{
    Task<string?> PickFileAsync(CancellationToken ct = default);
}
