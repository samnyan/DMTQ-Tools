using System;
using System.Collections.Generic;
using System.IO;
using Assets.Lib.Models;
using StbImageSharp;
using ImageWriter = StbImageWriteSharp.ImageWriter;
using WriteColorComponents = StbImageWriteSharp.ColorComponents;

namespace Assets.Lib.Services;

/// <summary>Composes and extracts PNG atlas images using the coordinate system stored by Unity.</summary>
public static class SpriteAtlasImage
{
    /// <summary>Composes a transparent PNG atlas by drawing the supplied images into named sprite regions.</summary>
    /// <param name="document">Atlas dimensions and named sprite regions.</param>
    /// <param name="spritePngs">PNG image bytes keyed by exact sprite name.</param>
    /// <returns>PNG bytes for the composed atlas.</returns>
    public static byte[] Compose(SpriteAtlasDocument document, IReadOnlyDictionary<string, byte[]> spritePngs)
        => Compose(document, spritePngs, null);

    /// <summary>Composes sprite regions over an existing atlas image or a transparent new canvas.</summary>
    /// <param name="document">Atlas dimensions and named sprite regions.</param>
    /// <param name="spritePngs">PNG image bytes keyed by exact sprite name.</param>
    /// <param name="baseAtlasPng">Optional existing atlas PNG whose untouched regions should be retained.</param>
    /// <returns>PNG bytes for the composed atlas.</returns>
    public static byte[] Compose(SpriteAtlasDocument document, IReadOnlyDictionary<string, byte[]> spritePngs, byte[] baseAtlasPng)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        if (spritePngs is null) throw new ArgumentNullException(nameof(spritePngs));
        document.Validate();

        byte[] atlas;
        if (baseAtlasPng is null)
        {
            atlas = new byte[checked(document.TextureWidth * document.TextureHeight * 4)];
        }
        else
        {
            var baseImage = ImageResult.FromMemory(baseAtlasPng, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            if (baseImage.Width != document.TextureWidth || baseImage.Height != document.TextureHeight)
                throw new InvalidDataException($"Base PNG is {baseImage.Width}x{baseImage.Height}; expected {document.TextureWidth}x{document.TextureHeight}.");
            atlas = (byte[])baseImage.Data.Clone();
        }
        foreach (var sprite in document.Sprites)
        {
            if (!spritePngs.TryGetValue(sprite.Name, out var png))
                continue;
            if (png is null || png.Length == 0)
                throw new ArgumentException($"PNG data for '{sprite.Name}' is empty.", nameof(spritePngs));

            var width = ToPixel(sprite.Width, sprite.Name, nameof(sprite.Width));
            var height = ToPixel(sprite.Height, sprite.Name, nameof(sprite.Height));
            var x = ToPixel(sprite.X, sprite.Name, nameof(sprite.X));
            var yFromTop = document.TextureHeight - ToPixel(sprite.Y, sprite.Name, nameof(sprite.Y)) - height;
            var image = ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            CopyResampled(image.Data, image.Width, image.Height, atlas, document.TextureWidth, x, yFromTop, width, height);
        }

        return EncodePng(atlas, document.TextureWidth, document.TextureHeight);
    }

    /// <summary>Crops a sprite from an atlas PNG using its Unity bottom-left-origin rectangle.</summary>
    /// <param name="atlasPng">PNG bytes for the complete atlas.</param>
    /// <param name="atlasWidth">Expected atlas width in pixels.</param>
    /// <param name="atlasHeight">Expected atlas height in pixels.</param>
    /// <param name="sprite">Sprite region to crop.</param>
    /// <returns>PNG bytes for the sprite rectangle.</returns>
    public static byte[] Crop(byte[] atlasPng, int atlasWidth, int atlasHeight, SpriteAtlasEntry sprite)
    {
        if (atlasPng is null) throw new ArgumentNullException(nameof(atlasPng));
        if (sprite is null) throw new ArgumentNullException(nameof(sprite));
        if (atlasWidth <= 0 || atlasHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(atlasWidth), "Atlas dimensions must be positive.");

        var x = ToPixel(sprite.X, sprite.Name, nameof(sprite.X));
        var y = atlasHeight - ToPixel(sprite.Y, sprite.Name, nameof(sprite.Y)) - ToPixel(sprite.Height, sprite.Name, nameof(sprite.Height));
        var width = ToPixel(sprite.Width, sprite.Name, nameof(sprite.Width));
        var height = ToPixel(sprite.Height, sprite.Name, nameof(sprite.Height));
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > atlasWidth || y + height > atlasHeight)
            throw new ArgumentOutOfRangeException(nameof(sprite), "Sprite rectangle is outside the atlas bounds.");

        var source = ImageResult.FromMemory(atlasPng, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        if (source.Width != atlasWidth || source.Height != atlasHeight)
            throw new InvalidDataException($"PNG is {source.Width}x{source.Height}; expected {atlasWidth}x{atlasHeight}.");

        var pixels = new byte[checked(width * height * 4)];
        for (var row = 0; row < height; row++)
            Buffer.BlockCopy(source.Data, ((y + row) * atlasWidth + x) * 4, pixels, row * width * 4, width * 4);
        return EncodePng(pixels, width, height);
    }

    private static void CopyResampled(byte[] source, int sourceWidth, int sourceHeight,
        byte[] destination, int destinationWidth, int x, int y, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            var sourceY = Math.Min(sourceHeight - 1, row * sourceHeight / height);
            for (var column = 0; column < width; column++)
            {
                var sourceX = Math.Min(sourceWidth - 1, column * sourceWidth / width);
                Buffer.BlockCopy(source, (sourceY * sourceWidth + sourceX) * 4,
                    destination, ((y + row) * destinationWidth + x + column) * 4, 4);
            }
        }
    }

    private static byte[] EncodePng(byte[] pixels, int width, int height)
    {
        using var output = new MemoryStream();
        new ImageWriter().WritePng(pixels, width, height, WriteColorComponents.RedGreenBlueAlpha, output);
        return output.ToArray();
    }

    private static int ToPixel(float value, string spriteName, string fieldName)
    {
        var rounded = (int)Math.Round(value);
        if (Math.Abs(value - rounded) > 0.0001f)
            throw new ArgumentException($"Sprite '{spriteName}' has a fractional {fieldName} value that cannot address PNG pixels.");
        return rounded;
    }
}
