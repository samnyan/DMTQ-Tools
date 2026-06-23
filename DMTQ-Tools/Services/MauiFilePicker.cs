using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Services;

public sealed class MauiFilePicker : IProjectFilePicker
{
    public async Task<string?> PickFileAsync(CancellationToken ct = default)
    {
        var result = await Microsoft.Maui.Storage.FilePicker.Default.PickAsync(new()
        {
            PickerTitle = "Select a file"
        });
        return result?.FullPath;
    }
}
