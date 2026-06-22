using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

/// <summary>
/// MAUI folder picker using CommunityToolkit.Maui.Storage.FolderPicker.
/// </summary>
public sealed class MauiFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(CancellationToken ct = default)
    {
        var result = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync(ct);
        if (!result.IsSuccessful || result.Folder is null)
            return null;

        return result.Folder.Path;
    }
}
