using CommunityToolkit.Maui.Storage;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

/// <summary>
/// MAUI implementation of the platform save-file dialog.
/// </summary>
public sealed class MauiFileSaver : IProjectFileSaver
{
    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(
        string suggestedFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(content);

        var result = await FileSaver.Default.SaveAsync(
            suggestedFileName,
            content,
            cancellationToken);

        if (result.IsCancelled)
        {
            return null;
        }

        result.EnsureSuccess();
        return result.FilePath;
    }
}
