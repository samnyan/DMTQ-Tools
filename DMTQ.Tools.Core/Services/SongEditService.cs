using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class SongEditService
{
    public void UpdateSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(song.Id);

        var songRows = FindTables(package, "song_song")
            .Select(table => new { Table = table, Row = FindRow(table, "song_id", song.Id) })
            .Where(item => item.Row is not null)
            .ToArray();
        if (songRows.Length == 0)
        {
            throw new InvalidOperationException($"Song '{song.Id}' was not found.");
        }

        foreach (var item in songRows)
        {
            foreach (var field in song.SourceFields)
            {
                SetCell(item.Row!, field.Key, field.Value);
            }

            if (!string.IsNullOrWhiteSpace(song.PreviewPackageRelativePath))
            {
                SetCell(item.Row!, "preview", song.PreviewPackageRelativePath);
            }
        }

        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language))
            {
                continue;
            }

            var row = FindRow(table, "song_id", song.Id);
            if (row is null)
            {
                continue;
            }

            if (song.TitlesByLanguage.TryGetValue(language, out var title))
            {
                SetCell(row, "title", title);
            }

            if (song.DescriptionsByLanguage.TryGetValue(language, out var description))
            {
                SetCell(row, "description", description);
            }
        }

        foreach (var pattern in song.Patterns)
        {
            UpdatePatternInternal(package, song.Id, pattern.PatternId, pattern.SourceFields);
        }
    }

    public void AddSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(song.Id);

        if (FindTables(package, "song_song").Any(table => FindRow(table, "song_id", song.Id) is not null))
        {
            throw new InvalidOperationException($"Song '{song.Id}' already exists.");
        }

        AppendSongRows(package, song);
        AppendPatternRows(package, song);
        AppendSongDescriptionRows(package, song);
        AppendItemDescriptionRows(package, song);
        AppendProductRows(package, song);
        AppendProductItemRows(package, song);
        AppendCategoryProductRows(package, song);
    }

    public void UpdatePattern(
        PatchPackage package,
        string songId,
        string patternId,
        IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(songId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patternId);
        ArgumentNullException.ThrowIfNull(fields);

        UpdatePatternInternal(package, songId, patternId, fields);
    }

    public void AddPattern(
        PatchPackage package,
        string songId,
        SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(songId);
        ArgumentNullException.ThrowIfNull(pattern);

        var songTables = FindTables(package, "song_song");
        if (!songTables.Any(table => FindRow(table, "song_id", songId) is not null))
        {
            throw new InvalidOperationException($"Song '{songId}' does not exist. Add the song first.");
        }

        foreach (var table in FindTables(package, "song_songPattern"))
        {
            if (table.Rows.Any(row =>
                    GetCell(row, "song_id").Equals(songId, StringComparison.OrdinalIgnoreCase)
                    && GetCell(row, "pattern_id").Equals(pattern.PatternId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Pattern '{pattern.PatternId}' already exists for song '{songId}'.");
            }

            var row = CreateEmptyRow(table);
            SetCell(row, "pattern_id", pattern.PatternId);
            SetCell(row, "song_id", songId);
            foreach (var field in pattern.SourceFields)
            {
                SetCell(row, field.Key, field.Value);
            }

            table.Rows.Add(row);
        }
    }

    private static void UpdatePatternInternal(
        PatchPackage package,
        string songId,
        string patternId,
        IReadOnlyDictionary<string, string> fields)
    {
        var updated = false;
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            var row = table.Rows.FirstOrDefault(r =>
                GetCell(r, "song_id").Equals(songId, StringComparison.OrdinalIgnoreCase)
                && GetCell(r, "pattern_id").Equals(patternId, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                continue;
            }

            foreach (var field in fields)
            {
                SetCell(row, field.Key, field.Value);
            }

            updated = true;
        }

        if (!updated)
        {
            throw new InvalidOperationException(
                $"Pattern '{patternId}' for song '{songId}' was not found.");
        }
    }

    private static void AppendSongRows(PatchPackage package, Song song)
    {
        foreach (var table in FindTables(package, "song_song"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "song_id", song.Id);
            foreach (var field in song.SourceFields)
            {
                SetCell(row, field.Key, field.Value);
            }

            if (!string.IsNullOrWhiteSpace(song.PreviewPackageRelativePath))
            {
                SetCell(row, "preview", song.PreviewPackageRelativePath);
            }

            table.Rows.Add(row);
        }
    }

    private static void AppendPatternRows(PatchPackage package, Song song)
    {
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            foreach (var pattern in song.Patterns)
            {
                var row = CreateEmptyRow(table);
                SetCell(row, "pattern_id", pattern.PatternId);
                SetCell(row, "song_id", song.Id);
                foreach (var field in pattern.SourceFields)
                {
                    SetCell(row, field.Key, field.Value);
                }

                table.Rows.Add(row);
            }
        }
    }

    private static void AppendSongDescriptionRows(PatchPackage package, Song song)
    {
        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            var row = CreateEmptyRow(table);
            SetCell(row, "song_id", song.Id);
            SetCell(row, "title", song.TitlesByLanguage.TryGetValue(language, out var title) ? title : string.Empty);
            SetCell(row, "description", song.DescriptionsByLanguage.TryGetValue(language, out var description) ? description : string.Empty);
            table.Rows.Add(row);
        }
    }

    private static void AppendItemDescriptionRows(PatchPackage package, Song song)
    {
        foreach (var table in FindLocalizedTables(package, "item_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            var row = CreateEmptyRow(table);
            SetCell(row, "item_id", song.ItemIds.FirstOrDefault() ?? song.Id);
            SetCell(row, "name", song.ItemNamesByLanguage.TryGetValue(language, out var name) ? name : string.Empty);
            SetCell(row, "description", song.DescriptionsByLanguage.TryGetValue(language, out var description) ? description : string.Empty);
            table.Rows.Add(row);
        }
    }

    private static void AppendProductRows(PatchPackage package, Song song)
    {
        foreach (var productId in song.ProductIds)
        {
            foreach (var table in FindTables(package, "product_product"))
            {
                var row = CreateEmptyRow(table);
                SetCell(row, "product_id", productId);
                SetCell(row, "song_id", song.Id);
                table.Rows.Add(row);
            }
        }
    }

    private static void AppendProductItemRows(PatchPackage package, Song song)
    {
        foreach (var productId in song.ProductIds)
        {
            foreach (var itemId in song.ItemIds)
            {
                foreach (var table in FindTables(package, "product_item"))
                {
                    var row = CreateEmptyRow(table);
                    SetCell(row, "product_id", productId);
                    SetCell(row, "item_id", itemId);
                    table.Rows.Add(row);
                }
            }
        }
    }

    private static void AppendCategoryProductRows(PatchPackage package, Song song)
    {
        foreach (var categoryId in song.CategoryIds)
        {
            foreach (var productId in song.ProductIds)
            {
                foreach (var table in FindTables(package, "category_categoryproduct"))
                {
                    var row = CreateEmptyRow(table);
                    SetCell(row, "category_id", categoryId);
                    SetCell(row, "product_id", productId);
                    table.Rows.Add(row);
                }
            }
        }
    }

    private static IEnumerable<GameTable> FindTables(PatchPackage package, string tableName)
        => package.Tables.Tables.Where(table => table.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<GameTable> FindLocalizedTables(PatchPackage package, string logicalName)
        => package.Tables.Tables.Where(table => table.TableName.StartsWith(logicalName + "_", StringComparison.OrdinalIgnoreCase));

    private static GameTableRow? FindRow(GameTable table, string keyColumn, string keyValue)
        => table.Rows.FirstOrDefault(row => GetCell(row, keyColumn).Equals(keyValue, StringComparison.OrdinalIgnoreCase));

    private static string GetCell(GameTableRow row, string columnName)
        => row.Cells.FirstOrDefault(cell => cell.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    private static void SetCell(GameTableRow row, string columnName, string value)
    {
        for (var i = 0; i < row.Cells.Count; i++)
        {
            if (row.Cells[i].ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                row.Cells[i] = row.Cells[i] with { Value = value };
                return;
            }
        }

        row.Cells.Add(new GameTableCell(columnName, value));
    }

    private static string ExtractLanguage(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? string.Empty : tableName[(index + 1)..];
    }

    private static GameTableRow CreateEmptyRow(GameTable table)
    {
        var row = new GameTableRow
        {
            Order = table.Rows.Count == 0 ? 0 : table.Rows.Max(existing => existing.Order) + 1
        };

        foreach (var column in table.Columns.OrderBy(column => column.Order))
        {
            row.Cells.Add(new GameTableCell(column.Name, string.Empty));
        }

        return row;
    }
}
