using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using DMTQ.Tools.Core.Services;
using FluentAssertions;

namespace DMTQ.Tools.Core.Tests.Services;

[TestClass]
public sealed class LogicalTableServiceTests
{
    [TestMethod]
    public void BuildCatalog_GroupsNonLocalizedLanguageCopiesIntoOneLogicalTable()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", "source", "1.0", "android")
        };
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us", "song_id", "name", ("1", "oblivion")));
        package.Tables.Tables.Add(CreateTable("table/jp/song_song.csv", "song_song", "jp", "song_id", "name", ("1", "oblivion")));

        var service = new LogicalTableService();

        var catalog = service.BuildCatalog(package);

        var songTable = catalog.Single(t => t.Key == "song_song");
        songTable.Kind.Should().Be("Shared");
        songTable.SourcePackageRelativePaths.Should().BeEquivalentTo("table/us/song_song.csv", "table/jp/song_song.csv");
        songTable.Languages.Should().BeEquivalentTo("us", "jp");
        songTable.Rows.Should().ContainSingle();
        songTable.Columns.Select(c => c.Key).Should().Equal("song_id", "name");
    }

    [TestMethod]
    public void BuildCatalog_GroupsLocalizedDescriptionTablesIntoI18nColumns()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", "source", "1.0", "android")
        };
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us", "song_id", "title", ("1", "Oblivion")));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp", "song_id", "title", ("1", "Oblivion JP")));

        var service = new LogicalTableService();

        var catalog = service.BuildCatalog(package);

        var descTable = catalog.Single(t => t.Key == "song_desc");
        descTable.Kind.Should().Be("Localized");
        descTable.Languages.Should().Equal("jp", "us");
        descTable.Columns.Select(c => c.Key).Should().Equal("song_id", "title:jp", "title:us");
        descTable.Rows.Should().ContainSingle();
        descTable.Rows[0].Cells["song_id"].Should().Be("1");
        descTable.Rows[0].Cells["title:us"].Should().Be("Oblivion");
        descTable.Rows[0].Cells["title:jp"].Should().Be("Oblivion JP");
    }

    [TestMethod]
    public void UpdateCell_UpdatesSharedTableCopies()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", "source", "1.0", "android")
        };
        package.Tables.Tables.Add(CreateTable("table/us/song_song.csv", "song_song", "us", "song_id", "name", ("1", "oblivion")));
        package.Tables.Tables.Add(CreateTable("table/jp/song_song.csv", "song_song", "jp", "song_id", "name", ("1", "oblivion")));
        var service = new LogicalTableService();

        service.UpdateCell(package, "song_song", "1", "name", "new-name");

        package.Tables.Tables.SelectMany(table => table.Rows)
            .Select(row => row.Cells.Single(cell => cell.ColumnName == "name").Value)
            .Should().OnlyContain(value => value == "new-name");
    }

    [TestMethod]
    public void UpdateCell_UpdatesLocalizedLanguageCellOnly()
    {
        var package = new PatchPackage
        {
            ProjectInfo = new ProjectInfo("project", "source", "1.0", "android")
        };
        package.Tables.Tables.Add(CreateTable("table/us/song_desc_us.csv", "song_desc_us", "us", "song_id", "title", ("1", "Oblivion")));
        package.Tables.Tables.Add(CreateTable("table/jp/song_desc_jp.csv", "song_desc_jp", "jp", "song_id", "title", ("1", "Oblivion JP")));
        var service = new LogicalTableService();

        service.UpdateCell(package, "song_desc", "1", "title:jp", "新タイトル");

        var jp = package.Tables.Tables.Single(table => table.LanguageCode == "jp");
        var us = package.Tables.Tables.Single(table => table.LanguageCode == "us");
        jp.Rows[0].Cells.Single(cell => cell.ColumnName == "title").Value.Should().Be("新タイトル");
        us.Rows[0].Cells.Single(cell => cell.ColumnName == "title").Value.Should().Be("Oblivion");
    }

    private static GameTable CreateTable(
        string path,
        string tableName,
        string languageCode,
        string keyColumn,
        string valueColumn,
        params (string Key, string Value)[] rows)
    {
        var table = new GameTable
        {
            PackageRelativePath = path,
            TableName = tableName,
            LanguageCode = languageCode
        };
        table.Columns.Add(new GameTableColumn(keyColumn, 0));
        table.Columns.Add(new GameTableColumn(valueColumn, 1));

        for (var i = 0; i < rows.Length; i++)
        {
            var row = new GameTableRow { Order = i };
            row.Cells.Add(new GameTableCell(keyColumn, rows[i].Key));
            row.Cells.Add(new GameTableCell(valueColumn, rows[i].Value));
            table.Rows.Add(row);
        }

        return table;
    }
}
