using System;
using System.Collections.Generic;

namespace Assets.Lib.Models;

/// <summary>Project-local index of discovered song jacket atlases.</summary>
public sealed class EyecatchProjectIndex
{
    public int FormatVersion { get; set; } = 1;
    public DateTimeOffset? LastBuiltAtUtc { get; set; }
    public List<EyecatchAtlasFamily> Atlases { get; set; } = [];
}

/// <summary>NGUI2 and NGUI3 variants that share an atlas key such as e18.</summary>
public sealed class EyecatchAtlasFamily
{
    public string AtlasKey { get; set; } = string.Empty;
    public bool HasPlatformLayoutMismatch { get; set; }
    public bool HasSpriteNameMismatch { get; set; }
    public List<EyecatchAtlasVariant> Variants { get; set; } = [];
}

/// <summary>One NGUI schema and platform instance of an atlas bundle.</summary>
public sealed class EyecatchAtlasVariant
{
    public string AtlasKey { get; set; } = string.Empty;
    public SpriteAtlasKind Kind { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string AtlasName { get; set; } = string.Empty;
    public string ResourcePath { get; set; } = string.Empty;
    public bool ResourceRegistered { get; set; }
    public bool IsInstallPack { get; set; }
    public bool Exists { get; set; }
    public EyecatchVariantStatus Status { get; set; } = EyecatchVariantStatus.Unindexed;
    public string SourceChecksum { get; set; } = string.Empty;
    public string LastBuiltChecksum { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public string Error { get; set; } = string.Empty;
    public string AtlasImagePath { get; set; } = string.Empty;
    public SpriteAtlasDocument Document { get; set; } = new();
    public List<EyecatchSpriteAsset> SpriteAssets { get; set; } = [];
}

/// <summary>Path to one extracted sprite image in the project asset directory.</summary>
public sealed class EyecatchSpriteAsset
{
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

/// <summary>Indexing state for one platform atlas resource.</summary>
public enum EyecatchVariantStatus
{
    Unindexed,
    Missing,
    Ready,
    Error,
    SourceChanged
}
