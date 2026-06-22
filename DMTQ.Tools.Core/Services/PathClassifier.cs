namespace DMTQ.Tools.Core.Services;

public static class PathClassifier
{
    public static bool IsCsvTable(string packageRelativePath)
    {
        var path = NormalizePackageRelativePath(packageRelativePath);
        return path.StartsWith("table/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResourceCategory(string packageRelativePath)
    {
        var path = NormalizePackageRelativePath(packageRelativePath);
        var firstSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment switch
        {
            "dlc" => "dlc",
            "preview" => "preview",
            "Fonts" => "Fonts",
            "fonts" => "Fonts",
            _ => "other"
        };
    }

    public static string Normalize(string path)
        => NormalizePackageRelativePath(path);

    public static string NormalizePackageRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"Manifest contains unsafe package path '{path}'.");
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new InvalidDataException($"Manifest contains unsafe package path '{path}'.");
        }

        return string.Join('/', parts);
    }
}
