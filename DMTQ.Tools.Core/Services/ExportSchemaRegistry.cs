using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Models.Csv;

namespace DMTQ.Tools.Core.Services;

/// <summary>
/// Maps CSV table paths to the appropriate <see cref="CsvSchema{T}"/> or
/// <see cref="CsvLookupSchema{T}"/> and writes entity data directly to a stream.
/// </summary>
public static class ExportSchemaRegistry
{
    /// <summary>
    /// Attempts to write a CSV table from entity data. Returns <c>true</c> when the
    /// table is entity-backed and was written; returns <c>false</c> when the table
    /// is not recognised — the caller should fall back to legacy GameTable writing.
    /// </summary>
    public static bool TryWriteTable(
        string relativePath,
        PatchPackage package,
        Stream stream,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(stream);

        var tableName = GetTableName(relativePath);
        var languageCode = GetLanguageCode(relativePath);
        var tables = package.GetPlatformTables(platform);

        switch (tableName)
        {
            case "song_song":
                if (tables.Songs.Count > 0)
                {
                    new SongCsvSchema().WriteCsv(stream, tables.Songs);
                    return true;
                }
                return false;

            case "song_songPattern":
            {
                var patterns = tables.Songs.SelectMany(s => s.Patterns).ToList();
                if (patterns.Count > 0)
                {
                    new PatternCsvSchema().WriteCsv(stream, patterns);
                    return true;
                }
                return false;
            }

            case "quest_achievement":
                if (tables.Achievements.Count > 0)
                {
                    new AchievementCsvSchema().WriteCsv(stream, tables.Achievements);
                    return true;
                }
                return false;

            case "product_product":
                if (tables.Products.Count > 0)
                {
                    new ProductCsvSchema().WriteCsv(stream, tables.Products);
                    return true;
                }
                return false;

            case "category_categoryproduct":
                if (tables.Products.Count > 0)
                {
                    new CategoryProductCsvSchema().WriteCsv(stream, tables.Products);
                    return true;
                }
                return false;

            case "product_item":
                if (tables.Items.Count > 0)
                {
                    new ItemCsvSchema().WriteCsv(stream, tables.Items);
                    return true;
                }
                return false;

            case "ingameitem_ingameitem":
                if (tables.IngameItems.Count > 0)
                {
                    new IngameItemCsvSchema().WriteCsv(stream, tables.IngameItems);
                    return true;
                }
                return false;

            case "ingameitem_itemeffect":
                if (tables.IngameItemEffects.Count > 0)
                {
                    new IngameItemEffectCsvSchema().WriteCsv(stream, tables.IngameItemEffects);
                    return true;
                }
                return false;

            case "slang":
                if (package.SlangEntries.Count > 0)
                {
                    new SlangCsvSchema().WriteCsv(stream, package.SlangEntries);
                    return true;
                }
                return false;
        }

        // Localized tables: try language suffix match
        var lang = languageCode ?? ExtractLanguageSuffix(tableName);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            var logicalName = GetLogicalName(tableName, lang);

            switch (logicalName)
            {
                case "song_desc":
                    if (tables.Songs.Count > 0)
                    {
                        WriteSongDesc(stream, tables.Songs, lang);
                        return true;
                    }
                    return false;

                case "acievement_desc":
                    if (tables.Achievements.Count > 0)
                    {
                        new AchievementDescCsvSchema(lang).WriteCsv(stream, tables.Achievements);
                        return true;
                    }
                    return false;

                case "quest_desc":
                    if (tables.Quests.Count > 0)
                    {
                        new QuestDescCsvSchema(lang).WriteCsv(stream, tables.Quests);
                        return true;
                    }
                    return false;

                case "quest_mission_desc":
                    if (tables.Quests.Count > 0)
                    {
                        new QuestMissionDescCsvSchema(lang).WriteCsv(stream, tables.Quests);
                        return true;
                    }
                    return false;

                case "item_desc":
                    if (tables.Items.Count > 0)
                    {
                        new ItemDescCsvSchema(lang).WriteCsv(stream, tables.Items);
                        return true;
                    }
                    return false;
            }
        }

        // Not an entity-backed table — fall back to GameTable-based writer
        return false;
    }

    private static void WriteSongDesc(Stream stream, IEnumerable<Song> songs, string language)
    {
        // Gather SongLocalization objects from all Songs for the given language.
        var localizations = new List<SongLocalization>();
        foreach (var song in songs)
        {
            if (song.Localizations.TryGetValue(language, out var loc))
            {
                if (!string.IsNullOrWhiteSpace(loc.FullName)
                    || !string.IsNullOrWhiteSpace(loc.Genre)
                    || !string.IsNullOrWhiteSpace(loc.ArtistName)
                    || !string.IsNullOrWhiteSpace(loc.ComposedBy)
                    || !string.IsNullOrWhiteSpace(loc.Singer)
                    || !string.IsNullOrWhiteSpace(loc.FeatBy)
                    || !string.IsNullOrWhiteSpace(loc.ArrangedBy)
                    || !string.IsNullOrWhiteSpace(loc.VisualizedBy))
                {
                    var exportLoc = new SongLocalization
                    {
                        SongId = song.Id,
                        FullName = loc.FullName,
                        Genre = loc.Genre,
                        ArtistName = loc.ArtistName,
                        ComposedBy = loc.ComposedBy,
                        Singer = loc.Singer,
                        FeatBy = loc.FeatBy,
                        ArrangedBy = loc.ArrangedBy,
                        VisualizedBy = loc.VisualizedBy,
                    };
                    localizations.Add(exportLoc);
                }
            }
        }

        new SongDescCsvSchema(language).WriteCsv(stream, localizations);
    }

    // ── path helpers (mirror SongTableProjector logic) ──

    private static string GetTableName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    private static string? GetLanguageCode(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[0].Equals("table", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }

    private static string? ExtractLanguageSuffix(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? null : tableName[(index + 1)..];
    }

    private static string GetLogicalName(string tableName, string languageSuffix)
    {
        // e.g. "song_desc_us" → "song_desc"
        if (tableName.EndsWith("_" + languageSuffix, StringComparison.OrdinalIgnoreCase))
            return tableName[..^(languageSuffix.Length + 1)];
        return tableName;
    }
}
