namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Abstraction for picking a filesystem folder.
/// Implemented in MAUI host via CommunityToolkit.Maui.Storage.FolderPicker;
/// faked in bUnit UI tests.
/// </summary>
public interface IFolderPicker
{
    /// <summary>
    /// Opens a folder picker dialog and returns the selected path, or null if cancelled.
    /// </summary>
    Task<string?> PickFolderAsync(CancellationToken ct = default);
}
