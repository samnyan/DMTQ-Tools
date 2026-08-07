using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Associates song entities with their shared preview audio resources.</summary>
public static class SongPreviewLinker
{
    /// <summary>
    /// Links each song to the preview resource at <c>preview/&lt;Name&gt;.p.opus</c>.
    /// Matching is case-insensitive and uses the song's technical <c>Name</c>, not its display name or ID.
    /// </summary>
    /// <param name="package">The package containing songs and resources to link.</param>
    public static void LinkPreviewResources(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var previewPaths = package.Resources
            .Where(resource => resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
            .Select(resource => resource.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var song in package.Songs)
        {
            if (string.IsNullOrWhiteSpace(song.Name))
            {
                song.PreviewPackageRelativePath = null;
                continue;
            }

            var expectedPath = $"preview/{song.Name.Trim()}.p.opus";
            song.PreviewPackageRelativePath = previewPaths.Contains(expectedPath)
                ? expectedPath
                : null;
        }
    }
}
