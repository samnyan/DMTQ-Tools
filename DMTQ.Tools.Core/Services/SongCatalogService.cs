using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed partial class SongCatalogService
{
    public IReadOnlyList<Song> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var songTable = FindTable(package, "song_song");
        if (songTable is null)
        {
            return [];
        }

        var songs = BuildSongRows(songTable);
        AddPatternRows(package, songs);
        AddSongDescriptions(package, songs);
        AddProductAndItemLinks(package, songs);
        AddPreviewLinks(package, songs);

        return songs.Values
            .OrderBy(song => song.GetTitle("us"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, Song> BuildSongRows(GameTable songTable)
    {
        var result = new Dictionary<string, Song>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in songTable.Rows.OrderBy(row => row.Order))
        {
            var songId = GetCell(row, "song_id", "songId", "id");
            if (string.IsNullOrWhiteSpace(songId) || result.ContainsKey(songId))
            {
                continue;
            }

            var song = new Song { Id = songId };
            CopyCells(row, song.SourceFields);
            result[songId] = song;
        }

        return result;
    }

    private static void AddPatternRows(PatchPackage package, Dictionary<string, Song> songs)
    {
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            foreach (var row in table.Rows.OrderBy(row => row.Order))
            {
                var songId = GetCell(row, "song_id", "songId");
                if (!songs.TryGetValue(songId, out var song))
                {
                    continue;
                }

                var patternId = GetCell(row, "pattern_id", "song_pattern_id", "id");
                if (string.IsNullOrWhiteSpace(patternId))
                {
                    continue;
                }

                var pattern = new SongPattern
                {
                    PatternId = patternId,
                    SongId = songId
                };
                CopyCells(row, pattern.SourceFields);
                song.Patterns.Add(pattern);
            }
        }

        foreach (var song in songs.Values)
        {
            song.Patterns.Sort((left, right) =>
                string.Compare(left.PatternId, right.PatternId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AddSongDescriptions(PatchPackage package, Dictionary<string, Song> songs)
    {
        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName) ?? string.Empty;
            foreach (var row in table.Rows)
            {
                var songId = GetCell(row, "song_id", "songId", "id");
                if (!songs.TryGetValue(songId, out var song))
                {
                    continue;
                }

                var title = GetCell(row, "title", "name", "song_name");
                var description = GetCell(row, "description", "desc", "comment");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    song.TitlesByLanguage[language] = title;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    song.DescriptionsByLanguage[language] = description;
                }
            }
        }
    }

    private static void AddProductAndItemLinks(PatchPackage package, Dictionary<string, Song> songs)
    {
        var productToSong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in FindTables(package, "product_product"))
        {
            foreach (var row in table.Rows)
            {
                var productId = GetCell(row, "product_id", "id");
                var songId = GetCell(row, "song_id", "songId");
                if (!string.IsNullOrWhiteSpace(productId) && songs.TryGetValue(songId, out var song))
                {
                    productToSong[productId] = songId;
                    AddDistinct(song.ProductIds, productId);
                }
            }
        }

        var itemToSong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in FindTables(package, "product_item"))
        {
            foreach (var row in table.Rows)
            {
                var productId = GetCell(row, "product_id");
                var itemId = GetCell(row, "item_id", "itemId");
                if (!productToSong.TryGetValue(productId, out var songId) || !songs.TryGetValue(songId, out var song))
                {
                    continue;
                }

                itemToSong[itemId] = songId;
                AddDistinct(song.ItemIds, itemId);
            }
        }

        foreach (var table in FindTables(package, "category_categoryproduct"))
        {
            foreach (var row in table.Rows)
            {
                var productId = GetCell(row, "product_id");
                var categoryId = GetCell(row, "category_id");
                if (productToSong.TryGetValue(productId, out var songId) && songs.TryGetValue(songId, out var song))
                {
                    AddDistinct(song.CategoryIds, categoryId);
                }
            }
        }

        foreach (var table in FindLocalizedTables(package, "item_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName) ?? string.Empty;
            foreach (var row in table.Rows)
            {
                var itemId = GetCell(row, "item_id", "itemId", "id");
                if (!itemToSong.TryGetValue(itemId, out var songId) || !songs.TryGetValue(songId, out var song))
                {
                    continue;
                }

                var itemName = GetCell(row, "name", "title", "item_name");
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    song.ItemNamesByLanguage[language] = itemName;
                }
            }
        }
    }

    private static void AddPreviewLinks(PatchPackage package, Dictionary<string, Song> songs)
    {
        var previewPaths = package.Resources
            .Where(resource => resource.Category.Equals("preview", StringComparison.OrdinalIgnoreCase))
            .Select(resource => resource.PackageRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var song in songs.Values)
        {
            var explicitPreview = GetSourceField(song, "preview", "preview_path", "preview_file");
            if (!string.IsNullOrWhiteSpace(explicitPreview) && previewPaths.Contains(explicitPreview))
            {
                song.PreviewPackageRelativePath = explicitPreview;
                continue;
            }

            var songIdMatch = previewPaths.FirstOrDefault(path =>
                path.Contains(song.Id, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(songIdMatch))
            {
                song.PreviewPackageRelativePath = songIdMatch;
            }
        }
    }

    private static GameTable? FindTable(PatchPackage package, string tableName)
        => FindTables(package, tableName).FirstOrDefault();

    private static IEnumerable<GameTable> FindTables(PatchPackage package, string tableName)
        => package.Tables.Tables
            .Where(table => table.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(table => table.LanguageCode ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<GameTable> FindLocalizedTables(PatchPackage package, string logicalName)
        => package.Tables.Tables
            .Where(table => table.TableName.StartsWith(logicalName + "_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(table => table.LanguageCode ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static string GetCell(GameTableRow row, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            var value = row.Cells.FirstOrDefault(cell =>
                cell.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void CopyCells(GameTableRow row, Dictionary<string, string> target)
    {
        foreach (var cell in row.Cells)
        {
            target[cell.ColumnName] = cell.Value;
        }
    }

    private static string GetSourceField(Song song, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (song.SourceFields.TryGetValue(fieldName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void AddDistinct(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static string? ExtractLanguage(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? null : tableName[(index + 1)..];
    }
}
