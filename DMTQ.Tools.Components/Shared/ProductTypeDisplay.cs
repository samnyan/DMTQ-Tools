using DMTQ_Tools.Components.Localization;
using Microsoft.Extensions.Localization;

namespace DMTQ_Tools.Components.Shared;

/// <summary>Resolves product type codes to their localized descriptions while retaining unknown codes.</summary>
public static class ProductTypeDisplay
{
    /// <summary>Gets the localized description for a product type code.</summary>
    public static string GetName(string? type, IStringLocalizer<AppStrings> localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return type?.Trim().ToUpperInvariant() switch
        {
            "I" => localizer["Options.ProductTypeUnlock"].Value,
            "P" => localizer["Options.ProductTypePack"].Value,
            "T" or "TICKET" => localizer["Options.ProductTypeTicket"].Value,
            _ => localizer["Options.ProductTypeRawCode"].Value
        };
    }
}
