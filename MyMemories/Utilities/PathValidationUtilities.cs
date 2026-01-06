using System;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories.Utilities;

public static class PathValidationUtilities
{
    public static (bool IsValid, string? ErrorMessage) ValidateDirectoryPath(string path, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (allowEmpty, allowEmpty ? null : "Path cannot be empty.");

        if (!Path.IsPathRooted(path))
            return (false, "Path must be an absolute path.");

        try
        {
            // Check for invalid characters
            Path.GetFullPath(path);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Invalid path: {ex.Message}");
        }
    }

    public static async Task<bool> EnsureDirectoryExistsAsync(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}