using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using DMTQ.Tools.Core.Models.Csv;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ_Tools.Components.Services;

/// <summary>Creates an Excel workbook for the platform-independent song list.</summary>
public static class SongListExcelExporter
{
    private static readonly (int Line, string Name)[] ChartLines = [(3, "2Line"), (4, "3Line"), (5, "4Line")];
    private static readonly (int Difficulty, string Name)[] ChartDifficulties =
        [(0, "Easy"), (1, "Normal"), (2, "Hard"), (3, "Expert")];

    public static byte[] Export(
        PatchPackage package,
        IEnumerable<(string AtlasName, string SpriteName)> coverSprites)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(coverSprites);

        var android = package.GetPlatformTables("android");
        var ios = package.GetPlatformTables("ios");
        var songs = BuildSongSheet(android, ios);
        var products = BuildProductSheet(android.Products.Concat(ios.Products)
            .DistinctBy(product => product.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray());
        var sprites = BuildCoverSheet(coverSprites);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", WriteContentTypes);
            WriteEntry(archive, "_rels/.rels", WriteRootRelationships);
            WriteEntry(archive, "xl/workbook.xml", writer => WriteWorkbook(writer, ["歌曲", "商品", "封面贴图"]));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
            WriteEntry(archive, "xl/styles.xml", WriteStyles);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", writer => WriteSheet(writer, songs, freezeColumns: 3));
            WriteEntry(archive, "xl/worksheets/sheet2.xml", writer => WriteSheet(writer, products, freezeColumns: 1));
            WriteEntry(archive, "xl/worksheets/sheet3.xml", writer => WriteSheet(writer, sprites, freezeColumns: 1));
        }

        return stream.ToArray();
    }

    private static SheetData BuildSongSheet(PlatformTableDataView android, PlatformTableDataView ios)
    {
        var schema = new SongCsvSchema();
        var columns = schema.Columns.OrderBy(column => column.Order).ToArray();
        var headers = new List<string>(columns.Select(column => SongHeaderNames[column.ColumnName]))
        {
            "封面贴图名"
        };
        foreach (var (line, lineName) in ChartLines)
        foreach (var (difficulty, difficultyName) in ChartDifficulties)
            headers.Add($"{LineNames[line]} {DifficultyNames[difficulty]}");

        var itemsById = android.Items.Concat(ios.Items)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ImgUrl1)) ?? group.First(),
                StringComparer.OrdinalIgnoreCase);
        // Android supplies shared song metadata when IDs exist on both platforms; chart IDs are merged below.
        var songsById = android.Songs.Concat(ios.Songs)
            .GroupBy(song => song.Id)
            .OrderBy(group => group.Key);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var platformSongs in songsById)
        {
            var song = platformSongs.First();
            var row = columns.Select(column => column.Getter(song)).ToList();
            var itemId = song.ItemId.ToString(CultureInfo.InvariantCulture);
            row.Add(itemsById.TryGetValue(itemId, out var item) ? item.ImgUrl1 : string.Empty);

            foreach (var (line, _) in ChartLines)
            foreach (var (difficulty, _) in ChartDifficulties)
            {
                var patternIds = platformSongs.SelectMany(item => item.Patterns)
                    .Where(pattern => pattern.Line == line && pattern.Signature == difficulty)
                    .Select(pattern => pattern.PatternId)
                    .Distinct()
                    .Order()
                    .Select(patternId => patternId.ToString(CultureInfo.InvariantCulture));
                row.Add(string.Join(", ", patternIds));
            }

            rows.Add(row);
        }

        return new SheetData(headers, rows);
    }

    private static readonly IReadOnlyDictionary<string, string> SongHeaderNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["song_id"] = "歌曲ID",
            ["item_id"] = "道具ID",
            ["name"] = "歌曲名",
            ["full_name"] = "完整歌曲名",
            ["genre"] = "曲风",
            ["artist_name"] = "艺术家",
            ["original_bga_yn"] = "原版BGA",
            ["loop_bga_yn"] = "循环BGA",
            ["composed_by"] = "作曲",
            ["singer"] = "歌手",
            ["feat_by"] = "合作艺人",
            ["arranged_by"] = "编曲",
            ["visualized_by"] = "影像制作",
            ["cost_game_point"] = "游戏点数价格",
            ["cost_game_cash"] = "游戏现金价格",
            ["flag"] = "标志",
            ["status"] = "状态",
            ["free_yn"] = "免费",
            ["hidden_yn"] = "隐藏",
            ["open_yn"] = "开放",
            ["track_id"] = "音轨ID",
            ["mod_date"] = "修改日期",
            ["update"] = "更新标记"
        };

    private static readonly IReadOnlyDictionary<int, string> LineNames = new Dictionary<int, string>
    {
        [3] = "2线", [4] = "3线", [5] = "4线"
    };

    private static readonly IReadOnlyDictionary<int, string> DifficultyNames = new Dictionary<int, string>
    {
        [0] = "简单", [1] = "普通", [2] = "困难", [3] = "专家"
    };

    private static SheetData BuildProductSheet(IReadOnlyList<Product> products)
    {
        var schema = new ProductCsvSchema();
        var columns = schema.Columns.OrderBy(column => column.Order).ToArray();
        return new SheetData(
            columns.Select(column => ProductHeaderNames[column.ColumnName]).ToArray(),
            products.Select(product => (IReadOnlyList<string>)columns.Select(column => column.Getter(product)).ToArray())
                .ToArray());
    }

    private static readonly IReadOnlyDictionary<string, string> ProductHeaderNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["product_id"] = "商品ID",
            ["item_id"] = "道具ID",
            ["platform_product_id"] = "平台商品ID",
            ["store_product_id"] = "商店商品ID",
            ["product_type"] = "商品类型",
            ["cost_game_point"] = "游戏点数价格",
            ["cost_game_cash"] = "游戏现金价格",
            ["status"] = "状态",
            ["sale_start_date"] = "销售开始时间",
            ["sale_end_date"] = "销售结束时间",
            ["update"] = "更新标记"
        };

    private static SheetData BuildCoverSheet(IEnumerable<(string AtlasName, string SpriteName)> coverSprites)
    {
        var rows = coverSprites
            .Where(sprite => !string.IsNullOrWhiteSpace(sprite.AtlasName) && !string.IsNullOrWhiteSpace(sprite.SpriteName))
            .Distinct()
            .OrderBy(sprite => sprite.AtlasName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.SpriteName, StringComparer.OrdinalIgnoreCase)
            .Select(sprite => (IReadOnlyList<string>)[sprite.AtlasName, sprite.SpriteName])
            .ToArray();
        return new SheetData(["图包名", "贴图名"], rows);
    }

    private static void WriteSheet(XmlWriter writer, SheetData sheet, int freezeColumns)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        if (freezeColumns > 0) writer.WriteAttributeString("xSplit", freezeColumns.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", CellReference(freezeColumns + 1, 2));
        writer.WriteAttributeString("activePane", "bottomRight");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < sheet.Headers.Count; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            var headerLength = sheet.Headers[index].Length;
            writer.WriteAttributeString("width", Math.Clamp(headerLength + 2, 14, 26).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteRow(writer, 1, sheet.Headers, isHeader: true);
        for (var index = 0; index < sheet.Rows.Count; index++)
            WriteRow(writer, index + 2, sheet.Rows[index], isHeader: false);
        writer.WriteEndElement();

        if (sheet.Headers.Count > 0)
        {
            writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:{CellReference(sheet.Headers.Count, Math.Max(1, sheet.Rows.Count + 1))}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowIndex, IReadOnlyList<string> values, bool isHeader)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
        for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
        {
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", CellReference(columnIndex + 1, rowIndex));
            writer.WriteAttributeString("t", "inlineStr");
            if (isHeader) writer.WriteAttributeString("s", "1");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(values[columnIndex] ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string CellReference(int column, int row)
    {
        var name = string.Empty;
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }
        return name + row.ToString(CultureInfo.InvariantCulture);
    }

    private static void WriteWorkbook(XmlWriter writer, IReadOnlyList<string> sheetNames)
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipsNamespace);
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        for (var index = 0; index < sheetNames.Count; index++)
        {
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", sheetNames[index]);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", RelationshipsNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var index = 1; index <= 3; index++)
            WriteRelationship(writer, $"rId{index}", $"worksheets/sheet{index}.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
        WriteRelationship(writer, "rId4", "styles.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
        writer.WriteEndElement();
    }

    private static void WriteRootRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        WriteRelationship(writer, "rId1", "xl/workbook.xml", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteEndElement();
    }

    private static void WriteRelationship(XmlWriter writer, string id, string target, string type)
    {
        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static void WriteContentTypes(XmlWriter writer)
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteContentType(writer, "Default", "Extension", "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteContentType(writer, "Default", "Extension", "xml", "application/xml");
        WriteContentType(writer, "Override", "PartName", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        WriteContentType(writer, "Override", "PartName", "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        for (var index = 1; index <= 3; index++)
            WriteContentType(writer, "Override", "PartName", $"/xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        writer.WriteEndElement();
    }

    private static void WriteContentType(XmlWriter writer, string element, string keyAttribute, string key, string contentType)
    {
        writer.WriteStartElement(element, ContentTypesNamespace);
        writer.WriteAttributeString(keyAttribute, key);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("fonts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        writer.WriteStartElement("font", SpreadsheetNamespace); writer.WriteEndElement();
        writer.WriteStartElement("font", SpreadsheetNamespace);
        writer.WriteElementString("b", SpreadsheetNamespace, string.Empty);
        writer.WriteStartElement("color", SpreadsheetNamespace); writer.WriteAttributeString("rgb", "FFFFFFFF"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("fills", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "3");
        writer.WriteStartElement("fill", SpreadsheetNamespace); writer.WriteElementString("patternFill", SpreadsheetNamespace, string.Empty); writer.WriteEndElement();
        writer.WriteStartElement("fill", SpreadsheetNamespace); writer.WriteElementString("patternFill", SpreadsheetNamespace, string.Empty); writer.WriteEndElement();
        writer.WriteStartElement("fill", SpreadsheetNamespace);
        writer.WriteStartElement("patternFill", SpreadsheetNamespace); writer.WriteAttributeString("patternType", "solid");
        writer.WriteStartElement("fgColor", SpreadsheetNamespace); writer.WriteAttributeString("rgb", "FF315B7D"); writer.WriteEndElement();
        writer.WriteStartElement("bgColor", SpreadsheetNamespace); writer.WriteAttributeString("indexed", "64"); writer.WriteEndElement();
        writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("borders", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("border", SpreadsheetNamespace);
        writer.WriteElementString("left", SpreadsheetNamespace, string.Empty);
        writer.WriteElementString("right", SpreadsheetNamespace, string.Empty);
        writer.WriteElementString("top", SpreadsheetNamespace, string.Empty);
        writer.WriteElementString("bottom", SpreadsheetNamespace, string.Empty);
        writer.WriteElementString("diagonal", SpreadsheetNamespace, string.Empty);
        writer.WriteEndElement(); writer.WriteEndElement();

        writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("xf", SpreadsheetNamespace); writer.WriteAttributeString("numFmtId", "0"); writer.WriteAttributeString("fontId", "0"); writer.WriteAttributeString("fillId", "0"); writer.WriteAttributeString("borderId", "0"); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteStartElement("cellXfs", SpreadsheetNamespace); writer.WriteAttributeString("count", "2");
        writer.WriteStartElement("xf", SpreadsheetNamespace); writer.WriteAttributeString("numFmtId", "0"); writer.WriteAttributeString("fontId", "0"); writer.WriteAttributeString("fillId", "0"); writer.WriteAttributeString("borderId", "0"); writer.WriteAttributeString("xfId", "0"); writer.WriteEndElement();
        writer.WriteStartElement("xf", SpreadsheetNamespace); writer.WriteAttributeString("numFmtId", "0"); writer.WriteAttributeString("fontId", "1"); writer.WriteAttributeString("fillId", "2"); writer.WriteAttributeString("borderId", "0"); writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("applyFont", "1"); writer.WriteAttributeString("applyFill", "1"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyles", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle", SpreadsheetNamespace); writer.WriteAttributeString("name", "Normal"); writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteEntry(ZipArchive archive, string name, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false });
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

    private sealed record SheetData(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
}
