using DMTQ.Tools.Core.Models;

namespace DMTQ.Tools.Core.Services;

public sealed class SongCatalogService
{
    public IReadOnlyList<Song> BuildCatalog(PatchPackage package, bool forceFromTables = false)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!forceFromTables && package.Songs.Count > 0)
        {
            return package.Songs
                .OrderBy(song => song.GetTitle("us"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(song => song.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

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
            MapSongCells(row, song);
            result[songId] = song;
        }

        return result;
    }

    private static void MapSongCells(GameTableRow row, Song song)
    {
        song.ItemId = GetCell(row, "item_id");
        song.Name = GetCell(row, "name");
        song.FullName = GetCell(row, "full_name");
        song.Genre = GetCell(row, "genre");
        song.ArtistName = GetCell(row, "artist_name");
        song.OriginalBgaYn = GetCell(row, "original_bga_yn");
        song.LoopBgaYn = GetCell(row, "loop_bga_yn");
        song.ComposedBy = GetCell(row, "composed_by");
        song.Singer = GetCell(row, "singer");
        song.FeatBy = GetCell(row, "feat_by");
        song.ArrangedBy = GetCell(row, "arranged_by");
        song.VisualizedBy = GetCell(row, "visualized_by");
        song.CostGamePoint = GetCell(row, "cost_game_point");
        song.CostGameCash = GetCell(row, "cost_game_cash");
        song.Flag = GetCell(row, "flag");
        song.Status = GetCell(row, "status");
        song.FreeYn = GetCell(row, "free_yn");
        song.HiddenYn = GetCell(row, "hidden_yn");
        song.OpenYn = GetCell(row, "open_yn");
        song.TrackId = GetCell(row, "track_id");
        song.ModDate = GetCell(row, "mod_date");
        song.Update = GetCell(row, "update");
    }

    private static void AddPatternRows(PatchPackage package, Dictionary<string, Song> songs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                var key = songId + "::" + patternId;
                if (!seen.Add(key))
                {
                    continue;
                }

                var pattern = new SongPattern
                {
                    PatternId = patternId,
                    SongId = songId
                };
                MapPatternCells(row, pattern);
                song.Patterns.Add(pattern);
            }
        }

        foreach (var song in songs.Values)
        {
            song.Patterns.Sort((left, right) =>
            {
                var lineCmp = string.Compare(left.Line, right.Line, StringComparison.OrdinalIgnoreCase);
                return lineCmp != 0 ? lineCmp
                    : string.Compare(left.Signature, right.Signature, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    private static void MapPatternCells(GameTableRow row, SongPattern pattern)
    {
        pattern.Name = GetCell(row, "name", "pattern_name");
        pattern.Line = GetCell(row, "line");
        pattern.Signature = GetCell(row, "signature", "sig");
        pattern.Difficulty = GetCell(row, "difficulty", "difficulty_type", "diff");
        pattern.Level = GetCell(row, "level", "level_text", "rating");
        pattern.PointType = GetCell(row, "point_type");
        pattern.PointValue = GetCell(row, "point_value");
        pattern.Flg = GetCell(row, "flg");
        pattern.Update = GetCell(row, "update");
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
            if (!string.IsNullOrWhiteSpace(song.Name) && previewPaths.Contains(song.Name))
            {
                song.PreviewPackageRelativePath = song.Name;
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

    /// <summary>Returns true when the table carries data that is owned by Song entities
    /// and should be removed from raw GameTable storage after entity extraction.</summary>
    public static bool IsSongRelatedTable(string tableName)
        => tableName.Equals("song_song", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("song_songPattern", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("song_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Product data
    /// (product_product + category_categoryproduct).</summary>
    public static bool IsProductRelatedTable(string tableName)
        => tableName.Equals("product_product", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("category_categoryproduct", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Item data
    /// (product_item + item_desc_&lt;lang&gt;).</summary>
    public static bool IsItemRelatedTable(string tableName)
        => tableName.Equals("product_item", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("item_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries IngameItem data
    /// (ingameitem_ingameitem + ingameitem_itemeffect).</summary>
    public static bool IsIngameItemRelatedTable(string tableName)
        => tableName.Equals("ingameitem_ingameitem", StringComparison.OrdinalIgnoreCase)
           || tableName.Equals("ingameitem_itemeffect", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Achievement data
    /// (quest_achievement + acievement_desc_&lt;lang&gt;).</summary>
    public static bool IsAchievementRelatedTable(string tableName)
        => tableName.Equals("quest_achievement", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("acievement_desc_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when the table carries Quest data
    /// (quest_desc_&lt;lang&gt; + quest_mission_desc_&lt;lang&gt;).</summary>
    public static bool IsQuestRelatedTable(string tableName)
        => tableName.StartsWith("quest_desc_", StringComparison.OrdinalIgnoreCase)
           || tableName.StartsWith("quest_mission_desc_", StringComparison.OrdinalIgnoreCase);

    // ── Achievement catalog building ──

    public IReadOnlyList<Achievement> BuildAchievementCatalog(PatchPackage package)
    {
        if (package.Achievements.Count > 0)
            return package.Achievements.OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        var mainTable = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("quest_achievement", StringComparison.OrdinalIgnoreCase));
        if (mainTable is null) return [];

        var achievements = new Dictionary<string, Achievement>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in mainTable.Rows.OrderBy(r => r.Order))
        {
            var id = GetCell(row, "achievement_id", "id");
            if (string.IsNullOrWhiteSpace(id) || achievements.ContainsKey(id)) continue;

            var achievement = new Achievement { Id = id };
            achievement.ConditionType = GetCell(row, "condition_type");
            achievement.ConditionValue = GetCell(row, "condition_value");
            achievement.ConditionCount = GetCell(row, "condition_count");
            achievement.ConditionSpecial = GetCell(row, "condition_special");
            achievement.ImgUrl = GetCell(row, "img_url");
            achievement.AchievementTier = GetCell(row, "achievement_tier");
            achievement.ObtainPoint = GetCell(row, "obtain_point");
            achievement.Name = GetCell(row, "name");
            achievement.PreDescription = GetCell(row, "pre_description");
            achievement.AfterDescription = GetCell(row, "after_description");
            achievement.Update = GetCell(row, "update");
            achievements[id] = achievement;
        }

        // Localized descriptions from acievement_desc_<lang>
        foreach (var table in package.Tables.Tables.Where(
            t => t.TableName.StartsWith("acievement_desc_", StringComparison.OrdinalIgnoreCase)))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language)) continue;

            foreach (var row in table.Rows)
            {
                var id = GetCell(row, "achievement_id", "id");
                if (!achievements.TryGetValue(id, out var achievement)) continue;

                var name = GetCell(row, "achievement_name", "name");
                var preDesc = GetCell(row, "pre_description");
                var afterDesc = GetCell(row, "after_description");

                if (!string.IsNullOrWhiteSpace(name)) achievement.NamesByLanguage[language] = name;
                if (!string.IsNullOrWhiteSpace(preDesc)) achievement.PreDescriptionsByLanguage[language] = preDesc;
                if (!string.IsNullOrWhiteSpace(afterDesc)) achievement.AfterDescriptionsByLanguage[language] = afterDesc;
            }
        }

        return achievements.Values.OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── Quest catalog building ──

    public IReadOnlyList<Quest> BuildQuestCatalog(PatchPackage package)
    {
        if (package.Quests.Count > 0)
            return package.Quests.OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        // Localized quest names / descriptions from quest_desc_<lang>
        var quests = new Dictionary<string, Quest>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables.Tables.Where(
            t => t.TableName.StartsWith("quest_desc_", StringComparison.OrdinalIgnoreCase)))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language)) continue;

            foreach (var row in table.Rows)
            {
                var id = GetCell(row, "quest_id", "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!quests.TryGetValue(id, out var quest))
                {
                    quest = new Quest { Id = id };
                    quests[id] = quest;
                }

                var name = GetCell(row, "quest_name", "name");
                var desc = GetCell(row, "description", "desc");
                if (!string.IsNullOrWhiteSpace(name)) quest.NamesByLanguage[language] = name;
                if (!string.IsNullOrWhiteSpace(desc)) quest.DescriptionsByLanguage[language] = desc;
            }
        }

        // Mission descriptions from quest_mission_desc_<lang>
        var missionRowsByQuest = new Dictionary<string, List<(string Language, string Description)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables.Tables.Where(
            t => t.TableName.StartsWith("quest_mission_desc_", StringComparison.OrdinalIgnoreCase)))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language)) continue;

            foreach (var row in table.Rows.OrderBy(r => r.Order))
            {
                var questId = GetCell(row, "quest_mission_id", "quest_id", "id");
                if (string.IsNullOrWhiteSpace(questId)) continue;

                var desc = GetCell(row, "description", "desc");
                if (string.IsNullOrWhiteSpace(desc)) continue;

                if (!missionRowsByQuest.ContainsKey(questId))
                    missionRowsByQuest[questId] = [];
                missionRowsByQuest[questId].Add((language, desc));
            }
        }

        // Align missions across languages by position
        foreach (var (questId, rows) in missionRowsByQuest)
        {
            if (!quests.TryGetValue(questId, out var quest)) continue;

            var missionsByLang = rows.GroupBy(r => r.Language).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            var primaryLang = missionsByLang.Keys.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(primaryLang)) continue;

            var count = missionsByLang[primaryLang].Length;
            for (var i = 0; i < count; i++)
            {
                var mission = new QuestMission();
                foreach (var (lang, langRows) in missionsByLang)
                {
                    if (i < langRows.Length)
                        mission.DescriptionsByLanguage[lang] = langRows[i].Description;
                }
                quest.Missions.Add(mission);
            }
        }

        return quests.Values.OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── Product catalog building ──

    public IReadOnlyList<Product> BuildProductCatalog(PatchPackage package)
    {
        if (package.Products.Count > 0)
            return package.Products.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        var productTable = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("product_product", StringComparison.OrdinalIgnoreCase));
        if (productTable is null) return [];

        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in productTable.Rows.OrderBy(r => r.Order))
        {
            var id = GetCell(row, "product_id", "id");
            if (string.IsNullOrWhiteSpace(id) || products.ContainsKey(id)) continue;

            products[id] = new Product
            {
                Id = id,
                ItemId = GetCell(row, "item_id"),
                PlatformProductId = GetCell(row, "platform_product_id"),
                StoreProductId = GetCell(row, "store_product_id"),
                ProductType = GetCell(row, "product_type"),
                CostGamePoint = GetCell(row, "cost_game_point"),
                CostGameCash = GetCell(row, "cost_game_cash"),
                Status = GetCell(row, "status"),
                SaleStartDate = GetCell(row, "sale_start_date"),
                SaleEndDate = GetCell(row, "sale_end_date"),
                Update = GetCell(row, "update"),
            };
        }

        // category_categoryproduct → CategoryIds
        var categoryTable = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("category_categoryproduct", StringComparison.OrdinalIgnoreCase));
        if (categoryTable is not null)
        {
            foreach (var row in categoryTable.Rows)
            {
                var productId = GetCell(row, "product_id");
                var categoryId = GetCell(row, "category_id");
                if (!string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(categoryId)
                    && products.TryGetValue(productId, out var product))
                {
                    if (!product.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase))
                        product.CategoryIds.Add(categoryId);
                }
            }
        }

        return products.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── Item catalog building ──

    public IReadOnlyList<Item> BuildItemCatalog(PatchPackage package)
    {
        if (package.Items.Count > 0)
            return package.Items.OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        var itemTable = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("product_item", StringComparison.OrdinalIgnoreCase));
        if (itemTable is null) return [];

        var items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in itemTable.Rows.OrderBy(r => r.Order))
        {
            var id = GetCell(row, "item_id", "id");
            if (string.IsNullOrWhiteSpace(id) || items.ContainsKey(id)) continue;

            items[id] = new Item
            {
                Id = id,
                ItemName = GetCell(row, "item_name"),
                ImgUrl1 = GetCell(row, "img_url_1"),
                ImgUrl2 = GetCell(row, "img_url_2"),
                Description = GetCell(row, "description"),
                RepeatCount = GetCell(row, "repeat_count"),
                ItemType = GetCell(row, "item_type"),
                LimitMinute = GetCell(row, "limit_minute"),
                Status = GetCell(row, "status"),
                BuyLevel = GetCell(row, "buy_level"),
                BuyLimitCount = GetCell(row, "buy_limit_count"),
                BuyLimitType = GetCell(row, "buy_limit_type"),
                Summary = GetCell(row, "summary"),
                Update = GetCell(row, "update"),
            };
        }

        // item_desc_<lang> localization
        foreach (var table in package.Tables.Tables.Where(
            t => t.TableName.StartsWith("item_desc_", StringComparison.OrdinalIgnoreCase)))
        {
            var language = table.LanguageCode ?? ExtractLanguage(table.TableName);
            if (string.IsNullOrWhiteSpace(language)) continue;

            foreach (var row in table.Rows)
            {
                var id = GetCell(row, "item_id", "id");
                if (!items.TryGetValue(id, out var item)) continue;

                var name = GetCell(row, "name");
                var desc = GetCell(row, "description", "desc");
                var summary = GetCell(row, "summary");

                if (!string.IsNullOrWhiteSpace(name)) item.NamesByLanguage[language] = name;
                if (!string.IsNullOrWhiteSpace(desc)) item.DescriptionsByLanguage[language] = desc;
                if (!string.IsNullOrWhiteSpace(summary)) item.SummariesByLanguage[language] = summary;
            }
        }

        return items.Values.OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── IngameItem catalog building ──

    public IReadOnlyList<IngameItem> BuildIngameItemCatalog(PatchPackage package)
    {
        if (package.IngameItems.Count > 0)
            return package.IngameItems.OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        var table = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("ingameitem_ingameitem", StringComparison.OrdinalIgnoreCase));
        if (table is null) return [];

        var items = new Dictionary<string, IngameItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows.OrderBy(r => r.Order))
        {
            var itemType = GetCell(row, "item_type");
            var itemLevel = GetCell(row, "item_level");
            var id = itemType + "_" + itemLevel;
            if (string.IsNullOrWhiteSpace(id) || items.ContainsKey(id)) continue;

            items[id] = new IngameItem
            {
                Id = id,
                ItemType = itemType,
                ItemLevel = itemLevel,
                ProductId = GetCell(row, "product_id"),
                Update = GetCell(row, "update"),
            };
        }

        return items.Values.OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<IngameItemEffect> BuildIngameItemEffectCatalog(PatchPackage package)
    {
        if (package.IngameItemEffects.Count > 0)
            return package.IngameItemEffects.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        var table = package.Tables.Tables.FirstOrDefault(
            t => t.TableName.Equals("ingameitem_itemeffect", StringComparison.OrdinalIgnoreCase));
        if (table is null) return [];

        var effects = new Dictionary<string, IngameItemEffect>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows.OrderBy(r => r.Order))
        {
            var id = GetCell(row, "item_id", "id");
            if (string.IsNullOrWhiteSpace(id) || effects.ContainsKey(id)) continue;

            effects[id] = new IngameItemEffect
            {
                Id = id,
                EffectType = GetCell(row, "effect_type"),
                EffectPoint = GetCell(row, "effect_point"),
                EffectCount = GetCell(row, "effect_count"),
                EffectSpecial = GetCell(row, "effect_special"),
                Update = GetCell(row, "update"),
            };
        }

        return effects.Values.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
