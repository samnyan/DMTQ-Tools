using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached Item drafts and applies CRUD operations to a package.</summary>
public sealed class ItemEditService
{
    /// <summary>Creates a detached effect linked to an item, when present.</summary>
    public IngameItemEffect? CreateEffectDraft(
        PatchPackage package,
        string itemId,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var source = package.GetPlatformTables(platform).IngameItemEffects.FirstOrDefault(effect =>
            effect.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        return source is null ? null : CloneEffect(source, itemId);
    }

    /// <summary>Adds, updates, or removes the optional effect linked to an item.</summary>
    public void ApplyEffectDraft(
        PatchPackage package,
        string itemId,
        IngameItemEffect? draft,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var effects = package.GetPlatformTables(platform).IngameItemEffects;
        effects.RemoveAll(effect => effect.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (draft is not null)
        {
            effects.Add(CloneEffect(draft, itemId));
        }
    }

    public Item CreateDraft(Item source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var draft = new Item { Id = id ?? source.Id };
        CopyItemData(source, draft);
        return draft;
    }

    public void UpdateItem(PatchPackage package, Item item, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(item);

        var existing = FindItem(package, item.Id, platform);
        CopyItemData(item, existing);
    }

    public void AddItem(PatchPackage package, Item item, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(item);

        var items = package.GetPlatformTables(platform).Items;
        if (items.Any(existing =>
                existing.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Item '{item.Id}' already exists.");
        }

        items.Add(CreateDraft(item));
    }

    public void RemoveItem(PatchPackage package, string itemId, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        var tables = package.GetPlatformTables(platform);
        var item = FindItem(package, itemId, platform);
        tables.Items.Remove(item);
        tables.IngameItemEffects.RemoveAll(effect =>
            effect.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
    }

    private static Item FindItem(PatchPackage package, string itemId, string? platform)
        => package.GetPlatformTables(platform).Items.FirstOrDefault(item =>
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

    private static IngameItemEffect CloneEffect(IngameItemEffect source, string itemId)
        => new()
        {
            Id = itemId,
            EffectType = source.EffectType,
            EffectPoint = source.EffectPoint,
            EffectCount = source.EffectCount,
            EffectSpecial = source.EffectSpecial,
            Update = source.Update
        };
}
