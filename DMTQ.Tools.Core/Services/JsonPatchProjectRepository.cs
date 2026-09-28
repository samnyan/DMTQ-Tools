using System.Text.Json;
using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

public sealed class JsonPatchProjectRepository : IPatchProjectRepository
{
    private const int CurrentSchemaVersion = 2;
    private const string ProjectFileName = "project.json";
    private const string SlangFileName = "slang.json";

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
        await WriteJsonAtomicallyAsync(
            Path.Combine(projectRoot, SlangFileName),
            package.SlangEntries,
            cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicallyAsync(
            Path.Combine(projectRoot, ProjectFileName),
            document,
            cancellationToken).ConfigureAwait(false);
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
        using var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = jsonDocument.RootElement;
        var document = root.Deserialize<ProjectDocument>(JsonOptions);
        if (document is null)
        {
            throw new InvalidDataException("GameTableManager project file is empty or invalid.");
        }

        if (document.SchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported GameTableManager project schema version {document.SchemaVersion}.");
        }

        var snapshot = document.ToSnapshot(projectRoot);
        var slangPath = Path.Combine(projectRoot, SlangFileName);
        if (File.Exists(slangPath))
        {
            await using var slangStream = File.OpenRead(slangPath);
            var slangEntries = await JsonSerializer.DeserializeAsync<List<SlangEntry>>(
                slangStream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            if (slangEntries is null)
            {
                throw new InvalidDataException("GameTableManager slang file is empty or invalid.");
            }

            snapshot.Package.SlangEntries.Clear();
            snapshot.Package.SlangEntries.AddRange(slangEntries);
        }
        else if (TryGetLegacySlangEntries(root, out var legacySlangEntries))
        {
            snapshot.Package.SlangEntries.AddRange(legacySlangEntries);
        }

        return snapshot;
    }

    private static bool TryGetLegacySlangEntries(JsonElement projectJson, out List<SlangEntry> entries)
    {
        foreach (var property in projectJson.EnumerateObject())
        {
            if (property.Name.Equals("SlangEntries", StringComparison.OrdinalIgnoreCase))
            {
                entries = property.Value.Deserialize<List<SlangEntry>>(JsonOptions) ?? [];
                return true;
            }
        }

        entries = [];
        return false;
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string jsonPath,
        T value,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(jsonPath)
                        ?? throw new InvalidOperationException("JSON output path must have a parent directory.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(jsonPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, jsonPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
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
        public Dictionary<string, ProductCategoryMapping> ProductCategoryMappings { get; set; } = ProductCategoryDefaults.Create();
        public List<Item> Items { get; set; } = [];
        public List<IngameItem> IngameItems { get; set; } = [];
        public List<IngameItemEffect> IngameItemEffects { get; set; } = [];
        public Dictionary<string, PlatformTableData> PlatformTables { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
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
                ProductCategoryMappings = new Dictionary<string, ProductCategoryMapping>(
                    package.ProductCategoryMappings,
                    StringComparer.OrdinalIgnoreCase),
                Items = [..package.Items],
                IngameItems = [..package.IngameItems],
                IngameItemEffects = [..package.IngameItemEffects],
                PlatformTables = new Dictionary<string, PlatformTableData>(
                    package.PlatformTables,
                    StringComparer.OrdinalIgnoreCase)
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
            package.ProductCategoryMappings = ProductCategoryMappings;
            package.Items.AddRange(Items);
            package.IngameItems.AddRange(IngameItems);
            package.IngameItemEffects.AddRange(IngameItemEffects);
            package.PlatformTables = new Dictionary<string, PlatformTableData>(
                PlatformTables,
                StringComparer.OrdinalIgnoreCase);

            var options = new PackageExportOptions();
            foreach (var item in CompressionOverrides)
            {
                options.SetCompression(item.Key, item.Value);
            }

            return new PatchProjectSnapshot(package, ExportCompressionMode, options);
        }
    }
}
