using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

/// <summary>Projects <see cref="Song"/> entities back into <see cref="GameTable"/>
/// objects for CSV export.</summary>
public sealed class SongTableProjector
{
    public List<GameTable> ProjectTables(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var tables = new List<GameTable>();

        ProjectEntityTables(package, tables, SongCatalogService.IsSongRelatedTable,
            (path, lang, name) => name switch
            {
                "song_song" => BuildSongTable(path, lang, package.Songs),
                "song_songPattern" => BuildPatternTable(path, lang, package.Songs),
                _ when name.StartsWith("song_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildSongDescTable(path, lang, name, package.Songs),
                _ when name.StartsWith("item_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildItemDescTable(path, lang, name, package.Songs),
                "product_product" => BuildProductProductTable(path, lang, package.Songs),
                "product_item" => BuildProductItemTable(path, lang, package.Songs),
                "category_categoryproduct" => BuildCategoryProductTable(path, lang, package.Songs),
                _ => null
            });

        ProjectEntityTables(package, tables, SongCatalogService.IsAchievementRelatedTable,
            (path, lang, name) => name switch
            {
                "quest_achievement" => BuildAchievementTable(path, lang, package.Achievements),
                _ when name.StartsWith("acievement_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildAchievementDescTable(path, lang, name, package.Achievements),
                _ => null
            });

        ProjectEntityTables(package, tables, SongCatalogService.IsQuestRelatedTable,
            (path, lang, name) => name switch
            {
                _ when name.StartsWith("quest_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildQuestDescTable(path, lang, name, package.Quests),
                _ when name.StartsWith("quest_mission_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildQuestMissionDescTable(path, lang, name, package.Quests),
                _ => null
            });

        return tables;
    }

    private static void ProjectEntityTables(
        PatchPackage package,
        List<GameTable> tables,
        Func<string, bool> isRelated,
        Func<string, string?, string, GameTable?> build)
    {
        var paths = package.Manifest.Entries
            .Select(e => PathClassifier.NormalizePackageRelativePath(e.FileName))
            .Where(p => PathClassifier.IsCsvTable(p) && isRelated(GetTableName(p)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var tableName = GetTableName(path);
            var languageCode = GetLanguageCode(path);
            var table = build(path, languageCode, tableName);
            if (table is not null)
                tables.Add(table);
        }
    }

    private static GameTable BuildSongTable(string path, string? languageCode, List<Song> songs)
    {
        var columns = new[] { "song_id", "item_id", "name", "full_name", "genre", "artist_name",
            "original_bga_yn", "loop_bga_yn", "composed_by", "singer", "feat_by", "arranged_by",
            "visualized_by", "cost_game_point", "cost_game_cash", "flag", "status",
            "free_yn", "hidden_yn", "open_yn", "track_id", "mod_date", "update", "preview" };

        var table = CreateEmptyTable(path, "song_song", languageCode, columns);

        for (var i = 0; i < songs.Count; i++)
        {
            var song = songs[i];
            var row = NewRow(table, i);
            SetCell(row, "song_id", song.Id);
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

            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildPatternTable(string path, string? languageCode, List<Song> songs)
    {
        var columns = new[] { "pattern_id", "song_id", "name", "line", "signature",
            "difficulty", "level", "point_type", "point_value", "flg", "update" };

        var table = CreateEmptyTable(path, "song_songPattern", languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            foreach (var pattern in song.Patterns)
            {
                var row = NewRow(table, rowIndex++);
                SetCell(row, "pattern_id", pattern.PatternId);
                SetCell(row, "song_id", song.Id);
                SetCell(row, "name", pattern.Name);
                SetCell(row, "line", pattern.Line);
                SetCell(row, "signature", pattern.Signature);
                SetCell(row, "difficulty", pattern.Difficulty);
                SetCell(row, "level", pattern.Level);
                SetCell(row, "point_type", pattern.PointType);
                SetCell(row, "point_value", pattern.PointValue);
                SetCell(row, "flg", pattern.Flg);
                SetCell(row, "update", pattern.Update);
                table.Rows.Add(row);
            }
        }

        return table;
    }

    private static GameTable BuildSongDescTable(string path, string? languageCode, string tableName, List<Song> songs)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "song_id", "title", "description" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            var hasTitle = song.TitlesByLanguage.TryGetValue(language, out var title) && !string.IsNullOrWhiteSpace(title);
            var hasDesc = song.DescriptionsByLanguage.TryGetValue(language, out var description) && !string.IsNullOrWhiteSpace(description);
            if (!hasTitle && !hasDesc) continue;

            var row = NewRow(table, rowIndex++);
            SetCell(row, "song_id", song.Id);
            SetCell(row, "title", title ?? string.Empty);
            SetCell(row, "description", description ?? string.Empty);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildItemDescTable(string path, string? languageCode, string tableName, List<Song> songs)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "item_id", "name", "description" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var song in songs)
        {
            foreach (var itemId in song.ItemIds)
            {
                if (!emitted.Add(itemId)) continue;

                var hasName = song.ItemNamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrWhiteSpace(name);
                var hasDesc = song.DescriptionsByLanguage.TryGetValue(language, out var description) && !string.IsNullOrWhiteSpace(description);
                if (!hasName && !hasDesc) continue;

                var row = NewRow(table, rowIndex++);
                SetCell(row, "item_id", itemId);
                SetCell(row, "name", name ?? string.Empty);
                SetCell(row, "description", description ?? string.Empty);
                table.Rows.Add(row);
            }
        }

        return table;
    }

    private static GameTable BuildProductProductTable(string path, string? languageCode, List<Song> songs)
    {
        var columns = new[] { "product_id", "song_id" };
        var table = CreateEmptyTable(path, "product_product", languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            foreach (var productId in song.ProductIds)
            {
                var row = NewRow(table, rowIndex++);
                SetCell(row, "product_id", productId);
                SetCell(row, "song_id", song.Id);
                table.Rows.Add(row);
            }
        }

        return table;
    }

    private static GameTable BuildProductItemTable(string path, string? languageCode, List<Song> songs)
    {
        var columns = new[] { "product_id", "item_id" };
        var table = CreateEmptyTable(path, "product_item", languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            foreach (var productId in song.ProductIds)
            {
                foreach (var itemId in song.ItemIds)
                {
                    var row = NewRow(table, rowIndex++);
                    SetCell(row, "product_id", productId);
                    SetCell(row, "item_id", itemId);
                    table.Rows.Add(row);
                }
            }
        }

        return table;
    }

    private static GameTable BuildCategoryProductTable(string path, string? languageCode, List<Song> songs)
    {
        var columns = new[] { "category_id", "product_id" };
        var table = CreateEmptyTable(path, "category_categoryproduct", languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            foreach (var categoryId in song.CategoryIds)
            {
                foreach (var productId in song.ProductIds)
                {
                    var row = NewRow(table, rowIndex++);
                    SetCell(row, "category_id", categoryId);
                    SetCell(row, "product_id", productId);
                    table.Rows.Add(row);
                }
            }
        }

        return table;
    }

    // ── Achievement projection ──

    private static GameTable BuildAchievementTable(string path, string? languageCode, List<Achievement> achievements)
    {
        var columns = new[] { "achievement_id", "condition_type", "condition_value",
            "condition_count", "condition_special", "img_url", "achievement_tier",
            "obtain_point", "name", "pre_description", "after_description", "update" };

        var table = CreateEmptyTable(path, "quest_achievement", languageCode, columns);

        for (var i = 0; i < achievements.Count; i++)
        {
            var a = achievements[i];
            var row = NewRow(table, i);
            SetCell(row, "achievement_id", a.Id);
            SetCell(row, "condition_type", a.ConditionType);
            SetCell(row, "condition_value", a.ConditionValue);
            SetCell(row, "condition_count", a.ConditionCount);
            SetCell(row, "condition_special", a.ConditionSpecial);
            SetCell(row, "img_url", a.ImgUrl);
            SetCell(row, "achievement_tier", a.AchievementTier);
            SetCell(row, "obtain_point", a.ObtainPoint);
            SetCell(row, "name", a.Name);
            SetCell(row, "pre_description", a.PreDescription);
            SetCell(row, "after_description", a.AfterDescription);
            SetCell(row, "update", a.Update);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildAchievementDescTable(string path, string? languageCode, string tableName, List<Achievement> achievements)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "achievement_id", "achievement_name", "pre_description", "after_description" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var a in achievements)
        {
            var hasName = a.NamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasPre = a.PreDescriptionsByLanguage.TryGetValue(language, out var pre) && !string.IsNullOrWhiteSpace(pre);
            var hasAfter = a.AfterDescriptionsByLanguage.TryGetValue(language, out var after) && !string.IsNullOrWhiteSpace(after);
            if (!hasName && !hasPre && !hasAfter) continue;

            var row = NewRow(table, rowIndex++);
            SetCell(row, "achievement_id", a.Id);
            SetCell(row, "achievement_name", name ?? string.Empty);
            SetCell(row, "pre_description", pre ?? string.Empty);
            SetCell(row, "after_description", after ?? string.Empty);
            table.Rows.Add(row);
        }

        return table;
    }

    // ── Quest projection ──

    private static GameTable BuildQuestDescTable(string path, string? languageCode, string tableName, List<Quest> quests)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "quest_id", "quest_name", "description" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var q in quests)
        {
            var hasName = q.NamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasDesc = q.DescriptionsByLanguage.TryGetValue(language, out var desc) && !string.IsNullOrWhiteSpace(desc);
            if (!hasName && !hasDesc) continue;

            var row = NewRow(table, rowIndex++);
            SetCell(row, "quest_id", q.Id);
            SetCell(row, "quest_name", name ?? string.Empty);
            SetCell(row, "description", desc ?? string.Empty);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildQuestMissionDescTable(string path, string? languageCode, string tableName, List<Quest> quests)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "quest_mission_id", "description" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var q in quests)
        {
            for (var mi = 0; mi < q.Missions.Count; mi++)
            {
                if (!q.Missions[mi].DescriptionsByLanguage.TryGetValue(language, out var desc) || string.IsNullOrWhiteSpace(desc))
                    continue;

                var row = NewRow(table, rowIndex++);
                SetCell(row, "quest_mission_id", q.Id);
                SetCell(row, "description", desc);
                table.Rows.Add(row);
            }
        }

        return table;
    }

    // ── helpers ──

    private static GameTable CreateEmptyTable(string path, string tableName, string? languageCode, string[] columnNames)
    {
        var table = new GameTable
        {
            PackageRelativePath = path,
            TableName = tableName,
            LanguageCode = languageCode
        };
        for (var i = 0; i < columnNames.Length; i++)
            table.Columns.Add(new GameTableColumn(columnNames[i], i));
        return table;
    }

    private static GameTableRow NewRow(GameTable table, int order)
    {
        var row = new GameTableRow { Order = order };
        foreach (var column in table.Columns.OrderBy(c => c.Order))
            row.Cells.Add(new GameTableCell(column.Name, string.Empty));
        return row;
    }

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
    }

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

    private static string ExtractLanguageSuffix(string tableName)
    {
        var index = tableName.LastIndexOf('_');
        return index < 0 || index == tableName.Length - 1 ? string.Empty : tableName[(index + 1)..];
    }
}
