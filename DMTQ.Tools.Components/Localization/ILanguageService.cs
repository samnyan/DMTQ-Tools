using System.Globalization;

namespace DMTQ_Tools.Components.Localization;

/// <summary>Controls the two UI cultures supported by the application.</summary>
public interface ILanguageService
{
    /// <summary>Gets the culture currently used for UI strings and formatting.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>Raised after the current culture changes.</summary>
    event Action? LanguageChanged;

    /// <summary>Changes and persists the UI culture.</summary>
    /// <param name="cultureName">Either <c>en-US</c> or <c>zh-CN</c>.</param>
    void SetLanguage(string cultureName);
}

/// <summary>Small persistence abstraction so the UI layer remains MAUI-independent.</summary>
public interface ILanguagePreferenceStore
{
    /// <summary>Reads a saved preference.</summary>
    string? Get(string key);

    /// <summary>Saves a preference.</summary>
    void Set(string key, string value);
}
