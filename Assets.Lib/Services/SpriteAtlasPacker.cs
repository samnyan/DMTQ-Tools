using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Lib.Models;

namespace Assets.Lib.Services;

/// <summary>Packs rectangular sprite images into a fixed-size atlas using deterministic shelf placement.</summary>
public static class SpriteAtlasPacker
{
    /// <summary>Calculates non-overlapping sprite rectangles for a fixed atlas size.</summary>
    /// <param name="atlasName">Bundle and prefab name, such as <c>d_e10</c>.</param>
    /// <param name="kind">NGUI atlas serialization kind.</param>
    /// <param name="atlasWidth">Target atlas width in pixels.</param>
    /// <param name="atlasHeight">Target atlas height in pixels.</param>
    /// <param name="images">Sprite names and dimensions to pack.</param>
    /// <param name="spacing">Transparent spacing in pixels between adjacent sprites.</param>
    /// <returns>An atlas document with calculated sprite rectangles.</returns>
    public static SpriteAtlasDocument Pack(string atlasName, SpriteAtlasKind kind, int atlasWidth, int atlasHeight,
        IEnumerable<SpriteImageSize> images, int spacing = 2)
    {
        if (string.IsNullOrWhiteSpace(atlasName)) throw new ArgumentException("Atlas name is required.", nameof(atlasName));
        if (atlasWidth <= 0) throw new ArgumentOutOfRangeException(nameof(atlasWidth));
        if (atlasHeight <= 0) throw new ArgumentOutOfRangeException(nameof(atlasHeight));
        if (images is null) throw new ArgumentNullException(nameof(images));
        if (spacing < 0) throw new ArgumentOutOfRangeException(nameof(spacing));

        var requested = images.ToList();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in requested)
        {
            if (image is null || string.IsNullOrWhiteSpace(image.Name))
                throw new ArgumentException("Every image must have a name.", nameof(images));
            if (!names.Add(image.Name))
                throw new ArgumentException($"Duplicate sprite name '{image.Name}'.", nameof(images));
            if (image.Width <= 0 || image.Height <= 0 || image.Width > atlasWidth || image.Height > atlasHeight)
                throw new ArgumentException($"Sprite '{image.Name}' has invalid dimensions {image.Width}x{image.Height}.", nameof(images));
        }

        var document = new SpriteAtlasDocument
        {
            AtlasName = atlasName,
            Kind = kind,
            TextureWidth = atlasWidth,
            TextureHeight = atlasHeight
        };

        var x = 0;
        var top = 0;
        var rowHeight = 0;
        foreach (var image in requested.OrderByDescending(image => image.Height).ThenByDescending(image => image.Width).ThenBy(image => image.Name, StringComparer.Ordinal))
        {
            if (x > 0 && x + image.Width > atlasWidth)
            {
                top += rowHeight + spacing;
                x = 0;
                rowHeight = 0;
            }
            if (top + image.Height > atlasHeight)
                throw new InvalidOperationException($"Sprite '{image.Name}' does not fit in the {atlasWidth}x{atlasHeight} atlas.");

            var y = atlasHeight - top - image.Height;
            document.Sprites.Add(SpriteAtlasEntry.Create(image.Name, x, y, image.Width, image.Height));
            x += image.Width + spacing;
            rowHeight = Math.Max(rowHeight, image.Height);
        }

        document.Validate();
        return document;
    }
}
