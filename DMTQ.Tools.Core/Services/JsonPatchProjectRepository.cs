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
                Platforms = package.Platforms.Select(PlatformPackageRecordDto.FromModel).ToList()
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

            var options = new PackageExportOptions();
            foreach (var item in CompressionOverrides)
            {
                options.SetCompression(item.Key, item.Value);
            }

            return new PatchProjectSnapshot(package, ExportCompressionMode, options);
        }
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
