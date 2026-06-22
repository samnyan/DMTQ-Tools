using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class SongEditService
{
    public void UpdateSong(PatchPackage package, SongEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SongId);

        var songRows = FindTables(package, "song_song")
            .Select(table => new { Table = table, Row = FindRow(table, "song_id", request.SongId) })
            .Where(item => item.Row is not null)
            .ToArray();
        if (songRows.Length == 0)
        {
            throw new InvalidOperationException($"Song '{request.SongId}' was not found.");
        }

        foreach (var item in songRows)
        {
            foreach (var field in request.SourceFields)
            {
                SetCell(item.Row!, field.Key, field.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PreviewPackageRelativePath))
            {
                SetCell(item.Row!, "preview", request.PreviewPackageRelativePath);
            }
        }

        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language))
            {
                continue;
            }

            var row = FindRow(table, "song_id", request.SongId);
            if (row is null)
            {
                continue;
            }

            if (request.TitlesByLanguage.TryGetValue(language, out var title))
            {
                SetCell(row, "title", title);
            }

            if (request.DescriptionsByLanguage.TryGetValue(language, out var description))
            {
                SetCell(row, "description", description);
            }
        }

        foreach (var table in FindTables(package, "song_songPattern"))
        {
            foreach (var row in table.Rows.Where(row => GetCell(row, "song_id") == request.SongId))
            {
                var patternId = GetCell(row, "pattern_id");
                if (request.PatternDifficultyByPatternId.TryGetValue(patternId, out var difficulty))
                {
                    SetCell(row, "difficulty", difficulty);
                }

                if (request.PatternLevelByPatternId.TryGetValue(patternId, out var level))
                {
                    SetCell(row, "level", level);
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

    public void AddSong(PatchPackage package, AddSongRequest request)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SongId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CategoryId);

        if (FindTables(package, "song_song").Any(table => FindRow(table, "song_id", request.SongId) is not null))
        {
            throw new InvalidOperationException($"Song '{request.SongId}' already exists.");
        }

        AppendSongRows(package, request);
        AppendPatternRows(package, request);
        AppendSongDescriptionRows(package, request);
        AppendItemDescriptionRows(package, request);
        AppendProductRows(package, request);
        AppendProductItemRows(package, request);
        AppendCategoryProductRows(package, request);
    }

    private static void AppendSongRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindTables(package, "song_song"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "song_id", request.SongId);
            foreach (var field in request.SongFields)
            {
                SetCell(row, field.Key, field.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PreviewPackageRelativePath))
            {
                SetCell(row, "preview", request.PreviewPackageRelativePath);
            }

            table.Rows.Add(row);
        }
    }

    private static void AppendPatternRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            foreach (var pattern in request.Patterns)
            {
                var row = CreateEmptyRow(table);
                SetCell(row, "pattern_id", pattern.PatternId);
                SetCell(row, "song_id", request.SongId);
                SetCell(row, "name", pattern.PatternName);
                SetCell(row, "difficulty", pattern.Difficulty);
                SetCell(row, "level", pattern.Level);
                table.Rows.Add(row);
            }
        }
    }

    private static void AppendSongDescriptionRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            var row = CreateEmptyRow(table);
            SetCell(row, "song_id", request.SongId);
            SetCell(row, "title", request.TitlesByLanguage.TryGetValue(language, out var title) ? title : string.Empty);
            SetCell(row, "description", request.DescriptionsByLanguage.TryGetValue(language, out var description) ? description : string.Empty);
            table.Rows.Add(row);
        }
    }

    private static void AppendItemDescriptionRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindLocalizedTables(package, "item_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            var row = CreateEmptyRow(table);
            SetCell(row, "item_id", request.ItemId);
            SetCell(row, "name", request.ItemNamesByLanguage.TryGetValue(language, out var name) ? name : string.Empty);
            SetCell(row, "description", request.ItemDescriptionsByLanguage.TryGetValue(language, out var description) ? description : string.Empty);
            table.Rows.Add(row);
        }
    }

    private static void AppendProductRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindTables(package, "product_product"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "product_id", request.ProductId);
            SetCell(row, "song_id", request.SongId);
            table.Rows.Add(row);
        }
    }

    private static void AppendProductItemRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindTables(package, "product_item"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "product_id", request.ProductId);
            SetCell(row, "item_id", request.ItemId);
            table.Rows.Add(row);
        }
    }

    private static void AppendCategoryProductRows(PatchPackage package, AddSongRequest request)
    {
        foreach (var table in FindTables(package, "category_categoryproduct"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "category_id", request.CategoryId);
            SetCell(row, "product_id", request.ProductId);
            table.Rows.Add(row);
        }
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
