using Microsoft.AspNetCore.Components;

namespace DMTQ_Tools.Components.Shared;

/// <summary>Reads and updates the shareable query state used by table pages.</summary>
public static class TableNavigationState
{
    public static string? ReadQuery(NavigationManager navigation, string key)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var query = ParseQuery(navigation.ToAbsoluteUri(navigation.Uri).Query);
        return query.TryGetValue(key, out var value) ? value.ToString() : null;
    }

    public static int ReadPageIndex(NavigationManager navigation)
    {
        if (!int.TryParse(ReadQuery(navigation, "page"), out var page) || page <= 1)
            return 0;

        return page - 1;
    }

    public static string CreateEditorUrl(NavigationManager navigation, string editorPath)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorPath);

        var returnUrl = GetCurrentLocalUrl(navigation);
        var separator = editorPath.Contains('?') ? '&' : '?';
        return $"{editorPath}{separator}returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    public static string ResolveReturnUrl(NavigationManager navigation, string? returnUrl, string fallbackPath)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackPath);

        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallbackPath;

        var candidate = returnUrl.Trim();
        if (!candidate.StartsWith('/') || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.Contains("\\", StringComparison.Ordinal)
            || Uri.TryCreate(candidate, UriKind.Absolute, out _))
        {
            return fallbackPath;
        }

        return candidate;
    }

    public static void UpdateQuery(NavigationManager navigation, params (string Key, string? Value)[] updates)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(updates);

        var currentUri = navigation.ToAbsoluteUri(navigation.Uri);
        var query = ParseQuery(currentUri.Query);

        foreach (var (key, value) in updates)
        {
            if (string.IsNullOrWhiteSpace(value))
                query.Remove(key);
            else
                query[key] = value;
        }

        var queryString = query.Count == 0
            ? string.Empty
            : "?" + string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        navigation.NavigateTo($"{currentUri.GetLeftPart(UriPartial.Path)}{queryString}{currentUri.Fragment}", replace: true);
    }

    public static string GetCurrentLocalUrl(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var fragmentIndex = relative.IndexOf('#');
        if (fragmentIndex >= 0)
            relative = relative[..fragmentIndex];

        return "/" + relative.TrimStart('/');
    }

    private static string? GetReturnUrlFromQuery(NavigationManager navigation)
        => ReadQuery(navigation, "returnUrl");

    public static string ResolveCurrentReturnUrl(NavigationManager navigation, string fallbackPath)
        => ResolveReturnUrl(navigation, GetReturnUrlFromQuery(navigation), fallbackPath);

    private static Dictionary<string, string> ParseQuery(string queryString)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            var key = separator < 0 ? segment : segment[..separator];
            var value = separator < 0 ? string.Empty : segment[(separator + 1)..];
            values[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return values;
    }
}
