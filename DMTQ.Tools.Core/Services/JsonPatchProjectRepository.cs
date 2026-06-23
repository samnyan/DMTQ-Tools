using System.Text.Json;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

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
        public ProjectInfo? ProjectInfo { get; set; }
        public List<GameTable> Tables { get; set; } = [];
        public List<ResourceFile> Resources { get; set; } = [];
        public List<Song> Songs { get; set; } = [];
        public List<Achievement> Achievements { get; set; } = [];
        public List<Quest> Quests { get; set; } = [];
        public List<Product> Products { get; set; } = [];
        public List<Item> Items { get; set; } = [];
        public List<IngameItem> IngameItems { get; set; } = [];
        public List<IngameItemEffect> IngameItemEffects { get; set; } = [];

        public static ProjectDocument FromPackage(
            PatchPackage package,
            string exportCompressionMode,
            PackageExportOptions exportOptions)
        {
            return new ProjectDocument
            {
                ExportCompressionMode = exportCompressionMode,
                CompressionOverrides = new Dictionary<string, bool>(exportOptions.CompressionOverrides, StringComparer.OrdinalIgnoreCase),
                ProjectInfo = package.ProjectInfo,
                Tables = [..package.Tables.Tables],
                Resources = [..package.Resources],
                Songs = [..package.Songs],
                Achievements = [..package.Achievements],
                Quests = [..package.Quests],
                Products = [..package.Products],
                Items = [..package.Items],
                IngameItems = [..package.IngameItems],
                IngameItemEffects = [..package.IngameItemEffects]
            };
        }

        public PatchProjectSnapshot ToSnapshot(string projectRoot)
        {
            var package = new PatchPackage
            {
                ProjectInfo = new ProjectInfo(projectRoot, ProjectInfo?.SourcePackageRoot, ProjectInfo?.Version, ProjectInfo?.Platform)
            };

            package.Tables.Tables.AddRange(Tables);
            package.Resources.AddRange(Resources);
            package.Songs.AddRange(Songs);
            package.Achievements.AddRange(Achievements);
            package.Quests.AddRange(Quests);
            package.Products.AddRange(Products);
            package.Items.AddRange(Items);
            package.IngameItems.AddRange(IngameItems);
            package.IngameItemEffects.AddRange(IngameItemEffects);

            var options = new PackageExportOptions();
            foreach (var item in CompressionOverrides)
            {
                options.SetCompression(item.Key, item.Value);
            }

            return new PatchProjectSnapshot(package, ExportCompressionMode, options);
        }
    }
}
