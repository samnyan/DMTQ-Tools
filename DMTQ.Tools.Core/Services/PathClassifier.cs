namespace DMTQ.Tools.Core.Services;

public static class PathClassifier
{
    public static bool IsCsvTable(string packageRelativePath)
    {
        var path = Normalize(packageRelativePath);
        return path.StartsWith("table/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResourceCategory(string packageRelativePath)
    {
        var path = Normalize(packageRelativePath);
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
        => path.Replace('\\', '/');
}
