using AssetsTools.NET.Texture;

namespace Assets.Lib.Models;

/// <summary>Contains an atlas model together with its decoded PNG texture.</summary>
public sealed class SpriteAtlasBundleContent
{
    /// <summary>Gets or sets the prefab and sprite metadata.</summary>
    public SpriteAtlasDocument Document { get; set; } = new();

    /// <summary>Gets or sets the decoded atlas texture as PNG bytes.</summary>
    public byte[] AtlasPng { get; set; } = System.Array.Empty<byte>();

    /// <summary>Gets or sets the original serialized Unity texture format.</summary>
    public TextureFormat OriginalTextureFormat { get; set; }
}
