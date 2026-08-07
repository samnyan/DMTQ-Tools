using DMTQ_Tools.Components.Localization;

namespace DMTQ_Tools.Services;

/// <summary>Stores the selected UI language in the native MAUI preferences store.</summary>
public sealed class MauiLanguagePreferenceStore : ILanguagePreferenceStore
{
    /// <inheritdoc />
    public string? Get(string key)
    {
        var value = Preferences.Default.Get(key, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <inheritdoc />
    public void Set(string key, string value) => Preferences.Default.Set(key, value);
}
