using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Lib.Models;

/// <summary>Identifies the atlas component schema used by a DMTQ bundle.</summary>
public enum SpriteAtlasKind
{
    /// <summary>NGUI2 atlas with Rect-based sprite records.</summary>
    NGUI2,

    /// <summary>NGUI3 atlas with integer UISpriteData records.</summary>
    NGUI3
}

/// <summary>Identifies the target platform of a Unity AssetBundle template.</summary>
public enum UnityTargetPlatform
{
    /// <summary>Windows Standalone bundle.</summary>
    Windows,

    /// <summary>Android bundle.</summary>
    Android,

    /// <summary>iOS bundle.</summary>
    IOS
}

/// <summary>Describes one sprite rectangle and the metadata serialized by the game's NGUI atlas.</summary>
public sealed class SpriteAtlasEntry
{
    /// <summary>Gets or sets the name looked up by the game, such as <c>e10_oblivion</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the left coordinate in Unity atlas pixels.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the bottom coordinate in Unity atlas pixels.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the sprite width in pixels.</summary>
    public float Width { get; set; }

    /// <summary>Gets or sets the sprite height in pixels.</summary>
    public float Height { get; set; }

    /// <summary>Gets or sets the NGUI2 inner rectangle's left coordinate.</summary>
    public float InnerX { get; set; }

    /// <summary>Gets or sets the NGUI2 inner rectangle's bottom coordinate.</summary>
    public float InnerY { get; set; }

    /// <summary>Gets or sets the NGUI2 inner rectangle's width.</summary>
    public float InnerWidth { get; set; }

    /// <summary>Gets or sets the NGUI2 inner rectangle's height.</summary>
    public float InnerHeight { get; set; }

    /// <summary>Gets or sets the left border in pixels.</summary>
    public int BorderLeft { get; set; }

    /// <summary>Gets or sets the right border in pixels.</summary>
    public int BorderRight { get; set; }

    /// <summary>Gets or sets the top border in pixels.</summary>
    public int BorderTop { get; set; }

    /// <summary>Gets or sets the bottom border in pixels.</summary>
    public int BorderBottom { get; set; }

    /// <summary>Gets or sets the left trimmed-pixel padding.</summary>
    public float PaddingLeft { get; set; }

    /// <summary>Gets or sets the right trimmed-pixel padding.</summary>
    public float PaddingRight { get; set; }

    /// <summary>Gets or sets the top trimmed-pixel padding.</summary>
    public float PaddingTop { get; set; }

    /// <summary>Gets or sets the bottom trimmed-pixel padding.</summary>
    public float PaddingBottom { get; set; }

    /// <summary>Creates a sprite entry whose inner rectangle matches its outer rectangle.</summary>
    public static SpriteAtlasEntry Create(string name, float x, float y, float width, float height)
        => new()
        {
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            InnerX = x,
            InnerY = y,
            InnerWidth = width,
            InnerHeight = height
        };
}

/// <summary>Serializable, platform-neutral state for one NGUI sprite atlas prefab.</summary>
public sealed class SpriteAtlasDocument
{
    /// <summary>Gets or sets the bundle and prefab name, for example <c>d_e10</c>.</summary>
    public string AtlasName { get; set; } = string.Empty;

    /// <summary>Gets or sets the NGUI atlas schema.</summary>
    public SpriteAtlasKind Kind { get; set; }

    /// <summary>Gets or sets the atlas texture width.</summary>
    public int TextureWidth { get; set; }

    /// <summary>Gets or sets the atlas texture height.</summary>
    public int TextureHeight { get; set; }

    /// <summary>Gets the sprite records in atlas order.</summary>
    public List<SpriteAtlasEntry> Sprites { get; set; } = new();

    /// <summary>Validates names, dimensions, and sprite bounds before serialization.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AtlasName))
            throw new InvalidOperationException("Atlas name is required.");
        if (AtlasName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new InvalidOperationException("Atlas name must be a file and bundle name, without directory separators.");
        if (!Enum.IsDefined(typeof(SpriteAtlasKind), Kind))
            throw new InvalidOperationException($"Unknown sprite atlas kind '{Kind}'.");
        if (TextureWidth <= 0 || TextureHeight <= 0)
            throw new InvalidOperationException("Atlas texture dimensions must be positive.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sprite in Sprites)
        {
            if (sprite is null || string.IsNullOrWhiteSpace(sprite.Name))
                throw new InvalidOperationException("Every sprite must have a name.");
            if (!names.Add(sprite.Name))
                throw new InvalidOperationException($"Duplicate sprite name '{sprite.Name}'.");
            if (sprite.X < 0 || sprite.Y < 0 || sprite.Width <= 0 || sprite.Height <= 0
                || sprite.X + sprite.Width > TextureWidth
                || sprite.Y + sprite.Height > TextureHeight)
                throw new InvalidOperationException($"Sprite '{sprite.Name}' is outside the atlas bounds.");
            if (Kind == SpriteAtlasKind.NGUI3
                && (!IsWholePixel(sprite.X) || !IsWholePixel(sprite.Y)
                    || !IsWholePixel(sprite.Width) || !IsWholePixel(sprite.Height)
                    || !IsWholePixel(sprite.PaddingLeft) || !IsWholePixel(sprite.PaddingRight)
                    || !IsWholePixel(sprite.PaddingTop) || !IsWholePixel(sprite.PaddingBottom)))
                throw new InvalidOperationException($"NGUI3 sprite '{sprite.Name}' must use whole-pixel coordinates and padding.");
        }
    }

    /// <summary>Creates a deep copy suitable for editing independently.</summary>
    public SpriteAtlasDocument Clone()
    {
        var copy = new SpriteAtlasDocument
        {
            AtlasName = AtlasName,
            Kind = Kind,
            TextureWidth = TextureWidth,
            TextureHeight = TextureHeight
        };
        copy.Sprites.AddRange(Sprites.Select(sprite => new SpriteAtlasEntry
        {
            Name = sprite.Name,
            X = sprite.X,
            Y = sprite.Y,
            Width = sprite.Width,
            Height = sprite.Height,
            InnerX = sprite.InnerX,
            InnerY = sprite.InnerY,
            InnerWidth = sprite.InnerWidth,
            InnerHeight = sprite.InnerHeight,
            BorderLeft = sprite.BorderLeft,
            BorderRight = sprite.BorderRight,
            BorderTop = sprite.BorderTop,
            BorderBottom = sprite.BorderBottom,
            PaddingLeft = sprite.PaddingLeft,
            PaddingRight = sprite.PaddingRight,
            PaddingTop = sprite.PaddingTop,
            PaddingBottom = sprite.PaddingBottom
        }));
        return copy;
    }

    private static bool IsWholePixel(float value) => Math.Abs(value - Math.Round(value)) < 0.0001f;
}

/// <summary>Connects a song key to the sprite URL and atlas package used by the game.</summary>
public sealed class SongJacketReference
{
    /// <summary>Gets or sets the stable song key used by the DMTQ project.</summary>
    public string SongKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the exact sprite name stored in the game's song or item table.</summary>
    public string SpriteName { get; set; } = string.Empty;

    /// <summary>Gets or sets the NGUI2 bundle name derived from the sprite name.</summary>
    public string NGUI2AtlasName { get; set; } = string.Empty;

    /// <summary>Gets or sets the NGUI3 bundle name derived from the sprite name.</summary>
    public string NGUI3AtlasName { get; set; } = string.Empty;

    /// <summary>Builds the game's default NGUI2 and NGUI3 atlas names from a sprite URL.</summary>
    /// <param name="songKey">Stable key for the song in the saved DMTQ project.</param>
    /// <param name="spriteName">Sprite URL such as <c>e10_oblivion</c>.</param>
    /// <returns>A reference using the game's <c>d_</c>/<c>d3_</c> atlas lookup convention.</returns>
    public static SongJacketReference FromSpriteName(string songKey, string spriteName)
    {
        if (string.IsNullOrWhiteSpace(songKey)) throw new ArgumentException("Song key is required.", nameof(songKey));
        if (string.IsNullOrWhiteSpace(spriteName)) throw new ArgumentException("Sprite name is required.", nameof(spriteName));
        var separator = spriteName.IndexOf('_');
        if (separator <= 0 || separator == spriteName.Length - 1)
            throw new ArgumentException("Sprite names must use the '<atlas-key>_<sprite-key>' form.", nameof(spriteName));

        var atlasKey = spriteName.Substring(0, separator);
        return new SongJacketReference
        {
            SongKey = songKey,
            SpriteName = spriteName,
            NGUI2AtlasName = "d_" + atlasKey,
            NGUI3AtlasName = "d3_" + atlasKey
        };
    }
}

/// <summary>Identifies the platform-specific dummy bundle used as the output template.</summary>
public sealed class UnityBundleTemplate
{
    /// <summary>Gets or sets the Unity build target represented by the template.</summary>
    public UnityTargetPlatform Platform { get; set; }

    /// <summary>Gets or sets the path to the raw UnityFS dummy bundle.</summary>
    public string BundlePath { get; set; } = string.Empty;
}
