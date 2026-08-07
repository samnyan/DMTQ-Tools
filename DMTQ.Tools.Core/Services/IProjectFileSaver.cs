namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Abstraction for saving a file through the host platform's file picker.
/// </summary>
public interface IProjectFileSaver
{
    /// <summary>
    /// Opens a save dialog and writes the supplied content to the selected file.
    /// </summary>
    /// <param name="suggestedFileName">The initial file name shown by the save dialog.</param>
    /// <param name="content">The content to save. The stream is read from its current position.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved file path, or <see langword="null"/> when the user cancels.</returns>
    Task<string?> SaveFileAsync(
        string suggestedFileName,
        Stream content,
        CancellationToken cancellationToken = default);
}
