using System;
using System.Globalization;
using System.IO;
using System.Text;
using Assets.Lib.Models;

namespace Assets.Lib.Services;

/// <summary>Serializes a sprite atlas model as a Unity 5 prefab text asset.</summary>
public static class SpriteAtlasPrefabYaml
{
    private const long GameObjectId = 1709254077376921;
    private const long TransformId = 4730242686860875;
    private const long AtlasComponentId = 114724804207875695;
    private const long PrefabId = 100100000;

    /// <summary>Writes the prefab YAML for the model, preserving the selected NGUI2 or NGUI3 schema.</summary>
    /// <param name="document">Atlas model to serialize.</param>
    /// <param name="scriptGuid">Unity GUID of the game's matching atlas MonoBehaviour script.</param>
    /// <param name="materialGuid">Unity GUID of the material assigned to the atlas component.</param>
    /// <returns>Unity text-serialized prefab content.</returns>
    public static string Serialize(SpriteAtlasDocument document, string scriptGuid, string materialGuid)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        ValidateGuid(scriptGuid, nameof(scriptGuid));
        ValidateGuid(materialGuid, nameof(materialGuid));
        document.Validate();

        var builder = new StringBuilder(2048 + document.Sprites.Count * 420);
        builder.AppendLine("%YAML 1.1");
        builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        builder.AppendLine("--- !u!1 &" + GameObjectId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("GameObject:");
        builder.AppendLine("  serializedVersion: 5");
        builder.AppendLine("  m_ObjectHideFlags: 0");
        builder.AppendLine("  m_PrefabParentObject: {fileID: 0}");
        builder.AppendLine("  m_PrefabInternal: {fileID: " + PrefabId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_Component:");
        builder.AppendLine("  - component: {fileID: " + TransformId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  - component: {fileID: " + AtlasComponentId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_Layer: 0");
        builder.AppendLine("  m_Name: " + EscapeYaml(document.AtlasName));
        builder.AppendLine("  m_TagString: Untagged");
        builder.AppendLine("  m_Icon: {fileID: 0}");
        builder.AppendLine("  m_NavMeshLayer: 0");
        builder.AppendLine("  m_StaticEditorFlags: 0");
        builder.AppendLine("  m_IsActive: 1");
        builder.AppendLine("--- !u!4 &" + TransformId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("Transform:");
        builder.AppendLine("  m_ObjectHideFlags: 0");
        builder.AppendLine("  m_PrefabParentObject: {fileID: 0}");
        builder.AppendLine("  m_PrefabInternal: {fileID: " + PrefabId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_GameObject: {fileID: " + GameObjectId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}");
        builder.AppendLine("  m_LocalPosition: {x: 0, y: 0, z: 0}");
        builder.AppendLine("  m_LocalScale: {x: 1, y: 1, z: 1}");
        builder.AppendLine("  m_Children: []");
        builder.AppendLine("  m_Father: {fileID: 0}");
        builder.AppendLine("  m_RootOrder: 0");
        builder.AppendLine("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
        builder.AppendLine("--- !u!114 &" + AtlasComponentId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("MonoBehaviour:");
        builder.AppendLine("  m_ObjectHideFlags: 0");
        builder.AppendLine("  m_PrefabParentObject: {fileID: 0}");
        builder.AppendLine("  m_PrefabInternal: {fileID: " + PrefabId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_GameObject: {fileID: " + GameObjectId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_Enabled: 1");
        builder.AppendLine("  m_EditorHideFlags: 0");
        builder.AppendLine("  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}");
        builder.AppendLine("  m_Name:");
        builder.AppendLine("  m_EditorClassIdentifier:");
        builder.AppendLine("  material: {fileID: 2100000, guid: " + materialGuid + ", type: 2}");

        if (document.Kind == SpriteAtlasKind.NGUI2)
            WriteNGUI2Sprites(builder, document);
        else
            WriteNGUI3Sprites(builder, document);

        builder.AppendLine("  mCoordinates: 0");
        builder.AppendLine("  mPixelSize: 1");
        builder.AppendLine("  mReplacement: {fileID: 0}");
        if (document.Kind == SpriteAtlasKind.NGUI3)
            builder.AppendLine("  sprites: []");
        builder.AppendLine("--- !u!1001 &" + PrefabId.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("Prefab:");
        builder.AppendLine("  serializedVersion: 2");
        builder.AppendLine("  m_ObjectHideFlags: 1");
        builder.AppendLine("  m_Modification:");
        builder.AppendLine("    m_TransformParent: {fileID: 0}");
        builder.AppendLine("    m_Modifications: []");
        builder.AppendLine("    m_RemovedComponents: []");
        builder.AppendLine("  m_ParentPrefab: {fileID: 0}");
        builder.AppendLine("  m_RootGameObject: {fileID: " + GameObjectId.ToString(CultureInfo.InvariantCulture) + "}");
        builder.AppendLine("  m_IsPrefabParent: 1");
        return builder.ToString();
    }

    /// <summary>Writes a prefab and matching asset meta file from the atlas model.</summary>
    /// <param name="document">Atlas model to serialize.</param>
    /// <param name="prefabPath">Output path for the <c>.prefab</c> file.</param>
    /// <param name="scriptGuid">Unity GUID of the game's matching atlas MonoBehaviour script.</param>
    /// <param name="materialGuid">Unity GUID of the material assigned to the atlas component.</param>
    /// <param name="prefabGuid">Unique Unity asset GUID for the generated prefab.</param>
    public static void Write(SpriteAtlasDocument document, string prefabPath, string scriptGuid, string materialGuid, string prefabGuid)
    {
        if (string.IsNullOrWhiteSpace(prefabPath)) throw new ArgumentException("Prefab path is required.", nameof(prefabPath));
        var fullPath = Path.GetFullPath(prefabPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, Serialize(document, scriptGuid, materialGuid), new UTF8Encoding(false));
        File.WriteAllText(fullPath + ".meta", SerializeMeta(document, prefabGuid), new UTF8Encoding(false));
    }

    /// <summary>Writes a Unity asset meta file that assigns the prefab to its matching bundle.</summary>
    /// <param name="document">Atlas model to serialize.</param>
    /// <param name="prefabGuid">Unique Unity asset GUID for the prefab.</param>
    /// <returns>Unity text-serialized meta content.</returns>
    public static string SerializeMeta(SpriteAtlasDocument document, string prefabGuid)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        ValidateGuid(prefabGuid, nameof(prefabGuid));
        document.Validate();
        return "fileFormatVersion: 2\n"
            + "guid: " + prefabGuid + "\n"
            + "NativeFormatImporter:\n"
            + "  name:\n"
            + "  mainObjectFileID: 100100000\n"
            + "  userData:\n"
            + "  assetBundleName: " + document.AtlasName + ".unity3d\n"
            + "  assetBundleVariant:\n";
    }

    private static void WriteNGUI2Sprites(StringBuilder builder, SpriteAtlasDocument document)
    {
        builder.AppendLine("  sprites:");
        foreach (var sprite in document.Sprites)
        {
            builder.AppendLine("  - name: " + EscapeYaml(sprite.Name));
            WriteRect(builder, "outer", sprite.X, sprite.Y, sprite.Width, sprite.Height);
            WriteRect(builder, "inner", sprite.InnerX, sprite.InnerY, sprite.InnerWidth, sprite.InnerHeight);
            WritePadding(builder, sprite);
        }
    }

    private static void WriteNGUI3Sprites(StringBuilder builder, SpriteAtlasDocument document)
    {
        builder.AppendLine("  mSprites:");
        foreach (var sprite in document.Sprites)
        {
            builder.AppendLine("  - name: " + EscapeYaml(sprite.Name));
            builder.AppendLine("    x: " + PixelValue(sprite.X));
            builder.AppendLine("    y: " + PixelValue(sprite.Y));
            builder.AppendLine("    width: " + PixelValue(sprite.Width));
            builder.AppendLine("    height: " + PixelValue(sprite.Height));
            builder.AppendLine("    borderLeft: " + sprite.BorderLeft.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("    borderRight: " + sprite.BorderRight.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("    borderTop: " + sprite.BorderTop.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("    borderBottom: " + sprite.BorderBottom.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("    paddingLeft: " + PixelValue(sprite.PaddingLeft));
            builder.AppendLine("    paddingRight: " + PixelValue(sprite.PaddingRight));
            builder.AppendLine("    paddingTop: " + PixelValue(sprite.PaddingTop));
            builder.AppendLine("    paddingBottom: " + PixelValue(sprite.PaddingBottom));
        }
    }

    private static void WriteRect(StringBuilder builder, string name, float x, float y, float width, float height)
    {
        builder.AppendLine("    " + name + ":");
        builder.AppendLine("      serializedVersion: 2");
        builder.AppendLine("      x: " + PixelValue(x));
        builder.AppendLine("      y: " + PixelValue(y));
        builder.AppendLine("      width: " + PixelValue(width));
        builder.AppendLine("      height: " + PixelValue(height));
    }

    private static void WritePadding(StringBuilder builder, SpriteAtlasEntry sprite)
    {
        builder.AppendLine("    paddingLeft: " + PixelValue(sprite.PaddingLeft));
        builder.AppendLine("    paddingRight: " + PixelValue(sprite.PaddingRight));
        builder.AppendLine("    paddingTop: " + PixelValue(sprite.PaddingTop));
        builder.AppendLine("    paddingBottom: " + PixelValue(sprite.PaddingBottom));
    }

    private static string PixelValue(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string EscapeYaml(string value)
    {
        if (value.IndexOfAny(new[] { ':', '#', '\'', '"', '[', ']', '{', '}', ',', '&', '*', '!', '|', '>', '%', '@', '`', '\n', '\r' }) >= 0
            || value.StartsWith(" ", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal))
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        return value;
    }

    private static void ValidateGuid(string guid, string parameterName)
    {
        if (guid is null || guid.Length != 32)
            throw new ArgumentException("Unity GUID must contain exactly 32 hexadecimal characters.", parameterName);
        for (var index = 0; index < guid.Length; index++)
        {
            var value = guid[index];
            if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F')))
                throw new ArgumentException("Unity GUID must contain exactly 32 hexadecimal characters.", parameterName);
        }
    }
}
