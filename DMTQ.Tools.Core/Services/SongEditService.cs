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
            WriteSongCells(item.Row!, song);
        }

        foreach (var table in FindLocalizedTables(package, "song_desc"))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language)) continue;

            var row = FindRow(table, "song_id", song.Id);
            if (row is null) continue;

            if (song.TitlesByLanguage.TryGetValue(language, out var title))
                SetCell(row, "title", title);
            if (song.DescriptionsByLanguage.TryGetValue(language, out var description))
                SetCell(row, "description", description);
        }

        foreach (var pattern in song.Patterns)
        {
            WritePatternToAllTables(package, pattern);
        }
    }

    public void AddSong(PatchPackage package, Song song)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(song.Id);

        if (FindTables(package, "song_song").Any(table => FindRow(table, "song_id", song.Id) is not null))
            throw new InvalidOperationException($"Song '{song.Id}' already exists.");

        AppendSongRows(package, song);
        AppendPatternRows(package, song);
        AppendSongDescriptionRows(package, song);
        AppendItemDescriptionRows(package, song);
        AppendProductRows(package, song);
        AppendProductItemRows(package, song);
        AppendCategoryProductRows(package, song);
    }

    public void UpdatePattern(PatchPackage package, string songId, string patternId,
        SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var updated = false;
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            var row = table.Rows.FirstOrDefault(r =>
                GetCell(r, "song_id").Equals(songId, StringComparison.OrdinalIgnoreCase)
                && GetCell(r, "pattern_id").Equals(patternId, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;

            WritePatternCells(row, pattern);
            updated = true;
        }

        if (!updated)
            throw new InvalidOperationException($"Pattern '{patternId}' for song '{songId}' was not found.");
    }

    public void AddPattern(PatchPackage package, string songId, SongPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pattern);

        var songTables = FindTables(package, "song_song");
        if (!songTables.Any(table => FindRow(table, "song_id", songId) is not null))
            throw new InvalidOperationException($"Song '{songId}' does not exist.");

        foreach (var table in FindTables(package, "song_songPattern"))
        {
            if (table.Rows.Any(row =>
                    GetCell(row, "song_id").Equals(songId, StringComparison.OrdinalIgnoreCase)
                    && GetCell(row, "pattern_id").Equals(pattern.PatternId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Pattern '{pattern.PatternId}' already exists for song '{songId}'.");
            }

            var row = CreateEmptyRow(table);
            SetCell(row, "pattern_id", pattern.PatternId);
            SetCell(row, "song_id", songId);
            WritePatternCells(row, pattern);
            table.Rows.Add(row);
        }
    }

    // ── Cell mapping: Song → CSV cells ──

    private static void WriteSongCells(GameTableRow row, Song song)
    {
        SetCell(row, "item_id", song.ItemId);
        SetCell(row, "name", song.Name);
        SetCell(row, "full_name", song.FullName);
        SetCell(row, "genre", song.Genre);
        SetCell(row, "artist_name", song.ArtistName);
        SetCell(row, "original_bga_yn", song.OriginalBgaYn);
        SetCell(row, "loop_bga_yn", song.LoopBgaYn);
        SetCell(row, "composed_by", song.ComposedBy);
        SetCell(row, "singer", song.Singer);
        SetCell(row, "feat_by", song.FeatBy);
        SetCell(row, "arranged_by", song.ArrangedBy);
        SetCell(row, "visualized_by", song.VisualizedBy);
        SetCell(row, "cost_game_point", song.CostGamePoint);
        SetCell(row, "cost_game_cash", song.CostGameCash);
        SetCell(row, "flag", song.Flag);
        SetCell(row, "status", song.Status);
        SetCell(row, "free_yn", song.FreeYn);
        SetCell(row, "hidden_yn", song.HiddenYn);
        SetCell(row, "open_yn", song.OpenYn);
        SetCell(row, "track_id", song.TrackId);
        SetCell(row, "mod_date", song.ModDate);
        SetCell(row, "update", song.Update);
        if (!string.IsNullOrWhiteSpace(song.PreviewPackageRelativePath))
            SetCell(row, "preview", song.PreviewPackageRelativePath);
    }

    private static void WritePatternCells(GameTableRow row, SongPattern pattern)
    {
        if (!string.IsNullOrWhiteSpace(pattern.Name)) SetCell(row, "name", pattern.Name);
        if (!string.IsNullOrWhiteSpace(pattern.Line)) SetCell(row, "line", pattern.Line);
        if (!string.IsNullOrWhiteSpace(pattern.Signature)) SetCell(row, "signature", pattern.Signature);
        if (!string.IsNullOrWhiteSpace(pattern.Difficulty)) SetCell(row, "difficulty", pattern.Difficulty);
        if (!string.IsNullOrWhiteSpace(pattern.Level)) SetCell(row, "level", pattern.Level);
        if (!string.IsNullOrWhiteSpace(pattern.PointType)) SetCell(row, "point_type", pattern.PointType);
        if (!string.IsNullOrWhiteSpace(pattern.PointValue)) SetCell(row, "point_value", pattern.PointValue);
        if (!string.IsNullOrWhiteSpace(pattern.Flg)) SetCell(row, "flg", pattern.Flg);
        if (!string.IsNullOrWhiteSpace(pattern.Update)) SetCell(row, "update", pattern.Update);
    }

    private static void WritePatternToAllTables(PatchPackage package, SongPattern pattern)
    {
        foreach (var table in FindTables(package, "song_songPattern"))
        {
            var row = table.Rows.FirstOrDefault(r =>
                GetCell(r, "song_id").Equals(pattern.SongId, StringComparison.OrdinalIgnoreCase)
                && GetCell(r, "pattern_id").Equals(pattern.PatternId, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;
            WritePatternCells(row, pattern);
        }
    }

    // ── Append helpers ──

    private static void AppendSongRows(PatchPackage package, Song song)
    {
        foreach (var table in FindTables(package, "song_song"))
        {
            var row = CreateEmptyRow(table);
            SetCell(row, "song_id", song.Id);
            WriteSongCells(row, song);
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
                WritePatternCells(row, pattern);
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
            SetCell(row, "title", song.TitlesByLanguage.TryGetValue(language, out var t) ? t : string.Empty);
            SetCell(row, "description", song.DescriptionsByLanguage.TryGetValue(language, out var d) ? d : string.Empty);
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
            SetCell(row, "name", song.ItemNamesByLanguage.TryGetValue(language, out var n) ? n : string.Empty);
            SetCell(row, "description", song.DescriptionsByLanguage.TryGetValue(language, out var d) ? d : string.Empty);
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

    // ── Table helpers ──

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
            row.Cells.Add(new GameTableCell(column.Name, string.Empty));
        return row;
    }
}
