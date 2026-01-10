using System;
using System.IO;

namespace MyMemories.Utilities;

public static class FileUtilities
{
    /// <summary>
    /// Formats file size in human-readable format (B, KB, MB, GB, TB).
    /// </summary>
    public static string FormatFileSize(ulong bytes)
    {
        if (bytes == 0)
            return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Formats file size in human-readable format (B, KB, MB, GB, TB) - overload for long.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
            return "0 B";
        return FormatFileSize((ulong)bytes);
    }

    /// <summary>
    /// Checks if file extension represents an image file.
    /// </summary>
    public static bool IsImageFile(string extension)
    {
        return extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif"
            or ".bmp" or ".ico" or ".webp";
    }

    /// <summary>
    /// Checks if file extension represents a text file.
    /// </summary>
    public static bool IsTextFile(string extension)
    {
        return extension.ToLowerInvariant() is ".txt" or ".xml" or ".json" or ".md" or ".log"
            or ".cs" or ".xaml" or ".config" or ".ini" or ".yaml"
            or ".yml" or ".csv";
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, 
            StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}