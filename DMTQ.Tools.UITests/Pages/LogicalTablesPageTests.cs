using DMTQ.Tools.Core.Models;
using DMTQ.Tools.Core.Models.Export;
using DMTQ.Tools.Core.Models.Project;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DMTQ.Tools.UITests.Pages;

[TestClass]
public sealed class LogicalTablesPageTests : BlazorUITestBase
{
    [TestMethod]
    public async Task RawTableRowCanBeEditedAndAdded()
    {
        var state = CreateStateWithEmptyPackage();
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test", null, "1.0", null) };
        var table = new GameTable
        {
            PackageRelativePath = "table/us/custom_settings.csv",
            TableName = "custom_settings",
            LanguageCode = "us"
        };
        table.Columns.Add(new GameTableColumn("id", 0));
        table.Columns.Add(new GameTableColumn("description", 1));
        var sourceRow = new GameTableRow { Order = 0 };
        sourceRow.Cells.Add(new GameTableCell("id", "row-1"));
        sourceRow.Cells.Add(new GameTableCell("description", "before"));
        table.Rows.Add(sourceRow);
        package.Tables.Tables.Add(table);
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Tables>();
        cut.Markup.Should().Contain("custom_settings");
        cut.Markup.Should().Contain("before");

        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Instance.IconOnly)
            .Instance.OnClick.InvokeAsync(new()));
        await cut.InvokeAsync(() => cut.FindComponents<FluentTextArea>()
            .Single(input => input.Instance.Label == "description")
            .Instance.ValueChanged.InvokeAsync("after"));
        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Markup.Contains("Save row", StringComparison.Ordinal)
                || button.Markup.Contains("保存行", StringComparison.Ordinal))
            .Instance.OnClick.InvokeAsync(new()));

        table.Rows[0].Cells.Single(cell => cell.ColumnName == "description").Value.Should().Be("after");

        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Markup.Contains("Add row", StringComparison.Ordinal)
                || button.Markup.Contains("添加行", StringComparison.Ordinal))
            .Instance.OnClick.InvokeAsync(new()));
        await cut.InvokeAsync(() => cut.FindComponents<FluentTextInput>()
            .Single(input => input.Instance.Label == "id")
            .Instance.ValueChanged.InvokeAsync("row-2"));
        await cut.InvokeAsync(() => cut.FindComponents<FluentTextArea>()
            .Single(input => input.Instance.Label == "description")
            .Instance.ValueChanged.InvokeAsync("new row"));
        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Markup.Contains("Save row", StringComparison.Ordinal)
                || button.Markup.Contains("保存行", StringComparison.Ordinal))
            .Instance.OnClick.InvokeAsync(new()));

        table.Rows.Should().HaveCount(2);
        table.Rows[1].Cells.Single(cell => cell.ColumnName == "description").Value.Should().Be("new row");
    }

    [TestMethod]
    public async Task RawFlagColumnUsesSwitchAndKeepsYnCsvEncoding()
    {
        var state = CreateStateWithEmptyPackage();
        var package = new PatchPackage { ProjectInfo = new ProjectInfo("test", null, "1.0", null) };
        var table = new GameTable
        {
            PackageRelativePath = "table/us/song_songPattern.csv",
            TableName = "song_songPattern",
            LanguageCode = "us"
        };
        table.Columns.Add(new GameTableColumn("pattern_id", 0));
        table.Columns.Add(new GameTableColumn("flg", 1));
        var row = new GameTableRow { Order = 0 };
        row.Cells.Add(new GameTableCell("pattern_id", "10"));
        row.Cells.Add(new GameTableCell("flg", "Y"));
        table.Rows.Add(row);
        package.Tables.Tables.Add(table);
        state.SetPackage(package);
        state.SetProjectRoot("test-project");
        RegisterAllServices(state);

        var cut = RenderWithProviders<Tables>();
        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Instance.IconOnly)
            .Instance.OnClick.InvokeAsync(new()));
        cut.FindComponents<FluentSwitch>().Should().ContainSingle();
        await cut.InvokeAsync(() => cut.FindComponent<FluentSwitch>().Instance.ValueChanged.InvokeAsync(false));
        await cut.InvokeAsync(() => cut.FindComponents<FluentButton>()
            .Single(button => button.Markup.Contains("Save row", StringComparison.Ordinal))
            .Instance.OnClick.InvokeAsync(new()));

        table.Rows[0].Cells.Single(cell => cell.ColumnName == "flg").Value.Should().Be("N");
    }
}
