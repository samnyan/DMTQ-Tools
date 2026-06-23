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

        ProjectEntityTables(package, tables, SongCatalogService.IsProductRelatedTable,
            (path, lang, name) => name switch
            {
                "product_product" => BuildProductTable(path, lang, package.Products),
                "category_categoryproduct" => BuildCategoryProductTable(path, lang, package.Products),
                _ => null
            });

        ProjectEntityTables(package, tables, SongCatalogService.IsItemRelatedTable,
            (path, lang, name) => name switch
            {
                "product_item" => BuildProductItemTable(path, lang, package.Items),
                _ when name.StartsWith("item_desc_", StringComparison.OrdinalIgnoreCase)
                    => BuildItemDescTable(path, lang, name, package.Items),
                _ => null
            });

        ProjectEntityTables(package, tables, SongCatalogService.IsIngameItemRelatedTable,
            (path, lang, name) => name switch
            {
                "ingameitem_ingameitem" => BuildIngameItemTable(path, lang, package.IngameItems),
                "ingameitem_itemeffect" => BuildIngameItemEffectTable(path, lang, package.IngameItemEffects),
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
        var columns = new[] { "song_id", "fullname", "genre", "artist",
            "composed_by", "singer", "feat_by", "arranged_by", "visualized_by" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var song in songs)
        {
            if (!song.Localizations.TryGetValue(language, out var loc)) continue;

            var row = NewRow(table, rowIndex++);
            SetCell(row, "song_id", song.Id);
            SetCell(row, "fullname", loc.FullName);
            SetCell(row, "genre", loc.Genre);
            SetCell(row, "artist", loc.ArtistName);
            SetCell(row, "composed_by", loc.ComposedBy);
            SetCell(row, "singer", loc.Singer);
            SetCell(row, "feat_by", loc.FeatBy);
            SetCell(row, "arranged_by", loc.ArrangedBy);
            SetCell(row, "visualized_by", loc.VisualizedBy);
            table.Rows.Add(row);
        }

        return table;
    }

    // ── Product projection ──

    private static GameTable BuildProductTable(string path, string? languageCode, List<Product> products)
    {
        var columns = new[] { "product_id", "item_id", "platform_product_id",
            "store_product_id", "product_type", "cost_game_point", "cost_game_cash",
            "status", "sale_start_date", "sale_end_date", "update" };

        var table = CreateEmptyTable(path, "product_product", languageCode, columns);

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var row = NewRow(table, i);
            SetCell(row, "product_id", p.Id);
            SetCell(row, "item_id", p.ItemId);
            SetCell(row, "platform_product_id", p.PlatformProductId);
            SetCell(row, "store_product_id", p.StoreProductId);
            SetCell(row, "product_type", p.ProductType);
            SetCell(row, "cost_game_point", p.CostGamePoint);
            SetCell(row, "cost_game_cash", p.CostGameCash);
            SetCell(row, "status", p.Status);
            SetCell(row, "sale_start_date", p.SaleStartDate);
            SetCell(row, "sale_end_date", p.SaleEndDate);
            SetCell(row, "update", p.Update);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildCategoryProductTable(string path, string? languageCode, List<Product> products)
    {
        var columns = new[] { "category_id", "product_id" };
        var table = CreateEmptyTable(path, "category_categoryproduct", languageCode, columns);
        var rowIndex = 0;

        foreach (var product in products)
        {
            foreach (var categoryId in product.CategoryIds)
            {
                var row = NewRow(table, rowIndex++);
                SetCell(row, "category_id", categoryId);
                SetCell(row, "product_id", product.Id);
                table.Rows.Add(row);
            }
        }

        return table;
    }

    // ── Item projection ──

    private static GameTable BuildProductItemTable(string path, string? languageCode, List<Item> items)
    {
        var columns = new[] { "item_id", "item_name", "img_url_1", "img_url_2",
            "description", "repeat_count", "item_type", "limit_minute",
            "status", "buy_level", "buy_limit_count", "buy_limit_type", "summary", "update" };

        var table = CreateEmptyTable(path, "product_item", languageCode, columns);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = NewRow(table, i);
            SetCell(row, "item_id", item.Id);
            SetCell(row, "item_name", item.ItemName);
            SetCell(row, "img_url_1", item.ImgUrl1);
            SetCell(row, "img_url_2", item.ImgUrl2);
            SetCell(row, "description", item.Description);
            SetCell(row, "repeat_count", item.RepeatCount);
            SetCell(row, "item_type", item.ItemType);
            SetCell(row, "limit_minute", item.LimitMinute);
            SetCell(row, "status", item.Status);
            SetCell(row, "buy_level", item.BuyLevel);
            SetCell(row, "buy_limit_count", item.BuyLimitCount);
            SetCell(row, "buy_limit_type", item.BuyLimitType);
            SetCell(row, "summary", item.Summary);
            SetCell(row, "update", item.Update);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildItemDescTable(string path, string? languageCode, string tableName, List<Item> items)
    {
        var language = languageCode ?? ExtractLanguageSuffix(tableName);
        var columns = new[] { "item_id", "name", "description", "summary" };

        var table = CreateEmptyTable(path, tableName, languageCode, columns);
        var rowIndex = 0;

        foreach (var item in items)
        {
            var hasName = item.NamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrWhiteSpace(name);
            var hasDesc = item.DescriptionsByLanguage.TryGetValue(language, out var desc) && !string.IsNullOrWhiteSpace(desc);
            var hasSummary = item.SummariesByLanguage.TryGetValue(language, out var summary) && !string.IsNullOrWhiteSpace(summary);
            if (!hasName && !hasDesc && !hasSummary) continue;

            var row = NewRow(table, rowIndex++);
            SetCell(row, "item_id", item.Id);
            SetCell(row, "name", name ?? string.Empty);
            SetCell(row, "description", desc ?? string.Empty);
            SetCell(row, "summary", summary ?? string.Empty);
            table.Rows.Add(row);
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

    // ── IngameItem projection ──

    private static GameTable BuildIngameItemTable(string path, string? languageCode, List<IngameItem> items)
    {
        var columns = new[] { "item_type", "item_level", "product_id", "update" };
        var table = CreateEmptyTable(path, "ingameitem_ingameitem", languageCode, columns);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = NewRow(table, i);
            SetCell(row, "item_type", item.ItemType);
            SetCell(row, "item_level", item.ItemLevel);
            SetCell(row, "product_id", item.ProductId);
            SetCell(row, "update", item.Update);
            table.Rows.Add(row);
        }

        return table;
    }

    private static GameTable BuildIngameItemEffectTable(string path, string? languageCode, List<IngameItemEffect> effects)
    {
        var columns = new[] { "item_id", "effect_type", "effect_point",
            "effect_count", "effect_special", "update" };
        var table = CreateEmptyTable(path, "ingameitem_itemeffect", languageCode, columns);

        for (var i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            var row = NewRow(table, i);
            SetCell(row, "item_id", e.Id);
            SetCell(row, "effect_type", e.EffectType);
            SetCell(row, "effect_point", e.EffectPoint);
            SetCell(row, "effect_count", e.EffectCount);
            SetCell(row, "effect_special", e.EffectSpecial);
            SetCell(row, "update", e.Update);
            table.Rows.Add(row);
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
