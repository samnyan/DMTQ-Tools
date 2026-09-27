using DMTQ.Tools.Core.Models.Entity;

namespace DMTQ.Tools.Components.Models;

public sealed class LinkedItemPickerState
{
    public IReadOnlyList<Item> Items { get; init; } = [];
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsCreateMode { get; set; }
    public bool Accepted { get; set; }
    public bool ResultIsNew { get; set; }
    public Item? ResultItem { get; set; }
}
