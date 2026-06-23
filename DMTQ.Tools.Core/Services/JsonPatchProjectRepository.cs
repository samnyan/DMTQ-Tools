using System.Text.Json;
using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class JsonPatchProjectRepository : IPatchProjectRepository
{
    private const int CurrentSchemaVersion = 1;
    private const string ProjectFileName = "project.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveAsync(
        PatchPackage package,
        string exportCompressionMode,
        PackageExportOptions exportOptions,
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportCompressionMode);
        ArgumentNullException.ThrowIfNull(exportOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        Directory.CreateDirectory(projectRoot);
        var document = ProjectDocument.FromPackage(package, exportCompressionMode, exportOptions);
        var jsonPath = Path.Combine(projectRoot, ProjectFileName);
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PatchProjectSnapshot> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var jsonPath = Path.Combine(projectRoot, ProjectFileName);
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException("Could not find GameTableManager project file.", jsonPath);
        }

        await using var stream = File.OpenRead(jsonPath);
        var document = await JsonSerializer.DeserializeAsync<ProjectDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            throw new InvalidDataException("GameTableManager project file is empty or invalid.");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported GameTableManager project schema version {document.SchemaVersion}.");
        }

        return document.ToSnapshot(projectRoot);
    }

    private sealed class ProjectDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string ExportCompressionMode { get; set; } = "Keep";
        public Dictionary<string, bool> CompressionOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public ProjectInfoDto ProjectInfo { get; set; } = new();
        public List<PatchFileEntryDto> ManifestEntries { get; set; } = [];
        public List<GameTableDto> Tables { get; set; } = [];
        public List<ResourceFileDto> Resources { get; set; } = [];
        public List<PlatformPackageRecordDto> Platforms { get; set; } = [];
        public List<SongDto> Songs { get; set; } = [];
        public List<AchievementDto> Achievements { get; set; } = [];
        public List<QuestDto> Quests { get; set; } = [];
        public List<ProductDto> Products { get; set; } = [];
        public List<ItemDto> Items { get; set; } = [];
        public List<IngameItemDto> IngameItems { get; set; } = [];
        public List<IngameItemEffectDto> IngameItemEffects { get; set; } = [];

        public static ProjectDocument FromPackage(
            PatchPackage package,
            string exportCompressionMode,
            PackageExportOptions exportOptions)
        {
            return new ProjectDocument
            {
                ExportCompressionMode = exportCompressionMode,
                CompressionOverrides = new Dictionary<string, bool>(exportOptions.CompressionOverrides, StringComparer.OrdinalIgnoreCase),
                ProjectInfo = ProjectInfoDto.FromModel(package.ProjectInfo),
                ManifestEntries = package.Manifest.Entries.Select(PatchFileEntryDto.FromModel).ToList(),
                Tables = package.Tables.Tables.Select(GameTableDto.FromModel).ToList(),
                Resources = package.Resources.Select(ResourceFileDto.FromModel).ToList(),
                Platforms = package.Platforms.Select(PlatformPackageRecordDto.FromModel).ToList(),
                Songs = package.Songs.Select(SongDto.FromModel).ToList(),
                Achievements = package.Achievements.Select(AchievementDto.FromModel).ToList(),
                Quests = package.Quests.Select(QuestDto.FromModel).ToList(),
                Products = package.Products.Select(ProductDto.FromModel).ToList(),
                Items = package.Items.Select(ItemDto.FromModel).ToList(),
                IngameItems = package.IngameItems.Select(IngameItemDto.FromModel).ToList(),
                IngameItemEffects = package.IngameItemEffects.Select(IngameItemEffectDto.FromModel).ToList()
            };
        }

        public PatchProjectSnapshot ToSnapshot(string projectRoot)
        {
            var package = new PatchPackage
            {
                ProjectInfo = ProjectInfo.ToModel(projectRoot)
            };

            package.Manifest.Entries.AddRange(ManifestEntries.Select(entry => entry.ToModel()));
            package.Tables.Tables.AddRange(Tables.Select(table => table.ToModel()));
            package.Resources.AddRange(Resources.Select(resource => resource.ToModel()));
            package.Platforms.AddRange(Platforms.Select(platform => platform.ToModel()));
            package.Songs.AddRange(Songs.Select(song => song.ToModel()));
            package.Achievements.AddRange(Achievements.Select(a => a.ToModel()));
            package.Quests.AddRange(Quests.Select(q => q.ToModel()));
            package.Products.AddRange(Products.Select(p => p.ToModel()));
            package.Items.AddRange(Items.Select(i => i.ToModel()));
            package.IngameItems.AddRange(IngameItems.Select(i => i.ToModel()));
            package.IngameItemEffects.AddRange(IngameItemEffects.Select(e => e.ToModel()));

            var options = new PackageExportOptions();
            foreach (var item in CompressionOverrides)
            {
                options.SetCompression(item.Key, item.Value);
            }

            return new PatchProjectSnapshot(package, ExportCompressionMode, options);
        }
    }

    private sealed class SongDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string OriginalBgaYn { get; set; } = string.Empty;
        public string LoopBgaYn { get; set; } = string.Empty;
        public string ComposedBy { get; set; } = string.Empty;
        public string Singer { get; set; } = string.Empty;
        public string FeatBy { get; set; } = string.Empty;
        public string ArrangedBy { get; set; } = string.Empty;
        public string VisualizedBy { get; set; } = string.Empty;
        public string CostGamePoint { get; set; } = string.Empty;
        public string CostGameCash { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FreeYn { get; set; } = string.Empty;
        public string HiddenYn { get; set; } = string.Empty;
        public string OpenYn { get; set; } = string.Empty;
        public string TrackId { get; set; } = string.Empty;
        public string ModDate { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;
        public string? PreviewPackageRelativePath { get; set; }
        public Dictionary<string, string> TitlesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ItemNamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ProductIds { get; set; } = [];
        public List<string> ItemIds { get; set; } = [];
        public List<string> CategoryIds { get; set; } = [];
        public Dictionary<string, SongLocalizationDto> Localizations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SongPatternDto> Patterns { get; set; } = [];

        public static SongDto FromModel(Song model)
            => new()
            {
                Id = model.Id,
                ItemId = model.ItemId,
                Name = model.Name,
                FullName = model.FullName,
                Genre = model.Genre,
                ArtistName = model.ArtistName,
                OriginalBgaYn = model.OriginalBgaYn,
                LoopBgaYn = model.LoopBgaYn,
                ComposedBy = model.ComposedBy,
                Singer = model.Singer,
                FeatBy = model.FeatBy,
                ArrangedBy = model.ArrangedBy,
                VisualizedBy = model.VisualizedBy,
                CostGamePoint = model.CostGamePoint,
                CostGameCash = model.CostGameCash,
                Flag = model.Flag,
                Status = model.Status,
                FreeYn = model.FreeYn,
                HiddenYn = model.HiddenYn,
                OpenYn = model.OpenYn,
                TrackId = model.TrackId,
                ModDate = model.ModDate,
                Update = model.Update,
                PreviewPackageRelativePath = model.PreviewPackageRelativePath,
                TitlesByLanguage = new Dictionary<string, string>(model.TitlesByLanguage, StringComparer.OrdinalIgnoreCase),
                DescriptionsByLanguage = new Dictionary<string, string>(model.DescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
                ItemNamesByLanguage = new Dictionary<string, string>(model.ItemNamesByLanguage, StringComparer.OrdinalIgnoreCase),
                ProductIds = [..model.ProductIds],
                ItemIds = [..model.ItemIds],
                CategoryIds = [..model.CategoryIds],
                Localizations = model.Localizations.ToDictionary(
                    kvp => kvp.Key,
                    kvp => SongLocalizationDto.FromModel(kvp.Value),
                    StringComparer.OrdinalIgnoreCase),
                Patterns = model.Patterns.Select(SongPatternDto.FromModel).ToList()
            };

        public Song ToModel()
        {
            var song = new Song { Id = Id };
            song.ItemId = ItemId;
            song.Name = Name;
            song.FullName = FullName;
            song.Genre = Genre;
            song.ArtistName = ArtistName;
            song.OriginalBgaYn = OriginalBgaYn;
            song.LoopBgaYn = LoopBgaYn;
            song.ComposedBy = ComposedBy;
            song.Singer = Singer;
            song.FeatBy = FeatBy;
            song.ArrangedBy = ArrangedBy;
            song.VisualizedBy = VisualizedBy;
            song.CostGamePoint = CostGamePoint;
            song.CostGameCash = CostGameCash;
            song.Flag = Flag;
            song.Status = Status;
            song.FreeYn = FreeYn;
            song.HiddenYn = HiddenYn;
            song.OpenYn = OpenYn;
            song.TrackId = TrackId;
            song.ModDate = ModDate;
            song.Update = Update;
            song.PreviewPackageRelativePath = PreviewPackageRelativePath;
            foreach (var kvp in TitlesByLanguage) song.TitlesByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in DescriptionsByLanguage) song.DescriptionsByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in ItemNamesByLanguage) song.ItemNamesByLanguage[kvp.Key] = kvp.Value;
            song.ProductIds.AddRange(ProductIds);
            song.ItemIds.AddRange(ItemIds);
            song.CategoryIds.AddRange(CategoryIds);
            foreach (var kvp in Localizations) song.Localizations[kvp.Key] = kvp.Value.ToModel();
            song.Patterns.AddRange(Patterns.Select(p => p.ToModel()));
            return song;
        }
    }

    private sealed class SongPatternDto
    {
        public string PatternId { get; set; } = string.Empty;
        public string SongId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string PointType { get; set; } = string.Empty;
        public string PointValue { get; set; } = string.Empty;
        public string Flg { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;

        public static SongPatternDto FromModel(SongPattern model)
            => new()
            {
                PatternId = model.PatternId,
                SongId = model.SongId,
                Name = model.Name,
                Line = model.Line,
                Signature = model.Signature,
                Difficulty = model.Difficulty,
                Level = model.Level,
                PointType = model.PointType,
                PointValue = model.PointValue,
                Flg = model.Flg,
                Update = model.Update
            };

        public SongPattern ToModel()
            => new()
            {
                PatternId = PatternId,
                SongId = SongId,
                Name = Name,
                Line = Line,
                Signature = Signature,
                Difficulty = Difficulty,
                Level = Level,
                PointType = PointType,
                PointValue = PointValue,
                Flg = Flg,
                Update = Update
            };
    }

    private sealed class SongLocalizationDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string ComposedBy { get; set; } = string.Empty;
        public string Singer { get; set; } = string.Empty;
        public string FeatBy { get; set; } = string.Empty;
        public string ArrangedBy { get; set; } = string.Empty;
        public string VisualizedBy { get; set; } = string.Empty;

        public static SongLocalizationDto FromModel(SongLocalization model)
            => new()
            {
                FullName = model.FullName,
                Genre = model.Genre,
                ArtistName = model.ArtistName,
                ComposedBy = model.ComposedBy,
                Singer = model.Singer,
                FeatBy = model.FeatBy,
                ArrangedBy = model.ArrangedBy,
                VisualizedBy = model.VisualizedBy
            };

        public SongLocalization ToModel()
            => new()
            {
                FullName = FullName,
                Genre = Genre,
                ArtistName = ArtistName,
                ComposedBy = ComposedBy,
                Singer = Singer,
                FeatBy = FeatBy,
                ArrangedBy = ArrangedBy,
                VisualizedBy = VisualizedBy
            };
    }

    private sealed class AchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string ConditionType { get; set; } = string.Empty;
        public string ConditionValue { get; set; } = string.Empty;
        public string ConditionCount { get; set; } = string.Empty;
        public string ConditionSpecial { get; set; } = string.Empty;
        public string ImgUrl { get; set; } = string.Empty;
        public string AchievementTier { get; set; } = string.Empty;
        public string ObtainPoint { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PreDescription { get; set; } = string.Empty;
        public string AfterDescription { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;
        public Dictionary<string, string> NamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> PreDescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AfterDescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static AchievementDto FromModel(Achievement model)
            => new()
            {
                Id = model.Id,
                ConditionType = model.ConditionType,
                ConditionValue = model.ConditionValue,
                ConditionCount = model.ConditionCount,
                ConditionSpecial = model.ConditionSpecial,
                ImgUrl = model.ImgUrl,
                AchievementTier = model.AchievementTier,
                ObtainPoint = model.ObtainPoint,
                Name = model.Name,
                PreDescription = model.PreDescription,
                AfterDescription = model.AfterDescription,
                Update = model.Update,
                NamesByLanguage = new Dictionary<string, string>(model.NamesByLanguage, StringComparer.OrdinalIgnoreCase),
                PreDescriptionsByLanguage = new Dictionary<string, string>(model.PreDescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
                AfterDescriptionsByLanguage = new Dictionary<string, string>(model.AfterDescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
            };

        public Achievement ToModel()
        {
            var achievement = new Achievement { Id = Id };
            achievement.ConditionType = ConditionType;
            achievement.ConditionValue = ConditionValue;
            achievement.ConditionCount = ConditionCount;
            achievement.ConditionSpecial = ConditionSpecial;
            achievement.ImgUrl = ImgUrl;
            achievement.AchievementTier = AchievementTier;
            achievement.ObtainPoint = ObtainPoint;
            achievement.Name = Name;
            achievement.PreDescription = PreDescription;
            achievement.AfterDescription = AfterDescription;
            achievement.Update = Update;
            foreach (var kvp in NamesByLanguage) achievement.NamesByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in PreDescriptionsByLanguage) achievement.PreDescriptionsByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in AfterDescriptionsByLanguage) achievement.AfterDescriptionsByLanguage[kvp.Key] = kvp.Value;
            return achievement;
        }
    }

    private sealed class QuestDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> NamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<QuestMissionDto> Missions { get; set; } = [];

        public static QuestDto FromModel(Quest model)
            => new()
            {
                Id = model.Id,
                NamesByLanguage = new Dictionary<string, string>(model.NamesByLanguage, StringComparer.OrdinalIgnoreCase),
                DescriptionsByLanguage = new Dictionary<string, string>(model.DescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
                Missions = model.Missions.Select(QuestMissionDto.FromModel).ToList(),
            };

        public Quest ToModel()
        {
            var quest = new Quest { Id = Id };
            foreach (var kvp in NamesByLanguage) quest.NamesByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in DescriptionsByLanguage) quest.DescriptionsByLanguage[kvp.Key] = kvp.Value;
            quest.Missions.AddRange(Missions.Select(m => m.ToModel()));
            return quest;
        }
    }

    private sealed class QuestMissionDto
    {
        public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static QuestMissionDto FromModel(QuestMission model)
            => new()
            {
                DescriptionsByLanguage = new Dictionary<string, string>(model.DescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
            };

        public QuestMission ToModel()
        {
            var mission = new QuestMission();
            foreach (var kvp in DescriptionsByLanguage) mission.DescriptionsByLanguage[kvp.Key] = kvp.Value;
            return mission;
        }
    }

    private sealed class ProductDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string PlatformProductId { get; set; } = string.Empty;
        public string StoreProductId { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string CostGamePoint { get; set; } = string.Empty;
        public string CostGameCash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SaleStartDate { get; set; } = string.Empty;
        public string SaleEndDate { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;
        public List<string> CategoryIds { get; set; } = [];

        public static ProductDto FromModel(Product model)
            => new()
            {
                Id = model.Id,
                ItemId = model.ItemId,
                PlatformProductId = model.PlatformProductId,
                StoreProductId = model.StoreProductId,
                ProductType = model.ProductType,
                CostGamePoint = model.CostGamePoint,
                CostGameCash = model.CostGameCash,
                Status = model.Status,
                SaleStartDate = model.SaleStartDate,
                SaleEndDate = model.SaleEndDate,
                Update = model.Update,
                CategoryIds = [..model.CategoryIds],
            };

        public Product ToModel()
        {
            var product = new Product { Id = Id };
            product.ItemId = ItemId;
            product.PlatformProductId = PlatformProductId;
            product.StoreProductId = StoreProductId;
            product.ProductType = ProductType;
            product.CostGamePoint = CostGamePoint;
            product.CostGameCash = CostGameCash;
            product.Status = Status;
            product.SaleStartDate = SaleStartDate;
            product.SaleEndDate = SaleEndDate;
            product.Update = Update;
            product.CategoryIds.AddRange(CategoryIds);
            return product;
        }
    }

    private sealed class ItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ImgUrl1 { get; set; } = string.Empty;
        public string ImgUrl2 { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RepeatCount { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string LimitMinute { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BuyLevel { get; set; } = string.Empty;
        public string BuyLimitCount { get; set; } = string.Empty;
        public string BuyLimitType { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;
        public Dictionary<string, string> NamesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DescriptionsByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SummariesByLanguage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static ItemDto FromModel(Item model)
            => new()
            {
                Id = model.Id,
                ItemName = model.ItemName,
                ImgUrl1 = model.ImgUrl1,
                ImgUrl2 = model.ImgUrl2,
                Description = model.Description,
                RepeatCount = model.RepeatCount,
                ItemType = model.ItemType,
                LimitMinute = model.LimitMinute,
                Status = model.Status,
                BuyLevel = model.BuyLevel,
                BuyLimitCount = model.BuyLimitCount,
                BuyLimitType = model.BuyLimitType,
                Summary = model.Summary,
                Update = model.Update,
                NamesByLanguage = new Dictionary<string, string>(model.NamesByLanguage, StringComparer.OrdinalIgnoreCase),
                DescriptionsByLanguage = new Dictionary<string, string>(model.DescriptionsByLanguage, StringComparer.OrdinalIgnoreCase),
                SummariesByLanguage = new Dictionary<string, string>(model.SummariesByLanguage, StringComparer.OrdinalIgnoreCase),
            };

        public Item ToModel()
        {
            var item = new Item { Id = Id };
            item.ItemName = ItemName;
            item.ImgUrl1 = ImgUrl1;
            item.ImgUrl2 = ImgUrl2;
            item.Description = Description;
            item.RepeatCount = RepeatCount;
            item.ItemType = ItemType;
            item.LimitMinute = LimitMinute;
            item.Status = Status;
            item.BuyLevel = BuyLevel;
            item.BuyLimitCount = BuyLimitCount;
            item.BuyLimitType = BuyLimitType;
            item.Summary = Summary;
            item.Update = Update;
            foreach (var kvp in NamesByLanguage) item.NamesByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in DescriptionsByLanguage) item.DescriptionsByLanguage[kvp.Key] = kvp.Value;
            foreach (var kvp in SummariesByLanguage) item.SummariesByLanguage[kvp.Key] = kvp.Value;
            return item;
        }
    }

    private sealed class IngameItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string ItemLevel { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;

        public static IngameItemDto FromModel(IngameItem model)
            => new()
            {
                Id = model.Id,
                ItemType = model.ItemType,
                ItemLevel = model.ItemLevel,
                ProductId = model.ProductId,
                Update = model.Update,
            };

        public IngameItem ToModel()
            => new()
            {
                Id = Id,
                ItemType = ItemType,
                ItemLevel = ItemLevel,
                ProductId = ProductId,
                Update = Update,
            };
    }

    private sealed class IngameItemEffectDto
    {
        public string Id { get; set; } = string.Empty;
        public string EffectType { get; set; } = string.Empty;
        public string EffectPoint { get; set; } = string.Empty;
        public string EffectCount { get; set; } = string.Empty;
        public string EffectSpecial { get; set; } = string.Empty;
        public string Update { get; set; } = string.Empty;

        public static IngameItemEffectDto FromModel(IngameItemEffect model)
            => new()
            {
                Id = model.Id,
                EffectType = model.EffectType,
                EffectPoint = model.EffectPoint,
                EffectCount = model.EffectCount,
                EffectSpecial = model.EffectSpecial,
                Update = model.Update,
            };

        public IngameItemEffect ToModel()
            => new()
            {
                Id = Id,
                EffectType = EffectType,
                EffectPoint = EffectPoint,
                EffectCount = EffectCount,
                EffectSpecial = EffectSpecial,
                Update = Update,
            };
    }

    private sealed class ProjectInfoDto
    {
        public string? SourcePackageRoot { get; set; }
        public string? Version { get; set; }
        public string? Platform { get; set; }

        public static ProjectInfoDto FromModel(ProjectInfo model)
            => new()
            {
                SourcePackageRoot = model.SourcePackageRoot,
                Version = model.Version,
                Platform = model.Platform
            };

        public ProjectInfo ToModel(string projectRoot)
            => new(projectRoot, SourcePackageRoot, Version, Platform);
    }

    private sealed class PatchFileEntryDto
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public long CompressedFileSize { get; set; }
        public string CompressedChecksum { get; set; } = string.Empty;
        public int AcquireOnDemand { get; set; }
        public bool Compressed { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;

        public static PatchFileEntryDto FromModel(PatchFileEntry model)
            => new()
            {
                FileName = model.FileName,
                FileSize = model.FileSize,
                Checksum = model.Checksum,
                CompressedFileSize = model.CompressedFileSize,
                CompressedChecksum = model.CompressedChecksum,
                AcquireOnDemand = model.AcquireOnDemand,
                Compressed = model.Compressed,
                Platform = model.Platform,
                Tag = model.Tag
            };

        public PatchFileEntry ToModel()
            => new(FileName, FileSize, Checksum, CompressedFileSize, CompressedChecksum, AcquireOnDemand, Compressed, Platform, Tag);
    }

    private sealed class GameTableDto
    {
        public string PackageRelativePath { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }
        public List<GameTableColumnDto> Columns { get; set; } = [];
        public List<GameTableRowDto> Rows { get; set; } = [];

        public static GameTableDto FromModel(GameTable model)
            => new()
            {
                PackageRelativePath = model.PackageRelativePath,
                TableName = model.TableName,
                LanguageCode = model.LanguageCode,
                Columns = model.Columns.Select(GameTableColumnDto.FromModel).ToList(),
                Rows = model.Rows.Select(GameTableRowDto.FromModel).ToList()
            };

        public GameTable ToModel()
        {
            var table = new GameTable
            {
                PackageRelativePath = PackageRelativePath,
                TableName = TableName,
                LanguageCode = LanguageCode
            };
            table.Columns.AddRange(Columns.Select(column => column.ToModel()));
            table.Rows.AddRange(Rows.Select(row => row.ToModel()));
            return table;
        }
    }

    private sealed class GameTableColumnDto
    {
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }

        public static GameTableColumnDto FromModel(GameTableColumn model)
            => new() { Name = model.Name, Order = model.Order };

        public GameTableColumn ToModel()
            => new(Name, Order);
    }

    private sealed class GameTableRowDto
    {
        public int Order { get; set; }
        public List<GameTableCellDto> Cells { get; set; } = [];

        public static GameTableRowDto FromModel(GameTableRow model)
            => new()
            {
                Order = model.Order,
                Cells = model.Cells.Select(GameTableCellDto.FromModel).ToList()
            };

        public GameTableRow ToModel()
        {
            var row = new GameTableRow { Order = Order };
            row.Cells.AddRange(Cells.Select(cell => cell.ToModel()));
            return row;
        }
    }

    private sealed class GameTableCellDto
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public static GameTableCellDto FromModel(GameTableCell model)
            => new() { ColumnName = model.ColumnName, Value = model.Value };

        public GameTableCell ToModel()
            => new(ColumnName, Value);
    }

    private sealed class ResourceFileDto
    {
        public string PackageRelativePath { get; set; } = string.Empty;
        public string ProjectRelativePath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool Compressed { get; set; }
        public string? SourcePackagePath { get; set; }
        public string? Platform { get; set; }
        public List<string>? IncludedPlatforms { get; set; }

        public static ResourceFileDto FromModel(ResourceFile model)
            => new()
            {
                PackageRelativePath = model.PackageRelativePath,
                ProjectRelativePath = model.ProjectRelativePath,
                Category = model.Category,
                Compressed = model.Compressed,
                SourcePackagePath = model.SourcePackagePath,
                Platform = model.Platform,
                IncludedPlatforms = model.IncludedPlatforms?.ToList()
            };

        public ResourceFile ToModel()
            => new(PackageRelativePath, ProjectRelativePath, Category, Compressed, SourcePackagePath, Platform, IncludedPlatforms);
    }

    private sealed class PlatformPackageRecordDto
    {
        public string Platform { get; set; } = string.Empty;
        public string SourcePackageRoot { get; set; } = string.Empty;
        public string? Version { get; set; }
        public DateTimeOffset ImportedAt { get; set; }
        public List<PatchFileEntryDto> BaselineManifestEntries { get; set; } = [];
        public int ImportedTableFileCount { get; set; }
        public int ImportedResourceFileCount { get; set; }
        public int MissingPhysicalFileCount { get; set; }

        public static PlatformPackageRecordDto FromModel(PlatformPackageRecord model)
            => new()
            {
                Platform = model.Platform,
                SourcePackageRoot = model.SourcePackageRoot,
                Version = model.Version,
                ImportedAt = model.ImportedAt,
                BaselineManifestEntries = model.BaselineManifestEntries.Select(PatchFileEntryDto.FromModel).ToList(),
                ImportedTableFileCount = model.ImportedTableFileCount,
                ImportedResourceFileCount = model.ImportedResourceFileCount,
                MissingPhysicalFileCount = model.MissingPhysicalFileCount
            };

        public PlatformPackageRecord ToModel()
        {
            var record = new PlatformPackageRecord
            {
                Platform = Platform,
                SourcePackageRoot = SourcePackageRoot,
                Version = Version,
                ImportedAt = ImportedAt,
                ImportedTableFileCount = ImportedTableFileCount,
                ImportedResourceFileCount = ImportedResourceFileCount,
                MissingPhysicalFileCount = MissingPhysicalFileCount
            };
            record.BaselineManifestEntries.AddRange(BaselineManifestEntries.Select(entry => entry.ToModel()));
            return record;
        }
    }
}
