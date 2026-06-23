using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Models.Csv;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Models.Csv;

[TestClass]
public sealed class EntitySchemaTests
{
    // ── SongCsvSchema ──

    [TestMethod]
    public void SongCsvSchema_WriteCsv_ThenReadCsv_RoundTrips()
    {
        // Arrange
        var schema = new SongCsvSchema();
        var songs = new List<Song>
        {
            new() { Id = 1001, ItemId = 2001, Name = "Test Song", FullName = "Test Song Full",
                Genre = "POP", ArtistName = "Artist1", OriginalBgaYn = "Y", LoopBgaYn = "N",
                ComposedBy = "Composer1", Singer = "Singer1", FeatBy = "Feat1",
                ArrangedBy = "Arranger1", VisualizedBy = "Visual1",
                CostGamePoint = 100, CostGameCash = 200, Flag = 0, Status = "1",
                FreeYn = "N", HiddenYn = "N", OpenYn = "Y", TrackId = 3001,
                ModDate = "2024-01-01", Update = "1" },
            new() { Id = 1002, ItemId = 2002, Name = "Song 2", FullName = "Song 2 Full",
                Genre = "ROCK", ArtistName = "Artist2", OriginalBgaYn = "N", LoopBgaYn = "Y",
                ComposedBy = "Composer2", Singer = "Singer2", FeatBy = "",
                ArrangedBy = "Arranger2", VisualizedBy = "",
                CostGamePoint = 50, CostGameCash = 0, Flag = 1, Status = "1",
                FreeYn = "Y", HiddenYn = "N", OpenYn = "Y", TrackId = 3002,
                ModDate = "2024-06-15", Update = "2" },
        };

        // Act — write
        using var writeStream = new MemoryStream();
        schema.WriteCsv(writeStream, songs);

        // Act — read back
        var csvBytes = writeStream.ToArray();
        using var readStream = new MemoryStream(csvBytes);
        var result = schema.ReadCsv(readStream);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1001);
        result[0].Name.Should().Be("Test Song");
        result[0].FullName.Should().Be("Test Song Full");
        result[0].Genre.Should().Be("POP");
        result[0].ArtistName.Should().Be("Artist1");
        result[0].OriginalBgaYn.Should().Be("Y");
        result[0].LoopBgaYn.Should().Be("N");
        result[0].ComposedBy.Should().Be("Composer1");
        result[0].Singer.Should().Be("Singer1");
        result[0].FeatBy.Should().Be("Feat1");
        result[0].ArrangedBy.Should().Be("Arranger1");
        result[0].VisualizedBy.Should().Be("Visual1");
        result[0].CostGamePoint.Should().Be(100);
        result[0].CostGameCash.Should().Be(200);
        result[0].Flag.Should().Be(0);
        result[0].Status.Should().Be("1");
        result[0].FreeYn.Should().Be("N");
        result[0].HiddenYn.Should().Be("N");
        result[0].OpenYn.Should().Be("Y");
        result[0].TrackId.Should().Be(3001);
        result[0].ModDate.Should().Be("2024-01-01");
        result[0].Update.Should().Be("1");

        result[1].Id.Should().Be(1002);
        result[1].Name.Should().Be("Song 2");
    }

    [TestMethod]
    public void SongCsvSchema_ReadCsv_DeduplicatesById()
    {
        // Arrange
        var schema = new SongCsvSchema();
        var csv =
            "song_id,item_id,name,full_name,genre,artist_name,original_bga_yn,loop_bga_yn," +
            "composed_by,singer,feat_by,arranged_by,visualized_by,cost_game_point,cost_game_cash," +
            "flag,status,free_yn,hidden_yn,open_yn,track_id,mod_date,update\r\n" +
            "1001,2001,A,,POP,,,,,,,,,0,0,0,1,N,N,Y,3001,,\r\n" +
            "1002,2002,B,,ROCK,,,,,,,,,0,0,0,1,N,N,Y,3002,,\r\n" +
            "1001,2003,ADuplicate,,POP,,,,,,,,,0,0,0,1,N,N,Y,3003,,\r\n";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        var result = schema.ReadCsv(stream);

        // Assert — the duplicate SONG_001 row is skipped
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1001);
        result[1].Id.Should().Be(1002);
    }

    [TestMethod]
    public void SongCsvSchema_WriteCsv_ProducesExpectedHeader()
    {
        var schema = new SongCsvSchema();
        var songs = new List<Song>
        {
            new() { Id = 1 },
        };

        using var stream = new MemoryStream();
        schema.WriteCsv(stream, songs);

        var csv = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("song_id");
        csv.Should().Contain("item_id");
        csv.Should().Contain("full_name");
        csv.Should().Contain("original_bga_yn");
        csv.Should().Contain("cost_game_point");
        csv.Should().Contain("update");
    }

    [TestMethod]
    public void SongCsvSchema_TableName_IsCorrect()
    {
        new SongCsvSchema().TableName.Should().Be("song_song");
    }

    // ── ProductCsvSchema ──

    [TestMethod]
    public void ProductCsvSchema_WriteCsv_ThenReadCsv_RoundTrips()
    {
        // Arrange
        var schema = new ProductCsvSchema();
        var products = new List<Product>
        {
            new() { Id = "PROD_001", ItemId = "ITEM_001", PlatformProductId = "PLAT_001",
                StoreProductId = "STORE_001", ProductType = "1", CostGamePoint = "500",
                CostGameCash = "0", Status = "1", SaleStartDate = "2024-01-01",
                SaleEndDate = "2099-12-31", Update = "1" },
            new() { Id = "PROD_002", ItemId = "ITEM_002", PlatformProductId = "PLAT_002",
                StoreProductId = "STORE_002", ProductType = "2", CostGamePoint = "0",
                CostGameCash = "300", Status = "1", SaleStartDate = "2024-06-01",
                SaleEndDate = "2024-12-31", Update = "2" },
        };

        // Act — write
        using var writeStream = new MemoryStream();
        schema.WriteCsv(writeStream, products);

        // Act — read back
        var csvBytes = writeStream.ToArray();
        using var readStream = new MemoryStream(csvBytes);
        var result = schema.ReadCsv(readStream);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("PROD_001");
        result[0].ItemId.Should().Be("ITEM_001");
        result[0].PlatformProductId.Should().Be("PLAT_001");
        result[0].StoreProductId.Should().Be("STORE_001");
        result[0].ProductType.Should().Be("1");
        result[0].CostGamePoint.Should().Be("500");
        result[0].CostGameCash.Should().Be("0");
        result[0].Status.Should().Be("1");
        result[0].SaleStartDate.Should().Be("2024-01-01");
        result[0].SaleEndDate.Should().Be("2099-12-31");
        result[0].Update.Should().Be("1");

        result[1].Id.Should().Be("PROD_002");
        result[1].ItemId.Should().Be("ITEM_002");
    }

    [TestMethod]
    public void ProductCsvSchema_TableName_IsCorrect()
    {
        new ProductCsvSchema().TableName.Should().Be("product_product");
    }

    // ── PatternCsvSchema ──

    [TestMethod]
    public void PatternCsvSchema_WriteCsv_ThenReadCsv_RoundTrips()
    {
        var schema = new PatternCsvSchema();
        var patterns = new List<SongPattern>
        {
            new() { PatternId = 1, SongId = 1001, Signature = 4,
                Line = 1, Difficulty = 2, PointType = 1, PointValue = 100,
                Flg = "0", Update = "1" },
            new() { PatternId = 2, SongId = 1001, Signature = 5,
                Line = 1, Difficulty = 3, PointType = 2, PointValue = 200,
                Flg = "1", Update = "1" },
        };

        using var writeStream = new MemoryStream();
        schema.WriteCsv(writeStream, patterns);

        var csvBytes = writeStream.ToArray();
        using var readStream = new MemoryStream(csvBytes);
        var result = schema.ReadCsv(readStream);

        result.Should().HaveCount(2);
        result[0].PatternId.Should().Be(1);
        result[0].SongId.Should().Be(1001);
        result[0].Signature.Should().Be(4);
        result[1].PatternId.Should().Be(2);
        result[1].Difficulty.Should().Be(3);
    }

    // ── IngameItemCsvSchema (composite key) ──

    [TestMethod]
    public void IngameItemCsvSchema_ComputesCompositeKey()
    {
        var schema = new IngameItemCsvSchema();
        var csv = "item_type,item_level,product_id,update\r\nSPEED,3,PROD_001,1\r\nPOWER,5,PROD_002,2\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = schema.ReadCsv(stream);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("SPEED_3");
        result[0].ItemType.Should().Be("SPEED");
        result[0].ItemLevel.Should().Be("3");
        result[0].ProductId.Should().Be("PROD_001");
        result[1].Id.Should().Be("POWER_5");
        result[1].ItemType.Should().Be("POWER");
        result[1].ItemLevel.Should().Be("5");
    }

    // ── IngameItemEffectCsvSchema ──

    [TestMethod]
    public void IngameItemEffectCsvSchema_WriteCsv_ThenReadCsv_RoundTrips()
    {
        var schema = new IngameItemEffectCsvSchema();
        var effects = new List<IngameItemEffect>
        {
            new() { Id = "EFFECT_001", EffectType = "SPEED_UP", EffectPoint = "10",
                EffectCount = "1", EffectSpecial = "0", Update = "1" },
        };

        using var writeStream = new MemoryStream();
        schema.WriteCsv(writeStream, effects);

        var csvBytes = writeStream.ToArray();
        using var readStream = new MemoryStream(csvBytes);
        var result = schema.ReadCsv(readStream);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("EFFECT_001");
        result[0].EffectType.Should().Be("SPEED_UP");
        result[0].EffectPoint.Should().Be("10");
    }
}
