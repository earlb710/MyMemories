namespace MyMemories.Utilities;

public static class ZipUtilities
{
    /// <summary>
    /// Parses a zip entry URL (format: "zipPath::entryPath").
    /// Returns (zipPath, entryPath) or (null, null) if invalid.
    /// </summary>
    public static (string? zipPath, string? entryPath) ParseZipEntryUrl(string url)
    {
        var parts = url.Split("::", 2);
        if (parts.Length != 2)
            return (null, null);
        
        return (parts[0], parts[1]);
    }

    /// <summary>
    /// Checks if a URL represents a zip entry (contains "::").
    /// </summary>
    public static bool IsZipEntryUrl(string url)
    {
        return !string.IsNullOrEmpty(url) && url.Contains("::");
    }
}