using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached Product drafts and applies CRUD operations to a package.</summary>
public sealed class ProductEditService
{
    /// <summary>Creates detached copies of in-game configurations linked to a product.</summary>
    public List<IngameItem> CreateIngameItemDrafts(
        PatchPackage package,
        string productId,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        return package.GetPlatformTables(platform).IngameItems
            .Where(item => item.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase))
            .Select(CloneIngameItem)
            .ToList();
    }

    /// <summary>Replaces all in-game configurations linked to a product.</summary>
    public void ApplyIngameItemDrafts(
        PatchPackage package,
        string productId,
        IEnumerable<IngameItem> drafts,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(drafts);

        var allItems = package.GetPlatformTables(platform).IngameItems;
        var normalized = NormalizeIngameItemDrafts(allItems, productId, drafts);
        ReplaceIngameItems(allItems, productId, normalized);
    }

    /// <summary>Atomically updates a product and its linked in-game configurations.</summary>
    public void UpdateProduct(
        PatchPackage package,
        Product product,
        IEnumerable<IngameItem> ingameItemDrafts,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(ingameItemDrafts);

        var tables = package.GetPlatformTables(platform);
        var existing = FindProduct(package, product.Id, platform);
        var normalized = NormalizeIngameItemDrafts(tables.IngameItems, product.Id, ingameItemDrafts);
        CopyProductData(product, existing);
        ReplaceIngameItems(tables.IngameItems, product.Id, normalized);
    }

    /// <summary>Atomically adds a product and its linked in-game configurations.</summary>
    public void AddProduct(
        PatchPackage package,
        Product product,
        IEnumerable<IngameItem> ingameItemDrafts,
        string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(ingameItemDrafts);

        var tables = package.GetPlatformTables(platform);
        if (tables.Products.Any(existing => existing.Id.Equals(product.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Product '{product.Id}' already exists.");
        var normalized = NormalizeIngameItemDrafts(tables.IngameItems, product.Id, ingameItemDrafts);
        tables.Products.Add(CreateDraft(product));
        ReplaceIngameItems(tables.IngameItems, product.Id, normalized);
    }

    private static void ReplaceIngameItems(List<IngameItem> allItems, string productId, List<IngameItem> normalized)
    {
        allItems.RemoveAll(item => item.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        allItems.AddRange(normalized);
    }

    public Product CreateDraft(Product source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var draft = new Product { Id = id ?? source.Id };
        CopyProductData(source, draft);
        return draft;
    }

    public void UpdateProduct(PatchPackage package, Product product, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);

        var existing = FindProduct(package, product.Id, platform);
        CopyProductData(product, existing);
    }

    public void AddProduct(PatchPackage package, Product product, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);

        var products = package.GetPlatformTables(platform).Products;
        if (products.Any(existing =>
                existing.Id.Equals(product.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Product '{product.Id}' already exists.");
        }

        products.Add(CreateDraft(product));
    }

    public void RemoveProduct(PatchPackage package, string productId, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        var products = package.GetPlatformTables(platform).Products;
        var product = FindProduct(package, productId, platform);
        products.Remove(product);

        package.GetPlatformTables(platform).IngameItems.RemoveAll(item =>
            item.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    private static Product FindProduct(PatchPackage package, string productId, string? platform)
        => package.GetPlatformTables(platform).Products.FirstOrDefault(product =>
               product.Id.Equals(productId, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

    private static void CopyProductData(Product source, Product target)
    {
        target.ItemId = source.ItemId;
        target.PlatformProductId = source.PlatformProductId;
        target.StoreProductId = source.StoreProductId;
        target.ProductType = source.ProductType;
        target.CostGamePoint = source.CostGamePoint;
        target.CostGameCash = source.CostGameCash;
        target.Status = source.Status;
        target.SaleStartDate = source.SaleStartDate;
        target.SaleEndDate = source.SaleEndDate;
        target.Update = source.Update;

        target.CategoryIds.Clear();
        target.CategoryIds.AddRange(source.CategoryIds);
    }

    private static IngameItem CloneIngameItem(IngameItem source)
        => new()
        {
            Id = source.Id,
            ItemType = source.ItemType,
            ItemLevel = source.ItemLevel,
            ProductId = source.ProductId,
            Update = source.Update
        };

    private static List<IngameItem> NormalizeIngameItemDrafts(
        IEnumerable<IngameItem> existingItems,
        string productId,
        IEnumerable<IngameItem> drafts)
    {
        var normalized = drafts.Select(draft =>
        {
            if (string.IsNullOrWhiteSpace(draft.ItemType)
                || string.IsNullOrWhiteSpace(draft.ItemLevel))
                throw new InvalidOperationException("In-game item type and level are required.");

            var type = draft.ItemType.Trim();
            var level = draft.ItemLevel.Trim();
            return new IngameItem
            {
                Id = $"{type}_{level}",
                ItemType = type,
                ItemLevel = level,
                ProductId = productId,
                Update = draft.Update
            };
        }).ToList();

        if (normalized.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("In-game item type and level combinations must be unique.");

        var conflictingIds = existingItems
            .Where(item => !item.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalized.Any(item => conflictingIds.Contains(item.Id)))
            throw new InvalidOperationException("An in-game item with the same type and level already exists.");

        return normalized;
    }
}
