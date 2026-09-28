using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assets.Lib.Models;
using Assets.Lib.Services;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Components.Services;

public sealed record EyecatchIndexProgress(int Completed, int Total, string CurrentResource);

/// <summary>Builds and persists the project-local index and extracted assets for eyecatch bundles.</summary>
public sealed class EyecatchIndexService(SpriteAtlasBundleService bundleService)
{
    private const string IndexFileName = "eyecatch.json";
    private const string AssetDirectory = ".dmtq/eyecatch";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] Platforms = ["android", "ios"];

    /// <summary>Loads an existing project index or returns an empty index when none has been built.</summary>
    public async Task<EyecatchProjectIndex> LoadAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var indexPath = Path.Combine(projectRoot, IndexFileName);
        if (!File.Exists(indexPath)) return new EyecatchProjectIndex();

        await using var stream = File.OpenRead(indexPath);
        var index = await JsonSerializer.DeserializeAsync<EyecatchProjectIndex>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (index is null || index.FormatVersion != 1)
            throw new InvalidDataException("eyecatch.json is empty or uses an unsupported format version.");
        return index;
    }

    /// <summary>Scans all known NGUI2/NGUI3 Android/iOS bundles and extracts their atlas and sprite images.</summary>
    public async Task<EyecatchProjectIndex> BuildAsync(
        PatchPackage package,
        string projectRoot,
        IProgress<EyecatchIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var index = await LoadAsync(fullProjectRoot, cancellationToken).ConfigureAwait(false);
        var candidates = DiscoverCandidates(package, fullProjectRoot);
        var keys = index.Atlases.Select(atlas => atlas.AtlasKey)
            .Concat(candidates.Select(candidate => candidate.AtlasKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var total = keys.Length * 4;
        var completed = 0;

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (family is null)
            {
                family = new EyecatchAtlasFamily { AtlasKey = key };
                index.Atlases.Add(family);
            }

            foreach (var kind in new[] { SpriteAtlasKind.NGUI2, SpriteAtlasKind.NGUI3 })
            foreach (var platform in Platforms)
            {
                var atlasName = (kind == SpriteAtlasKind.NGUI2 ? "d_" : "d3_") + key;
                var variant = family.Variants.FirstOrDefault(item => item.Kind == kind
                    && item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
                if (variant is null)
                {
                    variant = new EyecatchAtlasVariant { AtlasKey = key, Kind = kind, Platform = platform, AtlasName = atlasName };
                    family.Variants.Add(variant);
                }

                var candidate = SelectCandidate(candidates, key, kind, platform, fullProjectRoot);
                await IndexVariantAsync(package, fullProjectRoot, variant, candidate, cancellationToken)
                    .ConfigureAwait(false);
                progress?.Report(new EyecatchIndexProgress(++completed, total, variant.ResourcePath));
            }

            family.HasPlatformLayoutMismatch = HasPlatformLayoutMismatch(family);
            family.HasSpriteNameMismatch = HasSpriteNameMismatch(family);
        }

        index.Atlases = index.Atlases.OrderBy(atlas => atlas.AtlasKey, StringComparer.OrdinalIgnoreCase).ToList();
        index.LastBuiltAtUtc = DateTimeOffset.UtcNow;
        await SaveAsync(fullProjectRoot, index, cancellationToken).ConfigureAwait(false);
        return index;
    }

    /// <summary>Checks and indexes only the NGUI3 Android/iOS pair for one atlas, leaving legacy NGUI2 data untouched.</summary>
    public async Task<EyecatchProjectIndex> CheckNGUI3FamilyAsync(
        PatchPackage package,
        string projectRoot,
        string atlasKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(atlasKey);

        var root = Path.GetFullPath(projectRoot);
        var index = await LoadAsync(root, cancellationToken).ConfigureAwait(false);
        var candidates = DiscoverCandidates(package, root);
        var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase));
        if (family is null)
        {
            family = new EyecatchAtlasFamily { AtlasKey = atlasKey };
            index.Atlases.Add(family);
        }

        foreach (var platform in Platforms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var variant = family.Variants.FirstOrDefault(item => item.Kind == SpriteAtlasKind.NGUI3
                && item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
            if (variant is null)
            {
                variant = new EyecatchAtlasVariant
                {
                    AtlasKey = atlasKey,
                    Kind = SpriteAtlasKind.NGUI3,
                    Platform = platform,
                    AtlasName = $"d3_{atlasKey}"
                };
                family.Variants.Add(variant);
            }

            var candidate = SelectCandidate(candidates, atlasKey, SpriteAtlasKind.NGUI3, platform, root);
            await IndexVariantAsync(package, root, variant, candidate, cancellationToken).ConfigureAwait(false);
        }

        family.HasPlatformLayoutMismatch = HasPlatformLayoutMismatch(family);
        family.HasSpriteNameMismatch = HasSpriteNameMismatch(family);
        index.Atlases = index.Atlases.OrderBy(atlas => atlas.AtlasKey, StringComparer.OrdinalIgnoreCase).ToList();
        await SaveAsync(root, index, cancellationToken).ConfigureAwait(false);
        return index;
    }

    /// <summary>Loads an indexed PNG as a data URL for the list preview.</summary>
    public async Task<string?> ReadImageDataUrlAsync(string projectRoot, string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var bytes = await ReadImageBytesAsync(projectRoot, relativePath, cancellationToken).ConfigureAwait(false);
        if (bytes is null) return null;
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    /// <summary>Reads an indexed project asset after validating its relative path.</summary>
    public async Task<byte[]?> ReadImageBytesAsync(string projectRoot, string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var imagePath = ResolveProjectPath(projectRoot, relativePath);
        if (!File.Exists(imagePath)) return null;
        return await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds a cached sprite image by its exact game lookup name.</summary>
    public async Task<string?> FindSpriteImageDataUrlAsync(string projectRoot, string spriteName, CancellationToken cancellationToken = default)
    {
        var (atlasKey, _) = ParseSpriteName(spriteName);
        var index = await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase));
        var imagePath = family?.Variants
            .Where(variant => variant.Status == EyecatchVariantStatus.Ready)
            .OrderBy(variant => variant.Kind == SpriteAtlasKind.NGUI3 ? 0 : 1)
            .ThenBy(variant => variant.Platform.Equals("android", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .SelectMany(variant => variant.SpriteAssets)
            .FirstOrDefault(asset => asset.Name.Equals(spriteName, StringComparison.OrdinalIgnoreCase))?.ImagePath;
        return await ReadImageDataUrlAsync(projectRoot, imagePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces a sprite source image in every indexed variant containing the sprite and marks those variants dirty.</summary>
    public async Task<int> ReplaceSpriteImageAsync(
        string projectRoot,
        string spriteName,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var (atlasKey, _) = ParseSpriteName(spriteName);
        var png = SpriteAtlasImage.NormalizeToPng(imageBytes);
        var index = await LoadAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Atlas '{atlasKey}' has not been indexed.");
        var updated = 0;
        foreach (var variant in family.Variants.Where(item => item.Status == EyecatchVariantStatus.Ready && item.Document is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sprite = variant.Document.Sprites.FirstOrDefault(item => item.Name.Equals(spriteName, StringComparison.OrdinalIgnoreCase));
            var asset = variant.SpriteAssets.FirstOrDefault(item => item.Name.Equals(spriteName, StringComparison.OrdinalIgnoreCase));
            if (sprite is null || asset is null || string.IsNullOrWhiteSpace(variant.AtlasImagePath)) continue;

            var atlasPath = ResolveProjectPath(projectRoot, variant.AtlasImagePath);
            var originalAtlas = await File.ReadAllBytesAsync(atlasPath, cancellationToken).ConfigureAwait(false);
            var updatedAtlas = SpriteAtlasImage.Compose(variant.Document,
                new Dictionary<string, byte[]>(StringComparer.Ordinal) { [sprite.Name] = png }, originalAtlas);
            var updatedSprite = SpriteAtlasImage.Crop(updatedAtlas, variant.Document.TextureWidth, variant.Document.TextureHeight, sprite);
            await WriteAssetAsync(projectRoot, variant.AtlasImagePath, updatedAtlas, cancellationToken).ConfigureAwait(false);
            await WriteAssetAsync(projectRoot, asset.ImagePath, updatedSprite, cancellationToken).ConfigureAwait(false);
            variant.IsDirty = true;
            updated++;
        }

        if (updated == 0)
            throw new FileNotFoundException($"No indexed atlas contains sprite '{spriteName}'.");
        await SaveAsync(Path.GetFullPath(projectRoot), index, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>Updates one indexed variant after its bundle has been generated successfully.</summary>
    public async Task RefreshBuiltVariantAsync(
        string projectRoot,
        EyecatchAtlasVariant variant,
        string resourcePath,
        bool isInstallPack,
        SpriteAtlasDocument document,
        byte[] atlasPng,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = FileUtility.NormalizePackageRelativePath(resourcePath);
        var diskPath = Path.GetFullPath(Path.Combine(projectRoot, "resources", variant.Platform,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var sourceChecksum = await FileUtility.ComputeMd5Async(diskPath, cancellationToken).ConfigureAwait(false);
        var variantRoot = $"{AssetDirectory}/{variant.AtlasKey}/{variant.Kind}/{variant.Platform}";
        var atlasImagePath = $"{variantRoot}/atlas.png";
        await WriteAssetAsync(projectRoot, atlasImagePath, atlasPng, cancellationToken).ConfigureAwait(false);
        var spriteAssets = new List<EyecatchSpriteAsset>(document.Sprites.Count);
        foreach (var sprite in document.Sprites)
        {
            var imagePath = $"{variantRoot}/sprites/{SpriteAssetFileName(sprite.Name)}";
            var crop = SpriteAtlasImage.Crop(atlasPng, document.TextureWidth, document.TextureHeight, sprite);
            await WriteAssetAsync(projectRoot, imagePath, crop, cancellationToken).ConfigureAwait(false);
            spriteAssets.Add(new EyecatchSpriteAsset { Name = sprite.Name, ImagePath = imagePath });
        }

        variant.AtlasName = document.AtlasName;
        variant.ResourcePath = normalizedPath;
        variant.ResourceRegistered = true;
        variant.IsInstallPack = isInstallPack;
        variant.Exists = true;
        variant.Status = EyecatchVariantStatus.Ready;
        variant.SourceChecksum = sourceChecksum;
        variant.LastBuiltChecksum = sourceChecksum;
        variant.IsDirty = false;
        variant.Error = string.Empty;
        variant.AtlasImagePath = atlasImagePath;
        variant.Document = document.Clone();
        variant.SpriteAssets = spriteAssets;
    }

    /// <summary>Applies a project atlas edit to both platform caches of the same NGUI generation.</summary>
    public async Task ApplyEditedVariantAsync(
        string projectRoot,
        string platform,
        string resourcePath,
        SpriteAtlasDocument document,
        byte[] atlasPng,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(projectRoot);
        var index = await LoadAsync(root, cancellationToken).ConfigureAwait(false);
        var (atlasKey, kind) = ParseAtlasName(document.AtlasName);
        var family = index.Atlases.FirstOrDefault(item => item.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase));
        if (family is null) return;

        var currentPath = FileUtility.NormalizePackageRelativePath(resourcePath);
        foreach (var variant in family.Variants.Where(item => item.Kind == kind))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isCurrent = variant.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase);
            if (!isCurrent && variant.Status != EyecatchVariantStatus.Ready) continue;

            if (!isCurrent && variant.Document is not null && !DocumentsHaveSameLayout(document, variant.Document))
            {
                variant.IsDirty = true;
                continue;
            }

            var variantResourcePath = isCurrent || string.IsNullOrWhiteSpace(variant.ResourcePath)
                ? currentPath
                : variant.ResourcePath;
            var variantRoot = $"{AssetDirectory}/{variant.AtlasKey}/{variant.Kind}/{variant.Platform}";
            var atlasImagePath = $"{variantRoot}/atlas.png";
            await WriteAssetAsync(root, atlasImagePath, atlasPng, cancellationToken).ConfigureAwait(false);
            var spriteAssets = new List<EyecatchSpriteAsset>(document.Sprites.Count);
            foreach (var sprite in document.Sprites)
            {
                var imagePath = $"{variantRoot}/sprites/{SpriteAssetFileName(sprite.Name)}";
                var crop = SpriteAtlasImage.Crop(atlasPng, document.TextureWidth, document.TextureHeight, sprite);
                await WriteAssetAsync(root, imagePath, crop, cancellationToken).ConfigureAwait(false);
                spriteAssets.Add(new EyecatchSpriteAsset { Name = sprite.Name, ImagePath = imagePath });
            }

            variant.AtlasName = document.AtlasName;
            variant.ResourcePath = variantResourcePath;
            variant.ResourceRegistered = true;
            variant.Exists = true;
            variant.Status = EyecatchVariantStatus.Ready;
            variant.AtlasImagePath = atlasImagePath;
            variant.Document = document.Clone();
            variant.SpriteAssets = spriteAssets;
            if (isCurrent)
            {
                var diskPath = Path.GetFullPath(Path.Combine(root, "resources", variant.Platform,
                    currentPath.Replace('/', Path.DirectorySeparatorChar)));
                var checksum = await FileUtility.ComputeMd5Async(diskPath, cancellationToken).ConfigureAwait(false);
                variant.SourceChecksum = checksum;
                variant.LastBuiltChecksum = checksum;
                variant.IsDirty = false;
            }
            else
            {
                variant.IsDirty = true;
            }
        }

        family.HasPlatformLayoutMismatch = HasPlatformLayoutMismatch(family);
        family.HasSpriteNameMismatch = HasSpriteNameMismatch(family);
        await SaveAsync(root, index, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Atomically saves the standalone eyecatch index without changing the project schema.</summary>
    public Task SaveIndexAsync(string projectRoot, EyecatchProjectIndex index, CancellationToken cancellationToken = default)
        => SaveAsync(Path.GetFullPath(projectRoot), index, cancellationToken);

    private static async Task SaveAsync(string projectRoot, EyecatchProjectIndex index, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(projectRoot, IndexFileName);
        var tempPath = Path.Combine(projectRoot, $".{IndexFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static List<AtlasCandidate> DiscoverCandidates(PatchPackage package, string projectRoot)
    {
        var candidates = new Dictionary<string, AtlasCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in package.Resources.Where(resource => resource.Category.Equals("dlc", StringComparison.OrdinalIgnoreCase)))
        {
            if (!TryParseAtlasName(resource.FileName, out var key, out var atlasName, out var kind)) continue;
            AddCandidate(candidates, new AtlasCandidate(key, atlasName, kind, resource.FileName, resource, true));
        }

        foreach (var platform in Platforms)
        {
            var dlcRoot = Path.Combine(projectRoot, "resources", platform, "dlc");
            if (!Directory.Exists(dlcRoot)) continue;
            foreach (var path in Directory.EnumerateFiles(dlcRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(Path.Combine(projectRoot, "resources", platform), path)
                    .Replace('\\', '/');
                if (!TryParseAtlasName(relativePath, out var key, out var atlasName, out var kind)) continue;
                AddCandidate(candidates, new AtlasCandidate(key, atlasName, kind, relativePath, null, false));
            }
        }

        return candidates.Values.ToList();
    }

    private static void AddCandidate(IDictionary<string, AtlasCandidate> candidates, AtlasCandidate candidate)
    {
        var identity = $"{candidate.AtlasKey}|{candidate.Kind}|{candidate.ResourcePath}|{candidate.Registered}";
        if (!candidates.ContainsKey(identity)) candidates.Add(identity, candidate);
    }

    private static AtlasCandidate? SelectCandidate(
        IReadOnlyCollection<AtlasCandidate> candidates,
        string key,
        SpriteAtlasKind kind,
        string platform,
        string projectRoot)
    {
        return candidates
            .Where(candidate => candidate.AtlasKey.Equals(key, StringComparison.OrdinalIgnoreCase) && candidate.Kind == kind)
            .Where(candidate => candidate.Resource is null
                || candidate.Resource.PlatformManifest.Count == 0
                || candidate.Resource.PlatformManifest.Any(entry => entry.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => new
            {
                Candidate = candidate,
                Exists = ResolveSourcePath(projectRoot, candidate.ResourcePath, platform) is not null,
                ManifestExists = candidate.Resource?.PlatformManifest.FirstOrDefault(entry =>
                    entry.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))?.Exist ?? false,
                ExtensionPriority = Path.GetExtension(candidate.ResourcePath).Equals(".unity3d", StringComparison.OrdinalIgnoreCase) ? 0 : 1
            })
            .OrderByDescending(item => item.Exists)
            .ThenByDescending(item => item.ManifestExists)
            .ThenBy(item => item.ExtensionPriority)
            .Select(item => item.Candidate)
            .FirstOrDefault();
    }

    private async Task IndexVariantAsync(
        PatchPackage package,
        string projectRoot,
        EyecatchAtlasVariant variant,
        AtlasCandidate? candidate,
        CancellationToken cancellationToken)
    {
        variant.Exists = false;
        variant.Error = string.Empty;
        if (candidate is null)
        {
            variant.Status = EyecatchVariantStatus.Missing;
            variant.ResourcePath = string.Empty;
            variant.ResourceRegistered = false;
            variant.IsInstallPack = false;
            return;
        }

        variant.AtlasName = candidate.AtlasName;
        variant.AtlasKey = candidate.AtlasKey;
        variant.ResourcePath = FileUtility.NormalizePackageRelativePath(candidate.ResourcePath);
        variant.ResourceRegistered = candidate.Resource is not null;
        variant.IsInstallPack = candidate.Resource?.PlatformManifest.FirstOrDefault(entry =>
            entry.Platform.Equals(variant.Platform, StringComparison.OrdinalIgnoreCase))?.IsInstallPack ?? false;

        var sourcePath = ResolveSourcePath(projectRoot, variant.ResourcePath, variant.Platform);
        if (sourcePath is null)
        {
            variant.Status = EyecatchVariantStatus.Missing;
            return;
        }

        variant.Exists = true;
        var sourceChecksum = await FileUtility.ComputeMd5Async(sourcePath, cancellationToken).ConfigureAwait(false);
        var assetsStillExist = !string.IsNullOrWhiteSpace(variant.AtlasImagePath)
            && File.Exists(ResolveProjectPath(projectRoot, variant.AtlasImagePath))
            && variant.SpriteAssets.All(asset => File.Exists(ResolveProjectPath(projectRoot, asset.ImagePath)));

        if (variant.IsDirty && !variant.SourceChecksum.Equals(sourceChecksum, StringComparison.OrdinalIgnoreCase))
        {
            variant.Status = EyecatchVariantStatus.SourceChanged;
            variant.Error = "Source bundle changed while this variant has local edits. Resolve the conflict before rebuilding the index.";
            return;
        }

        if (variant.Status == EyecatchVariantStatus.Ready
            && variant.SourceChecksum.Equals(sourceChecksum, StringComparison.OrdinalIgnoreCase)
            && assetsStillExist)
        {
            return;
        }

        string? unpackedPath = null;
        try
        {
            var readPath = sourcePath;
            if (sourcePath.EndsWith(".lz4", StringComparison.OrdinalIgnoreCase))
            {
                unpackedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.unity3d");
                await FileUtility.DecompressFileAsync(sourcePath, unpackedPath, cancellationToken).ConfigureAwait(false);
                readPath = unpackedPath;
            }

            var content = await Task.Run(() => bundleService.Read(readPath, variant.Kind), cancellationToken)
                .ConfigureAwait(false);
            var variantRoot = $"{AssetDirectory}/{variant.AtlasKey}/{variant.Kind}/{variant.Platform}";
            var atlasImagePath = $"{variantRoot}/atlas.png";
            await WriteAssetAsync(projectRoot, atlasImagePath, content.AtlasPng, cancellationToken).ConfigureAwait(false);
            var spriteAssets = new List<EyecatchSpriteAsset>(content.Document.Sprites.Count);
            foreach (var sprite in content.Document.Sprites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = SpriteAssetFileName(sprite.Name);
                var imagePath = $"{variantRoot}/sprites/{fileName}";
                var png = SpriteAtlasImage.Crop(content.AtlasPng, content.Document.TextureWidth, content.Document.TextureHeight, sprite);
                await WriteAssetAsync(projectRoot, imagePath, png, cancellationToken).ConfigureAwait(false);
                spriteAssets.Add(new EyecatchSpriteAsset { Name = sprite.Name, ImagePath = imagePath });
            }

            variant.Document = content.Document;
            variant.AtlasImagePath = atlasImagePath;
            variant.SpriteAssets = spriteAssets;
            variant.SourceChecksum = sourceChecksum;
            variant.LastBuiltChecksum = string.Empty;
            variant.IsDirty = false;
            variant.Status = EyecatchVariantStatus.Ready;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            variant.Status = EyecatchVariantStatus.Error;
            variant.Error = exception.Message;
        }
        finally
        {
            if (unpackedPath is not null && File.Exists(unpackedPath)) File.Delete(unpackedPath);
        }
    }

    private static bool HasPlatformLayoutMismatch(EyecatchAtlasFamily family)
    {
        var android = family.Variants.FirstOrDefault(variant => variant.Kind == SpriteAtlasKind.NGUI3 && variant.Platform == "android");
        var ios = family.Variants.FirstOrDefault(variant => variant.Kind == SpriteAtlasKind.NGUI3 && variant.Platform == "ios");
        return android?.Status == EyecatchVariantStatus.Ready && ios?.Status == EyecatchVariantStatus.Ready
            && !DocumentsHaveSameLayout(android.Document, ios.Document);
    }

    private static bool HasSpriteNameMismatch(EyecatchAtlasFamily family)
    {
        HashSet<string>? referenceNames = null;
        foreach (var variant in family.Variants.Where(item => item.Kind == SpriteAtlasKind.NGUI3
            && item.Status == EyecatchVariantStatus.Ready))
        {
            var names = variant.Document.Sprites.Select(sprite => sprite.Name).ToHashSet(StringComparer.Ordinal);
            if (referenceNames is not null && !referenceNames.SetEquals(names)) return true;
            referenceNames ??= names;
        }
        return false;
    }

    private static bool DocumentsHaveSameLayout(SpriteAtlasDocument left, SpriteAtlasDocument right)
    {
        if (left.TextureWidth != right.TextureWidth || left.TextureHeight != right.TextureHeight
            || left.Sprites.Count != right.Sprites.Count) return false;
        for (var index = 0; index < left.Sprites.Count; index++)
        {
            var a = left.Sprites[index];
            var b = right.Sprites[index];
            if (!a.Name.Equals(b.Name, StringComparison.Ordinal)
                || a.X != b.X || a.Y != b.Y || a.Width != b.Width || a.Height != b.Height
                || a.InnerX != b.InnerX || a.InnerY != b.InnerY
                || a.InnerWidth != b.InnerWidth || a.InnerHeight != b.InnerHeight)
                return false;
        }
        return true;
    }

    private static string? ResolveSourcePath(string projectRoot, string resourcePath, string platform)
    {
        var platformRoot = Path.Combine(projectRoot, "resources", platform);
        var path = Path.GetFullPath(Path.Combine(platformRoot, resourcePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = platformRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Atlas resource path is outside the platform resource directory.");
        if (File.Exists(path)) return path;
        if (File.Exists(path + ".lz4")) return path + ".lz4";
        return null;
    }

    private static bool TryParseAtlasName(string path, out string key, out string atlasName, out SpriteAtlasKind kind)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(".lz4", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^4];
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".unity", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".unity3d", StringComparison.OrdinalIgnoreCase))
        {
            key = atlasName = string.Empty;
            kind = default;
            return false;
        }

        atlasName = Path.GetFileNameWithoutExtension(fileName);
        var prefix = atlasName.StartsWith("d3_", StringComparison.OrdinalIgnoreCase) ? "d3_"
            : atlasName.StartsWith("d_", StringComparison.OrdinalIgnoreCase) ? "d_"
            : string.Empty;
        key = prefix.Length == 0 ? string.Empty : atlasName[prefix.Length..];
        kind = prefix == "d3_" ? SpriteAtlasKind.NGUI3 : SpriteAtlasKind.NGUI2;
        return key.Length > 1 && key[0] == 'e' && key[1..].All(char.IsDigit);
    }

    private static string SpriteAssetFileName(string spriteName)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(spriteName))).ToLowerInvariant()[..12];
        return hash + ".png";
    }

    private static (string AtlasKey, string SpriteName) ParseSpriteName(string spriteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteName);
        var separator = spriteName.IndexOf('_');
        if (separator < 2 || spriteName[0] != 'e' || !spriteName.AsSpan(1, separator - 1).ToString().All(char.IsDigit))
            throw new InvalidDataException($"Sprite name '{spriteName}' must use the e<number>_name format.");
        return (spriteName[..separator], spriteName);
    }

    private static (string AtlasKey, SpriteAtlasKind Kind) ParseAtlasName(string atlasName)
    {
        if (atlasName.StartsWith("d3_e", StringComparison.OrdinalIgnoreCase))
            return (atlasName[3..], SpriteAtlasKind.NGUI3);
        if (atlasName.StartsWith("d_e", StringComparison.OrdinalIgnoreCase))
            return (atlasName[2..], SpriteAtlasKind.NGUI2);
        throw new InvalidDataException($"Unsupported eyecatch atlas name '{atlasName}'.");
    }

    private static string ResolveProjectPath(string projectRoot, string relativePath)
    {
        var normalized = FileUtility.NormalizePackageRelativePath(relativePath);
        var root = Path.GetFullPath(projectRoot);
        var path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Indexed asset path is outside the project directory.");
        return path;
    }

    private static async Task WriteAssetAsync(string projectRoot, string relativePath, byte[] content, CancellationToken cancellationToken)
    {
        var destination = ResolveProjectPath(projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tempPath = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private sealed record AtlasCandidate(
        string AtlasKey,
        string AtlasName,
        SpriteAtlasKind Kind,
        string ResourcePath,
        ResourceFile? Resource,
        bool Registered);
}
