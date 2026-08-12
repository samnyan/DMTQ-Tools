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
        => LinkPreviewResources(package, package.Songs);

    /// <summary>Links a selected platform's songs to shared preview resources.</summary>
    /// <param name="package">The package containing preview resources.</param>
    /// <param name="songs">Songs belonging to the selected client platform.</param>
    public static void LinkPreviewResources(PatchPackage package, IEnumerable<Models.Entity.Song> songs)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(songs);

        var previewPaths = package.Resources
            .Where(resource => resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
            .Select(resource => resource.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var song in songs)
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
