using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Lib.Models;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using StbImageSharp;

namespace Assets.Lib.Services;

/// <summary>Reads and rewrites DMTQ NGUI atlas bundles using platform-specific Unity dummy bundles.</summary>
public sealed class SpriteAtlasBundleService
{
    /// <summary>Reads the atlas prefab state and PNG texture from a UnityFS bundle.</summary>
    /// <param name="bundlePath">Path to a raw UnityFS bundle or platform dummy.</param>
    /// <returns>The atlas metadata and a PNG copy of its texture.</returns>
    public SpriteAtlasBundleContent Read(string bundlePath, SpriteAtlasKind? preferredKind = null)
    {
        if (string.IsNullOrWhiteSpace(bundlePath)) throw new ArgumentException("Bundle path is required.", nameof(bundlePath));
        if (!File.Exists(bundlePath)) throw new FileNotFoundException("Unity bundle was not found.", bundlePath);

        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var assets = LoadSerializedAssets(manager, bundle);
            var atlas = FindAtlas(manager, assets, preferredKind);
            var texture = FindAtlasTexture(manager, assets, atlas);
            var textureState = TextureFile.ReadTextureFile(texture.Field);
            var encodedTexture = textureState.FillPictureData(assets.Instance);
            if (encodedTexture is null || encodedTexture.Length == 0)
                throw new InvalidDataException("The template atlas texture has no embedded image data.");

            using var png = new MemoryStream();
            if (!textureState.DecodeTextureImage(encodedTexture, png, ImageExportType.Png))
                throw new NotSupportedException($"The atlas texture format {textureState.m_TextureFormat} cannot be decoded.");

            var document = ReadDocument(manager, assets.Instance, atlas, textureState.m_Width, textureState.m_Height);
            return new SpriteAtlasBundleContent
            {
                Document = document,
                AtlasPng = png.ToArray(),
                OriginalTextureFormat = (TextureFormat)textureState.m_TextureFormat
            };
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>Replaces one template bundle's atlas texture and sprite data while preserving its target platform structure and bundle compression.</summary>
    /// <param name="template">The dummy bundle built for the requested game platform.</param>
    /// <param name="document">Updated atlas and sprite state.</param>
    /// <param name="atlasPng">PNG bytes for the complete composed atlas.</param>
    /// <param name="outputPath">Path of the resulting raw UnityFS <c>.unity3d</c> bundle.</param>
    /// <param name="textureFormat">Optional Unity texture format. Omit it to retain the template texture's original format.</param>
    public void Export(UnityBundleTemplate template, SpriteAtlasDocument document, byte[] atlasPng, string outputPath,
        TextureFormat? textureFormat = null)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (document is null) throw new ArgumentNullException(nameof(document));
        if (atlasPng is null || atlasPng.Length == 0) throw new ArgumentException("Atlas PNG is required.", nameof(atlasPng));
        if (string.IsNullOrWhiteSpace(template.BundlePath)) throw new ArgumentException("Template bundle path is required.", nameof(template));
        if (!File.Exists(template.BundlePath)) throw new FileNotFoundException("Unity bundle template was not found.", template.BundlePath);
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));
        document.Validate();
        if (string.Equals(Path.GetFullPath(template.BundlePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output path must not overwrite the dummy bundle template.", nameof(outputPath));

        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(template.BundlePath, unpackIfPacked: true);
            var assets = LoadSerializedAssets(manager, bundle);
            var atlas = FindAtlas(manager, assets, document.Kind);
            if (atlas.Kind != document.Kind)
                throw new InvalidDataException($"The {template.Platform} template contains {atlas.Kind}, but the atlas document is {document.Kind}.");

            var texture = FindAtlasTexture(manager, assets, atlas);
            RewritePrefabName(atlas, document.AtlasName);
            RewriteAtlasArray(atlas, document);
            RewriteTexture(texture, atlasPng, document, textureFormat);
            RewriteMaterialName(manager, assets, atlas);
            RewriteBundleContainerName(manager, assets, atlas);
            assets.Directory.SetNewData(assets.Instance.file);
            WriteBundle(bundle, outputPath);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    private static SerializedAssets LoadSerializedAssets(AssetsManager manager, BundleFileInstance bundle)
    {
        for (var index = 0; index < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; index++)
        {
            if (!bundle.file.IsAssetsFile(index)) continue;
            var instance = manager.LoadAssetsFileFromBundle(bundle, index, loadDeps: false);
            return new SerializedAssets(instance, bundle.file.BlockAndDirInfo.DirectoryInfos[index]);
        }
        throw new InvalidDataException("Unity bundle has no serialized asset file.");
    }

    private static AtlasComponent FindAtlas(AssetsManager manager, SerializedAssets assets, SpriteAtlasKind? preferredKind)
    {
        var matches = new List<AtlasComponent>();
        foreach (var info in assets.Instance.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            AssetTypeValueField field;
            try
            {
                field = manager.GetBaseField(assets.Instance, info);
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is InvalidOperationException)
            {
                continue;
            }

            var ngui3Sprites = GetArrayField(field["mSprites"]);
            if (ngui3Sprites != null)
            {
                matches.Add(new AtlasComponent(manager, assets.Instance, info, field, SpriteAtlasKind.NGUI3, ngui3Sprites));
                continue;
            }

            var ngui2Sprites = GetArrayField(field["sprites"]);
            if (ngui2Sprites != null
                && ngui2Sprites.TemplateField.Children.Count > 1
                && ngui2Sprites.TemplateField.Children[1].Children.Any(child => child.Name == "outer"))
                matches.Add(new AtlasComponent(manager, assets.Instance, info, field, SpriteAtlasKind.NGUI2, ngui2Sprites));
        }

        var selected = preferredKind.HasValue
            ? matches.Where(match => match.Kind == preferredKind.Value).ToList()
            : matches;
        if (selected.Count != 1)
            throw new InvalidDataException(preferredKind.HasValue
                ? $"Expected one {preferredKind.Value} atlas component in the dummy bundle; found {selected.Count}."
                : $"Expected one NGUI2/NGUI3 atlas component in the dummy bundle; found {matches.Count}. Pass a preferred kind if it contains both.");
        return selected[0];
    }

    private static AssetTypeValueField GetArrayField(AssetTypeValueField field)
    {
        if (field.IsDummy) return null;
        if (field.TemplateField.IsArray) return field;

        var arrayFields = field.Children.Where(child => child.TemplateField.IsArray).ToList();
        return arrayFields.Count == 1 ? arrayFields[0] : null;
    }

    private static TextureAsset FindAtlasTexture(AssetsManager manager, SerializedAssets assets, AtlasComponent atlas)
    {
        var materialInfo = ResolveLocalPointer(assets.Instance.file, atlas.Field["material"], "atlas material");
        var material = manager.GetBaseField(assets.Instance, materialInfo);
        var texturePointer = FindMainTexturePointer(material);
        var textureInfo = ResolveLocalPointer(assets.Instance.file, texturePointer, "atlas main texture");
        if (textureInfo.TypeId != (int)AssetClassID.Texture2D)
            throw new InvalidDataException("Atlas material's main texture reference is not a Texture2D asset.");
        return new TextureAsset(textureInfo, manager.GetBaseField(assets.Instance, textureInfo));
    }

    private static AssetTypeValueField FindMainTexturePointer(AssetTypeValueField material)
    {
        var texEnvs = GetArrayField(material["m_SavedProperties"]["m_TexEnvs"]);
        if (texEnvs is null)
            throw new InvalidDataException("Template material has no readable texture environment array.");

        foreach (var pair in texEnvs.Children)
        {
            var name = pair["first"].AsString;
            var texture = pair["second"]["m_Texture"];
            if (!texture.IsDummy && (name == "_MainTex" || texEnvs.Children.Count == 1))
                return texture;
        }
        throw new InvalidDataException("Template atlas material does not reference an _MainTex texture.");
    }

    private static AssetFileInfo ResolveLocalPointer(AssetsFile file, AssetTypeValueField pointer, string description)
    {
        if (pointer.IsDummy) throw new InvalidDataException($"The {description} pointer is missing from the template.");
        if (pointer["m_FileID"].AsInt != 0)
            throw new NotSupportedException($"The {description} is stored in a different serialized assets file.");
        var pathId = pointer["m_PathID"].AsLong;
        return file.GetAssetInfo(pathId)
            ?? throw new InvalidDataException($"The {description} points to missing asset path ID {pathId}.");
    }

    private static SpriteAtlasDocument ReadDocument(AssetsManager manager, AssetsFileInstance assetsFile,
        AtlasComponent atlas, int textureWidth, int textureHeight)
    {
        var gameObjectPointer = atlas.Field["m_GameObject"];
        if (gameObjectPointer.IsDummy || gameObjectPointer["m_FileID"].AsInt != 0)
            throw new InvalidDataException("Atlas component has an invalid root GameObject reference.");
        var gameObjectInfo = atlas.AssetsFile.file.GetAssetInfo(gameObjectPointer["m_PathID"].AsLong)
            ?? throw new InvalidDataException("Atlas root GameObject was not found in the template.");

        var gameObject = manager.GetBaseField(assetsFile, gameObjectInfo);
        var document = new SpriteAtlasDocument
        {
            AtlasName = gameObject["m_Name"].AsString,
            Kind = atlas.Kind,
            TextureWidth = textureWidth,
            TextureHeight = textureHeight
        };

        foreach (var spriteField in atlas.SpriteArray.Children)
        {
            var sprite = new SpriteAtlasEntry { Name = spriteField["name"].AsString };
            if (atlas.Kind == SpriteAtlasKind.NGUI2)
            {
                ReadRect(spriteField["outer"], out var x, out var y, out var width, out var height);
                ReadRect(spriteField["inner"], out var innerX, out var innerY, out var innerWidth, out var innerHeight);
                sprite.X = x;
                sprite.Y = y;
                sprite.Width = width;
                sprite.Height = height;
                sprite.InnerX = innerX;
                sprite.InnerY = innerY;
                sprite.InnerWidth = innerWidth;
                sprite.InnerHeight = innerHeight;
                sprite.PaddingLeft = GetFloat(spriteField, "paddingLeft");
                sprite.PaddingRight = GetFloat(spriteField, "paddingRight");
                sprite.PaddingTop = GetFloat(spriteField, "paddingTop");
                sprite.PaddingBottom = GetFloat(spriteField, "paddingBottom");
            }
            else
            {
                sprite.X = spriteField["x"].AsInt;
                sprite.Y = spriteField["y"].AsInt;
                sprite.Width = spriteField["width"].AsInt;
                sprite.Height = spriteField["height"].AsInt;
                sprite.BorderLeft = GetInt(spriteField, "borderLeft");
                sprite.BorderRight = GetInt(spriteField, "borderRight");
                sprite.BorderTop = GetInt(spriteField, "borderTop");
                sprite.BorderBottom = GetInt(spriteField, "borderBottom");
                sprite.PaddingLeft = GetInt(spriteField, "paddingLeft");
                sprite.PaddingRight = GetInt(spriteField, "paddingRight");
                sprite.PaddingTop = GetInt(spriteField, "paddingTop");
                sprite.PaddingBottom = GetInt(spriteField, "paddingBottom");
                sprite.InnerX = sprite.X;
                sprite.InnerY = sprite.Y;
                sprite.InnerWidth = sprite.Width;
                sprite.InnerHeight = sprite.Height;
            }
            document.Sprites.Add(sprite);
        }

        return document;
    }

    private static void RewritePrefabName(AtlasComponent atlas, string atlasName)
    {
        if (string.IsNullOrWhiteSpace(atlasName)) throw new ArgumentException("Atlas name is required.", nameof(atlasName));
        var pointer = atlas.Field["m_GameObject"];
        var gameObjectInfo = ResolveLocalPointer(atlas.AssetsFile.file, pointer, "atlas root GameObject");
        var gameObject = atlas.Manager.GetBaseField(atlas.AssetsFile, gameObjectInfo);
        gameObject["m_Name"].AsString = atlasName;
        gameObjectInfo.SetNewData(gameObject);
        atlas.RootName = atlasName;
    }

    private static void RewriteAtlasArray(AtlasComponent atlas, SpriteAtlasDocument document)
    {
        var array = atlas.SpriteArray;
        var entries = new List<AssetTypeValueField>(document.Sprites.Count);
        foreach (var sprite in document.Sprites)
        {
            var item = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            item["name"].AsString = sprite.Name;
            if (atlas.Kind == SpriteAtlasKind.NGUI2)
            {
                WriteRect(item["outer"], sprite.X, sprite.Y, sprite.Width, sprite.Height);
                WriteRect(item["inner"], sprite.InnerX, sprite.InnerY, sprite.InnerWidth, sprite.InnerHeight);
                SetFloat(item, "paddingLeft", sprite.PaddingLeft);
                SetFloat(item, "paddingRight", sprite.PaddingRight);
                SetFloat(item, "paddingTop", sprite.PaddingTop);
                SetFloat(item, "paddingBottom", sprite.PaddingBottom);
            }
            else
            {
                item["x"].AsInt = checked((int)sprite.X);
                item["y"].AsInt = checked((int)sprite.Y);
                item["width"].AsInt = checked((int)sprite.Width);
                item["height"].AsInt = checked((int)sprite.Height);
                SetInt(item, "borderLeft", sprite.BorderLeft);
                SetInt(item, "borderRight", sprite.BorderRight);
                SetInt(item, "borderTop", sprite.BorderTop);
                SetInt(item, "borderBottom", sprite.BorderBottom);
                SetInt(item, "paddingLeft", checked((int)sprite.PaddingLeft));
                SetInt(item, "paddingRight", checked((int)sprite.PaddingRight));
                SetInt(item, "paddingTop", checked((int)sprite.PaddingTop));
                SetInt(item, "paddingBottom", checked((int)sprite.PaddingBottom));
            }
            entries.Add(item);
        }

        array.Children = entries;
        array.AsArray = new AssetTypeArrayInfo(entries.Count);
        atlas.Info.SetNewData(atlas.Field);
    }

    private static void ReadRect(AssetTypeValueField rect, out float x, out float y, out float width, out float height)
    {
        x = rect["x"].AsFloat;
        y = rect["y"].AsFloat;
        width = rect["width"].AsFloat;
        height = rect["height"].AsFloat;
    }

    private static void WriteRect(AssetTypeValueField rect, float x, float y, float width, float height)
    {
        rect["x"].AsFloat = x;
        rect["y"].AsFloat = y;
        rect["width"].AsFloat = width;
        rect["height"].AsFloat = height;
    }

    private static float GetFloat(AssetTypeValueField parent, string name) => parent[name].AsFloat;

    private static int GetInt(AssetTypeValueField parent, string name) => parent[name].AsInt;

    private static void SetFloat(AssetTypeValueField parent, string name, float value) => parent[name].AsFloat = value;

    private static void SetInt(AssetTypeValueField parent, string name, int value) => parent[name].AsInt = value;

    private static void RewriteTexture(TextureAsset textureAsset, byte[] atlasPng, SpriteAtlasDocument document,
        TextureFormat? requestedFormat)
    {
        var texture = TextureFile.ReadTextureFile(textureAsset.Field);
        var image = ImageResult.FromMemory(atlasPng, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        if (image.Width != document.TextureWidth || image.Height != document.TextureHeight)
            throw new InvalidDataException($"PNG dimensions {image.Width}x{image.Height} do not match the document dimensions {document.TextureWidth}x{document.TextureHeight}.");
        var format = requestedFormat ?? (TextureFormat)texture.m_TextureFormat;
        var imagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllBytes(imagePath, atlasPng);
            texture.EncodeTextureImage(imagePath, format, Math.Max(1, texture.m_MipCount), quality: 5);
        }
        catch (NotSupportedException exception)
        {
            throw new NotSupportedException($"The requested texture format {format} ({(int)format}) cannot be encoded by AssetsTools.NET.Texture.", exception);
        }
        finally
        {
            if (File.Exists(imagePath)) File.Delete(imagePath);
        }
        if (texture.m_Width != document.TextureWidth || texture.m_Height != document.TextureHeight)
            throw new InvalidDataException($"PNG dimensions {document.TextureWidth}x{document.TextureHeight} do not match the texture encoding result {texture.m_Width}x{texture.m_Height}.");
        texture.m_Name = document.AtlasName;
        texture.WriteTo(textureAsset.Field);
        textureAsset.Info.SetNewData(textureAsset.Field);
    }

    private static void RewriteMaterialName(AssetsManager manager, SerializedAssets assets, AtlasComponent atlas)
    {
        var materialInfo = ResolveLocalPointer(assets.Instance.file, atlas.Field["material"], "atlas material");
        var material = manager.GetBaseField(assets.Instance, materialInfo);
        material["m_Name"].AsString = atlas.RootName;
        materialInfo.SetNewData(material);
    }

    private static void RewriteBundleContainerName(AssetsManager manager, SerializedAssets assets, AtlasComponent atlas)
    {
        var rootPointer = atlas.Field["m_GameObject"];
        var rootPathId = rootPointer["m_PathID"].AsLong;
        var bundleInfo = assets.Instance.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault();
        if (bundleInfo is null)
            throw new InvalidDataException("Template serialized assets do not include the AssetBundle container object.");

        var bundleField = manager.GetBaseField(assets.Instance, bundleInfo);
        var container = GetArrayField(bundleField["m_Container"]);
        if (container is null)
            throw new InvalidDataException("Template AssetBundle has no readable prefab container table.");

        var renamed = false;
        var prefabEntries = new List<AssetTypeValueField>();
        foreach (var pair in container.Children)
        {
            var path = pair["first"].AsString;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                prefabEntries.Add(pair);
            var assetPointer = pair["second"]["asset"];
            if (assetPointer.IsDummy || assetPointer["m_FileID"].AsInt != 0 || assetPointer["m_PathID"].AsLong != rootPathId)
                continue;
            pair["first"].AsString = "Assets/dlc/" + atlas.RootName + ".prefab";
            renamed = true;
        }

        if (!renamed && prefabEntries.Count == 1)
        {
            prefabEntries[0]["first"].AsString = "Assets/dlc/" + atlas.RootName + ".prefab";
            renamed = true;
        }
        if (!renamed)
            throw new InvalidDataException("Template AssetBundle container does not reference the atlas root GameObject.");
        if (!bundleField["m_Name"].IsDummy)
            bundleField["m_Name"].AsString = atlas.RootName;
        bundleInfo.SetNewData(bundleField);
    }

    private static void WriteBundle(BundleFileInstance bundle, string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

        byte[] uncompressedBytes;
        using (var uncompressed = new MemoryStream())
        {
            using (var writer = new AssetsFileWriter(uncompressed))
                bundle.file.Write(writer);
            uncompressedBytes = uncompressed.ToArray();
        }

        using var uncompressedStream = new MemoryStream(uncompressedBytes, writable: false);
        var rewritten = new BundleFileInstance(uncompressedStream, outputPath, unpackIfPacked: true);
        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var outputWriter = new AssetsFileWriter(output);
        if (bundle.originalCompression == AssetBundleCompressionType.None)
            rewritten.file.Write(outputWriter);
        else
            rewritten.file.Pack(outputWriter, bundle.originalCompression);
    }

    private sealed class SerializedAssets(AssetsFileInstance instance, AssetBundleDirectoryInfo directory)
    {
        public AssetsFileInstance Instance { get; } = instance;
        public AssetBundleDirectoryInfo Directory { get; } = directory;
    }

    private sealed class AtlasComponent(AssetsManager manager, AssetsFileInstance assetsFile, AssetFileInfo info,
        AssetTypeValueField field, SpriteAtlasKind kind, AssetTypeValueField spriteArray)
    {
        public AssetsManager Manager { get; } = manager;
        public AssetsFileInstance AssetsFile { get; } = assetsFile;
        public AssetFileInfo Info { get; } = info;
        public AssetTypeValueField Field { get; } = field;
        public SpriteAtlasKind Kind { get; } = kind;
        public AssetTypeValueField SpriteArray { get; } = spriteArray;
        public string RootName { get; set; } = string.Empty;
    }

    private sealed class TextureAsset(AssetFileInfo info, AssetTypeValueField field)
    {
        public AssetFileInfo Info { get; } = info;
        public AssetTypeValueField Field { get; } = field;
    }
}
