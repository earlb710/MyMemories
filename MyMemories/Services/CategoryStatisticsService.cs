using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyMemories.Services;

/// <summary>
/// Service for calculating folder and category statistics.
/// </summary>
public class CategoryStatisticsService
{
    /// <summary>
    /// Calculates statistics for multiple folders.
    /// </summary>
    public (int FolderCount, int SubdirectoryCount, int FileCount, ulong TotalSize) CalculateMultipleFoldersStatistics(string[] folderPaths)
    {
        int folderCount = 0;
        int subdirectoryCount = 0;
        int fileCount = 0;
        ulong totalSize = 0;

        foreach (var folderPath in folderPaths)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                continue;

            folderCount++;
            var stats = CalculateFolderStatistics(folderPath);
            subdirectoryCount += stats.SubdirectoryCount;
            fileCount += stats.FileCount;
            totalSize += stats.TotalSize;
        }

        return (folderCount, subdirectoryCount, fileCount, totalSize);
    }

    /// <summary>
    /// Calculates folder statistics recursively.
    /// </summary>
    public (int SubdirectoryCount, int FileCount, ulong TotalSize) CalculateFolderStatistics(string folderPath)
    {
        int subdirectoryCount = 0;
        int fileCount = 0;
        ulong totalSize = 0;

        try
        {
            // Get all subdirectories recursively
            var directories = Directory.GetDirectories(folderPath, "*", SearchOption.AllDirectories);
            subdirectoryCount = directories.Length;

            // Get all files recursively
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            fileCount = files.Length;

            // Calculate total size
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += (ulong)fileInfo.Length;
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            LogUtilities.LogWarning("CategoryStatisticsService.CalculateFolderStatistics", $"Access denied to some folders in: {folderPath}");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("CategoryStatisticsService.CalculateFolderStatistics", "Error during statistics calculation", ex);
        }

        return (subdirectoryCount, fileCount, totalSize);
    }

    /// <summary>
    /// Recursively collects all folder paths from a category node (excluding catalog entries).
    /// </summary>
    public List<string> CollectFolderPathsFromCategory(Microsoft.UI.Xaml.Controls.TreeViewNode categoryNode)
    {
        var folderPaths = new List<string>();

        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Only include directory links that are not catalog entries
                if (link.IsDirectory && !link.IsCatalogEntry && Directory.Exists(link.Url))
                {
                    folderPaths.Add(link.Url);
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively collect from subcategories
                folderPaths.AddRange(CollectFolderPathsFromCategory(child));
            }
        }

        return folderPaths;
    }
}
