using Assets.Lib.Models;
using Assets.Lib.Services;
using AssetsTools.NET.Texture;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;

namespace DMTQ_Tools.Components.Services;

/// <summary>Builds all four Android/iOS NGUI2/NGUI3 bundles for an indexed atlas family.</summary>
public sealed class EyecatchBundleBuildService(
    EyecatchIndexService indexService,
    SpriteAtlasBundleService bundleService,
    IAtlasDummyTemplateProvider templateProvider,
    IProjectWorkflow workflow)
{
    private static readonly string[] Platforms = ["android", "ios"];

    /// <summary>Creates a new indexed atlas family and emits its four platform/schema bundles.</summary>
    public async Task<EyecatchProjectIndex> CreateFamilyAsync(
        PatchPackage package,
        string projectRoot,
        SpriteAtlasDocument sourceDocument,
        byte[] atlasPng,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        sourceDocument.Validate();
        var root = Path.GetFullPath(projectRoot);
        var atlasKey = sourceDocument.AtlasName.StartsWith("d3_e", StringComparison.OrdinalIgnoreCase)
            ? sourceDocument.AtlasName[3..]
            : sourceDocument.AtlasName.StartsWith("d_e", StringComparison.OrdinalIgnoreCase)
                ? sourceDocument.AtlasName[2..]
                : throw new InvalidDataException("New atlas names must use d_e<number> or d3_e<number>.");
        if (atlasKey.Length < 2 || atlasKey[0] != 'e' || !atlasKey[1..].All(char.IsDigit))
            throw new InvalidDataException("New atlas names must use d_e<number> or d3_e<number>.");

        var index = await indexService.LoadAsync(root, cancellationToken).ConfigureAwait(false);
        if (index.Atlases.Any(item => item.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Atlas family '{atlasKey}' already exists in the project index.");

        var documents = new Dictionary<SpriteAtlasKind, SpriteAtlasDocument>();
        foreach (var kind in Enum.GetValues<SpriteAtlasKind>())
        {
            var document = sourceDocument.Clone();
            document.Kind = kind;
            document.AtlasName = (kind == SpriteAtlasKind.NGUI3 ? "d3_" : "d_") + atlasKey;
            document.Validate();
            documents.Add(kind, document);
            var existingResource = package.Resources.FirstOrDefault(resource =>
                resource.Category.Equals("dlc", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(resource.FileName).Equals(document.AtlasName, StringComparison.OrdinalIgnoreCase)
                && (Path.GetExtension(resource.FileName).Equals(".unity", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(resource.FileName).Equals(".unity3d", StringComparison.OrdinalIgnoreCase)));
            if (existingResource is not null)
                throw new InvalidOperationException($"Atlas resource '{existingResource.FileName}' already exists in the project.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"dmtq-eyecatch-create-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var replacements = new List<ResourceReplacement>(4);
        var generated = new List<(SpriteAtlasKind Kind, string Platform, string ResourcePath, SpriteAtlasDocument Document, byte[] AtlasPng)>();
        try
        {
            foreach (var kind in Enum.GetValues<SpriteAtlasKind>())
            foreach (var platform in Platforms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = documents[kind];
                var templatePath = await templateProvider.GetTemplatePathAsync(kind.ToString(), platform, cancellationToken)
                    .ConfigureAwait(false);
                var outputPath = Path.Combine(tempRoot, $"{document.AtlasName}-{platform}.unity3d");
                var targetPlatform = platform == "android" ? UnityTargetPlatform.Android : UnityTargetPlatform.IOS;
                var textureFormat = platform == "android" ? TextureFormat.ETC2_RGBA8 : TextureFormat.ASTC_RGBA_6x6;
                bundleService.Export(new UnityBundleTemplate { Platform = targetPlatform, BundlePath = templatePath },
                    document, atlasPng, outputPath, textureFormat);
                var readback = await Task.Run(() => bundleService.Read(outputPath, kind), cancellationToken).ConfigureAwait(false);
                if (!SameSpriteNames(document, readback.Document))
                    throw new InvalidDataException($"Generated {document.AtlasName} bundle failed sprite read-back validation.");

                var resourcePath = $"dlc/{document.AtlasName}.unity3d";
                replacements.Add(new ResourceReplacement(outputPath, resourcePath, platform, Compressed: true));
                generated.Add((kind, platform, resourcePath, readback.Document, readback.AtlasPng));
                progress?.Report($"Built {document.AtlasName} for {platform}.");
            }

            await workflow.AddOrReplaceResourcesAsync(replacements, cancellationToken).ConfigureAwait(false);
            var family = new EyecatchAtlasFamily { AtlasKey = atlasKey, HasPlatformLayoutMismatch = false };
            foreach (var item in generated)
            {
                var variant = new EyecatchAtlasVariant
                {
                    AtlasKey = atlasKey,
                    Kind = item.Kind,
                    Platform = item.Platform,
                    AtlasName = item.Document.AtlasName,
                    ResourcePath = item.ResourcePath,
                    ResourceRegistered = true,
                    Exists = true,
                    Status = EyecatchVariantStatus.Ready
                };
                await indexService.RefreshBuiltVariantAsync(root, variant, item.ResourcePath, false,
                    item.Document, item.AtlasPng, cancellationToken).ConfigureAwait(false);
                family.Variants.Add(variant);
            }
            index.Atlases.Add(family);
            index.Atlases = index.Atlases.OrderBy(item => item.AtlasKey, StringComparer.OrdinalIgnoreCase).ToList();
            await indexService.SaveIndexAsync(root, index, cancellationToken).ConfigureAwait(false);
            return index;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    /// <summary>Applies the editor's current atlas content to every NGUI and platform variant in a family.</summary>
    public async Task<EyecatchProjectIndex> ApplyDocumentToFamilyAsync(
        PatchPackage package,
        string projectRoot,
        string atlasKey,
        SpriteAtlasDocument sourceDocument,
        byte[] atlasPng,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentNullException.ThrowIfNull(atlasPng);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(atlasKey);
        sourceDocument.Validate();

        var root = Path.GetFullPath(projectRoot);
        var index = await indexService.LoadAsync(root, cancellationToken).ConfigureAwait(false);
        var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase));
        if (family is null)
        {
            index = await indexService.BuildAsync(package, root, cancellationToken: cancellationToken).ConfigureAwait(false);
            family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase));
        }
        if (family is null)
            throw new FileNotFoundException($"Atlas family '{atlasKey}' was not found in the project.");
        if (HasSpriteNameMismatch(family))
            throw new InvalidOperationException("Atlas variants contain different sprite names. Batch overwrite is blocked to protect platform-specific sprites.");
        if (family.Variants.Any(variant => variant.Status == EyecatchVariantStatus.SourceChanged))
            throw new InvalidOperationException("An atlas source changed since it was indexed. Re-index and resolve the conflict before saving all variants.");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"dmtq-eyecatch-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var replacements = new List<ResourceReplacement>(4);
        var pending = new List<(EyecatchAtlasVariant Variant, string ResourcePath, bool IsInstallPack, SpriteAtlasDocument Document, byte[] AtlasPng)>(4);
        try
        {
            foreach (var kind in Enum.GetValues<SpriteAtlasKind>())
            foreach (var platform in Platforms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variant = family.Variants.FirstOrDefault(item => item.Kind == kind
                    && item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
                if (variant is null)
                {
                    variant = new EyecatchAtlasVariant
                    {
                        AtlasKey = atlasKey,
                        Kind = kind,
                        Platform = platform,
                        AtlasName = (kind == SpriteAtlasKind.NGUI3 ? "d3_" : "d_") + atlasKey,
                        Status = EyecatchVariantStatus.Missing
                    };
                    family.Variants.Add(variant);
                }

                var document = sourceDocument.Clone();
                document.Kind = kind;
                document.AtlasName = (kind == SpriteAtlasKind.NGUI3 ? "d3_" : "d_") + atlasKey;
                document.Validate();

                var templatePath = await templateProvider.GetTemplatePathAsync(kind.ToString(), platform, cancellationToken)
                    .ConfigureAwait(false);
                var targetPlatform = platform == "android" ? UnityTargetPlatform.Android : UnityTargetPlatform.IOS;
                var textureFormat = platform == "android" ? TextureFormat.ETC2_RGBA8 : TextureFormat.ASTC_RGBA_6x6;
                var outputPath = Path.Combine(tempRoot, $"{document.AtlasName}-{platform}.unity3d");
                bundleService.Export(new UnityBundleTemplate { Platform = targetPlatform, BundlePath = templatePath },
                    document, atlasPng, outputPath, textureFormat);
                var readback = await Task.Run(() => bundleService.Read(outputPath, kind), cancellationToken).ConfigureAwait(false);
                if (!SameSpriteNames(document, readback.Document))
                    throw new InvalidDataException($"Generated {document.AtlasName} bundle failed sprite read-back validation.");

                var resourcePath = string.IsNullOrWhiteSpace(variant.ResourcePath)
                    ? $"dlc/{document.AtlasName}.unity3d"
                    : FileUtility.NormalizePackageRelativePath(variant.ResourcePath);
                var resource = package.Resources.FirstOrDefault(item => item.FileName.Equals(resourcePath, StringComparison.OrdinalIgnoreCase));
                var compressed = resource?.Compressed ?? true;
                var isInstallPack = resource?.PlatformManifest.FirstOrDefault(entry =>
                    entry.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))?.IsInstallPack ?? variant.IsInstallPack;
                replacements.Add(new ResourceReplacement(outputPath, resourcePath, platform, compressed));
                pending.Add((variant, resourcePath, isInstallPack, readback.Document, readback.AtlasPng));
                progress?.Report($"Built {document.AtlasName} for {platform}.");
            }

            await workflow.AddOrReplaceResourcesAsync(replacements, cancellationToken).ConfigureAwait(false);
            foreach (var item in pending)
                await indexService.RefreshBuiltVariantAsync(root, item.Variant, item.ResourcePath, item.IsInstallPack,
                    item.Document, item.AtlasPng, cancellationToken).ConfigureAwait(false);

            family.HasPlatformLayoutMismatch = HasPlatformLayoutMismatch(family);
            family.HasSpriteNameMismatch = HasSpriteNameMismatch(family);
            await indexService.SaveIndexAsync(root, index, cancellationToken).ConfigureAwait(false);
            return index;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    /// <summary>Builds and transactionally updates all four platform bundle variants for one atlas family.</summary>
    public async Task<EyecatchProjectIndex> BuildFamilyAsync(
        PatchPackage package,
        string projectRoot,
        string atlasKey,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(atlasKey);

        var root = Path.GetFullPath(projectRoot);
        var index = await indexService.LoadAsync(root, cancellationToken).ConfigureAwait(false);
        var family = index.Atlases.FirstOrDefault(atlas => atlas.AtlasKey.Equals(atlasKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Atlas '{atlasKey}' is not indexed.");
        var hasSpriteNameMismatch = HasSpriteNameMismatch(family);
        if (hasSpriteNameMismatch)
            throw new InvalidOperationException("Android and iOS atlas variants contain different sprite names. Batch overwrite is blocked to protect platform-specific sprites.");
        if (family.Variants.Any(variant => variant.Status == EyecatchVariantStatus.SourceChanged))
            throw new InvalidOperationException("A source bundle changed while local edits are pending. Re-index and resolve the conflict first.");

        var documents = new Dictionary<SpriteAtlasKind, EyecatchAtlasVariant>();
        var layoutMismatchKinds = Enum.GetValues<SpriteAtlasKind>()
            .Where(kind => HasPlatformLayoutMismatch(family, kind))
            .ToHashSet();
        foreach (var kind in new[] { SpriteAtlasKind.NGUI2, SpriteAtlasKind.NGUI3 })
        {
            var baseVariant = family.Variants
                .Where(variant => variant.Kind == kind && variant.Status == EyecatchVariantStatus.Ready && variant.Document is not null)
                .OrderBy(variant => variant.Platform == "android" ? 0 : 1)
                .FirstOrDefault();
            if (baseVariant is null)
                throw new InvalidOperationException($"No readable {kind} atlas exists for '{atlasKey}'.");
            documents[kind] = baseVariant;

        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"dmtq-eyecatch-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var replacements = new List<ResourceReplacement>(4);
        var pendingVariants = new List<(EyecatchAtlasVariant Variant, string ResourcePath, bool IsInstallPack, SpriteAtlasDocument Document, byte[] AtlasPng)>();
        try
        {
            foreach (var kind in new[] { SpriteAtlasKind.NGUI2, SpriteAtlasKind.NGUI3 })
            foreach (var platform in Platforms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variant = family.Variants.FirstOrDefault(item => item.Kind == kind
                    && item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Index has no {kind} {platform} slot for '{atlasKey}'.");
                var sourceVariant = !layoutMismatchKinds.Contains(kind) && variant.Status == EyecatchVariantStatus.Ready
                    && !string.IsNullOrWhiteSpace(variant.AtlasImagePath)
                    ? variant
                    : documents[kind];
                var document = sourceVariant.Document.Clone();
                var atlasPng = await indexService.ReadImageBytesAsync(root, sourceVariant.AtlasImagePath, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new FileNotFoundException($"Indexed atlas PNG is missing for {sourceVariant.AtlasName}.");
                var templatePath = await templateProvider.GetTemplatePathAsync(kind.ToString(), platform, cancellationToken)
                    .ConfigureAwait(false);
                var outputPath = Path.Combine(tempRoot, $"{document.AtlasName}-{platform}.unity3d");
                var targetPlatform = platform == "android" ? UnityTargetPlatform.Android : UnityTargetPlatform.IOS;
                bundleService.Export(new UnityBundleTemplate { Platform = targetPlatform, BundlePath = templatePath },
                    document, atlasPng, outputPath, platform == "android" ? TextureFormat.ETC2_RGBA8 : TextureFormat.ASTC_RGBA_6x6);

                var readback = await Task.Run(() => bundleService.Read(outputPath, kind), cancellationToken).ConfigureAwait(false);
                if (!SameSpriteNames(document, readback.Document))
                    throw new InvalidDataException($"Generated {document.AtlasName} bundle failed sprite read-back validation.");

                var resourcePath = string.IsNullOrWhiteSpace(variant.ResourcePath)
                    ? $"dlc/{document.AtlasName}.unity3d"
                    : FileUtility.NormalizePackageRelativePath(variant.ResourcePath);
                var resource = package.Resources.FirstOrDefault(item => item.FileName.Equals(resourcePath, StringComparison.OrdinalIgnoreCase));
                var compressed = resource?.Compressed ?? false;
                var isInstallPack = resource?.PlatformManifest.FirstOrDefault(entry =>
                    entry.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))?.IsInstallPack ?? variant.IsInstallPack;
                replacements.Add(new ResourceReplacement(outputPath, resourcePath, platform, compressed));
                pendingVariants.Add((variant, resourcePath, isInstallPack, readback.Document, atlasPng));
                progress?.Report($"Built {document.AtlasName} for {platform}.");
            }

            await workflow.AddOrReplaceResourcesAsync(replacements, cancellationToken).ConfigureAwait(false);
            foreach (var pending in pendingVariants)
            {
                await indexService.RefreshBuiltVariantAsync(root, pending.Variant, pending.ResourcePath, pending.IsInstallPack,
                    pending.Document, pending.AtlasPng, cancellationToken).ConfigureAwait(false);
            }
            family.HasPlatformLayoutMismatch = HasPlatformLayoutMismatch(family);
            family.HasSpriteNameMismatch = HasSpriteNameMismatch(family);
            await indexService.SaveIndexAsync(root, index, cancellationToken).ConfigureAwait(false);
            return index;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static bool SameSpriteNames(SpriteAtlasDocument expected, SpriteAtlasDocument actual)
        => expected.Kind == actual.Kind
            && expected.TextureWidth == actual.TextureWidth
            && expected.TextureHeight == actual.TextureHeight
            && expected.Sprites.Select(sprite => sprite.Name).SequenceEqual(actual.Sprites.Select(sprite => sprite.Name), StringComparer.Ordinal);

    private static bool HasSpriteNameMismatch(EyecatchAtlasFamily family)
    {
        HashSet<string>? referenceNames = null;
        foreach (var variant in family.Variants.Where(item => item.Status == EyecatchVariantStatus.Ready))
        {
            var names = variant.Document.Sprites.Select(sprite => sprite.Name).ToHashSet(StringComparer.Ordinal);
            if (referenceNames is not null && !referenceNames.SetEquals(names)) return true;
            referenceNames ??= names;
        }
        return false;
    }

    private static bool HasPlatformLayoutMismatch(EyecatchAtlasFamily family)
        => Enum.GetValues<SpriteAtlasKind>().Any(kind => HasPlatformLayoutMismatch(family, kind));

    private static bool HasPlatformLayoutMismatch(EyecatchAtlasFamily family, SpriteAtlasKind kind)
    {
        var android = family.Variants.FirstOrDefault(item => item.Kind == kind && item.Platform.Equals("android", StringComparison.OrdinalIgnoreCase));
        var ios = family.Variants.FirstOrDefault(item => item.Kind == kind && item.Platform.Equals("ios", StringComparison.OrdinalIgnoreCase));
        return android?.Status == EyecatchVariantStatus.Ready && ios?.Status == EyecatchVariantStatus.Ready
            && !SameLayout(android.Document, ios.Document);
    }

    private static bool SameLayout(SpriteAtlasDocument left, SpriteAtlasDocument right)
    {
        if (left.TextureWidth != right.TextureWidth || left.TextureHeight != right.TextureHeight
            || left.Sprites.Count != right.Sprites.Count) return false;
        return left.Sprites.Zip(right.Sprites).All(pair => pair.First.Name == pair.Second.Name
            && pair.First.X == pair.Second.X && pair.First.Y == pair.Second.Y
            && pair.First.Width == pair.Second.Width && pair.First.Height == pair.Second.Height
            && pair.First.InnerX == pair.Second.InnerX && pair.First.InnerY == pair.Second.InnerY
            && pair.First.InnerWidth == pair.Second.InnerWidth && pair.First.InnerHeight == pair.Second.InnerHeight
            && pair.First.PaddingLeft == pair.Second.PaddingLeft && pair.First.PaddingRight == pair.Second.PaddingRight
            && pair.First.PaddingTop == pair.Second.PaddingTop && pair.First.PaddingBottom == pair.Second.PaddingBottom);
    }
}
