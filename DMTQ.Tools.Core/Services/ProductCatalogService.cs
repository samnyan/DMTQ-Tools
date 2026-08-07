using DMTQ.Tools.Core.Models.Entity;
using DMTQ.Tools.Core.Models.Project;

namespace DMTQ.Tools.Core.Services;

/// <summary>Builds the editable Product catalog from the current package.</summary>
public sealed class ProductCatalogService
{
    public IReadOnlyList<Product> BuildCatalog(PatchPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return package.Products
            .OrderBy(product => product.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(product => product.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
