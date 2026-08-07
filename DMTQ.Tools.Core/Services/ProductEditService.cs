using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Creates detached Product drafts and applies CRUD operations to a package.</summary>
public sealed class ProductEditService
{
    public Product CreateDraft(Product source, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var draft = new Product { Id = id ?? source.Id };
        CopyProductData(source, draft);
        return draft;
    }

    public void UpdateProduct(PatchPackage package, Product product)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);

        var existing = FindProduct(package, product.Id);
        CopyProductData(product, existing);
    }

    public void AddProduct(PatchPackage package, Product product)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(product);

        if (package.Products.Any(existing =>
                existing.Id.Equals(product.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Product '{product.Id}' already exists.");
        }

        package.Products.Add(CreateDraft(product));
    }

    public void RemoveProduct(PatchPackage package, string productId)
    {
        ArgumentNullException.ThrowIfNull(package);

        var product = FindProduct(package, productId);
        package.Products.Remove(product);
    }

    private static Product FindProduct(PatchPackage package, string productId)
        => package.Products.FirstOrDefault(product =>
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
}
