using System.Globalization;

namespace DMTQ_Tools.Components.Localization;

/// <summary>Manages the persisted English/Chinese culture for the MAUI Blazor UI.</summary>
public sealed class LanguageService : ILanguageService
{
    /// <summary>The preference key used by the MAUI host.</summary>
    public const string PreferenceKey = "dmtq.ui-language";

    /// <summary>The default UI culture.</summary>
    public const string DefaultCultureName = "en-US";

    /// <summary>The supported cultures.</summary>
    public static IReadOnlyList<string> SupportedCultureNames { get; } = ["en-US", "zh-CN"];

    private readonly ILanguagePreferenceStore _preferenceStore;
    private CultureInfo _currentCulture = CreateCulture(DefaultCultureName);

    /// <summary>Creates the service and restores the last valid saved culture.</summary>
    public LanguageService(ILanguagePreferenceStore preferenceStore)
    {
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
        var savedCulture = _preferenceStore.Get(PreferenceKey);
        _currentCulture = CreateCulture(IsSupported(savedCulture) ? savedCulture! : DefaultCultureName);
        ApplyCulture(_currentCulture);
    }

    /// <inheritdoc />
    public CultureInfo CurrentCulture => _currentCulture;

    /// <inheritdoc />
    public event Action? LanguageChanged;

    /// <inheritdoc />
    public void SetLanguage(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        if (!IsSupported(cultureName))
        {
            throw new ArgumentException(
                $"Unsupported culture '{cultureName}'. Supported cultures are: {string.Join(", ", SupportedCultureNames)}.",
                nameof(cultureName));
        }

        if (string.Equals(_currentCulture.Name, cultureName, StringComparison.OrdinalIgnoreCase))
            return;

        _currentCulture = CreateCulture(cultureName);
        ApplyCulture(_currentCulture);
        _preferenceStore.Set(PreferenceKey, _currentCulture.Name);
        LanguageChanged?.Invoke();
    }

    private static bool IsSupported(string? cultureName) =>
        cultureName is not null && SupportedCultureNames.Contains(cultureName, StringComparer.OrdinalIgnoreCase);

    private static CultureInfo CreateCulture(string cultureName) => new(cultureName);

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
