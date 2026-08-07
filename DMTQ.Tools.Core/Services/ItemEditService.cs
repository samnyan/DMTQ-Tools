using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached Item drafts and applies CRUD operations to a package.</summary>
public sealed class ItemEditService
{
    public Item CreateDraft(Item source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var draft = new Item { Id = id ?? source.Id };
        CopyItemData(source, draft);
        return draft;
    }

    public void UpdateItem(PatchPackage package, Item item)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(item);

        var existing = FindItem(package, item.Id);
        CopyItemData(item, existing);
    }

    public void AddItem(PatchPackage package, Item item)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(item);

        if (package.Items.Any(existing =>
                existing.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Item '{item.Id}' already exists.");
        }

        package.Items.Add(CreateDraft(item));
    }

    public void RemoveItem(PatchPackage package, string itemId)
    {
        ArgumentNullException.ThrowIfNull(package);

        var item = FindItem(package, itemId);
        package.Items.Remove(item);
    }

    private static Item FindItem(PatchPackage package, string itemId)
        => package.Items.FirstOrDefault(item =>
               item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Item '{itemId}' was not found.");

    private static void CopyItemData(Item source, Item target)
    {
        target.ItemName = source.ItemName;
        target.ImgUrl1 = source.ImgUrl1;
        target.ImgUrl2 = source.ImgUrl2;
        target.Description = source.Description;
        target.RepeatCount = source.RepeatCount;
        target.ItemType = source.ItemType;
        target.LimitMinute = source.LimitMinute;
        target.Status = source.Status;
        target.BuyLevel = source.BuyLevel;
        target.BuyLimitCount = source.BuyLimitCount;
        target.BuyLimitType = source.BuyLimitType;
        target.Summary = source.Summary;
        target.Update = source.Update;

        CopyDictionary(source.NamesByLanguage, target.NamesByLanguage);
        CopyDictionary(source.DescriptionsByLanguage, target.DescriptionsByLanguage);
        CopyDictionary(source.SummariesByLanguage, target.SummariesByLanguage);
    }

    private static void CopyDictionary(
        IReadOnlyDictionary<string, string> source,
        Dictionary<string, string> target)
    {
        target.Clear();
        foreach (var (language, value) in source)
            target[language] = value;
    }
}
