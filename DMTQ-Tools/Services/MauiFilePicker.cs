using DMTQ.Tools.Core.Services;
using Assets.Lib.Models;

namespace DMTQ_Tools.Services;

public sealed class MauiFilePicker : IProjectFilePicker
{
    public async Task<string?> PickFileAsync(IReadOnlyCollection<string>? allowedExtensions = null, CancellationToken ct = default)
    {
        var options = new PickOptions
        {
            PickerTitle = "Select a file"
        };
        if (allowedExtensions is { Count: > 0 })
        {
            var extensions = allowedExtensions
                .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            options.FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = extensions,
                [DevicePlatform.Android] = ["application/octet-stream"],
                [DevicePlatform.iOS] = extensions,
                [DevicePlatform.MacCatalyst] = extensions
            });
        }
        ct.ThrowIfCancellationRequested();
        var result = await Microsoft.Maui.Storage.FilePicker.Default.PickAsync(options);
        return result?.FullPath;
    }
}

/// <summary>Extracts packaged atlas dummy bundles to app data for AssetsTools.NET to rewrite.</summary>
public sealed class MauiAtlasDummyTemplateProvider : IAtlasDummyTemplateProvider
{
    public async Task<string> GetTemplatePathAsync(string atlasKind, string platform, CancellationToken cancellationToken = default)
    {
        var kindName = atlasKind.ToLowerInvariant();
        if (kindName is not ("ngui2" or "ngui3"))
            throw new ArgumentOutOfRangeException(nameof(atlasKind), atlasKind, "Unknown atlas kind.");
        var platformName = platform.ToLowerInvariant();
        if (platformName is not ("ios" or "android" or "windows"))
            throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown atlas template platform.");
        var atlasPrefix = kindName == "ngui3" ? "d3" : "d";
        var fileName = $"{atlasPrefix}_{platformName}_dummy.unity3d";
        var outputDirectory = Path.Combine(FileSystem.AppDataDirectory, "atlas-templates");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        await using var source = await FileSystem.OpenAppPackageFileAsync($"AtlasTemplates/{fileName}");
        await using var destination = File.Create(outputPath);
        await source.CopyToAsync(destination, cancellationToken);
        return outputPath;
    }
}
