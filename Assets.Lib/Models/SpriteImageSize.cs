namespace Assets.Lib.Models;

/// <summary>Names an image and its pixel dimensions for atlas packing.</summary>
public sealed class SpriteImageSize
{
    /// <summary>Gets or sets the sprite name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the source image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the source image height in pixels.</summary>
    public int Height { get; set; }
}
