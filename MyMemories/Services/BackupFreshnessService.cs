using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

/// <summary>
/// Represents a backup that is out of date.
/// </summary>
public class OutdatedBackup
{
    /// <summary>
    /// The name of the item (category or zip file name).
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// The type of backup (Category or ZipArchive).
    /// </summary>
    public BackupItemType ItemType { get; set; }

    /// <summary>
    /// Path to the source file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the outdated backup file.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Last modified time of the source file.
    /// </summary>
    public DateTime SourceModified { get; set; }

    /// <summary>
    /// Last modified time of the backup file.
    /// </summary>
    public DateTime BackupModified { get; set; }

    /// <summary>
    /// Time difference between source and backup.
    /// </summary>
    public TimeSpan TimeDifference => SourceModified - BackupModified;

    /// <summary>
    /// Whether this backup should be updated.
    /// </summary>
    public bool ShouldUpdate { get; set; } = true;

    /// <summary>
    /// The TreeViewNode containing this item (for saving after update).
    /// </summary>
    public TreeViewNode? Node { get; set; }
}

/// <summary>
/// Type of backup item.
/// </summary>
public enum BackupItemType
{
    Category,
    ZipArchive
}

/// <summary>
/// Service for checking if backups are up-to-date.
/// </summary>
public class BackupFreshnessService
{
    private readonly string _dataFolder;

    public BackupFreshnessService(string dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <summary>
    /// Checks all categories and zip archives for outdated backups.
    /// </summary>
    /// <param name="rootNodes">The root category nodes from the TreeView.</param>
    /// <returns>List of outdated backups.</returns>
    public async Task<List<OutdatedBackup>> CheckAllBackupsAsync(IEnumerable<TreeViewNode> rootNodes)
    {
        var outdatedBackups = new List<OutdatedBackup>();

        foreach (var rootNode in rootNodes)
        {
            if (rootNode.Content is not CategoryItem category)
                continue;

            // Check category backups
            if (category.HasBackupDirectories)
            {
                var categoryOutdated = await CheckCategoryBackupsAsync(category, rootNode);
                outdatedBackups.AddRange(categoryOutdated);
            }

            // Check zip archive backups within this category
            var zipOutdated = await CheckZipBackupsRecursiveAsync(rootNode);
            outdatedBackups.AddRange(zipOutdated);
        }

        return outdatedBackups;
    }

    /// <summary>
    /// Checks if a category's backups are up-to-date.
    /// </summary>
    private async Task<List<OutdatedBackup>> CheckCategoryBackupsAsync(CategoryItem category, TreeViewNode node)
    {
        var outdated = new List<OutdatedBackup>();

        // Get the source file path
        var fileName = Utilities.FileUtilities.SanitizeFileName(category.Name);
        var jsonPath = Path.Combine(_dataFolder, fileName + ".json");
        var encryptedPath = Path.Combine(_dataFolder, fileName + ".zip.json");

        string? sourcePath = null;
        if (File.Exists(encryptedPath))
        {
            sourcePath = encryptedPath;
        }
        else if (File.Exists(jsonPath))
        {
            sourcePath = jsonPath;
        }

        if (sourcePath == null)
            return outdated;

        var sourceInfo = new FileInfo(sourcePath);

        await Task.Run(() =>
        {
            foreach (var backupDir in category.BackupDirectories)
            {
                try
                {
                    var backupPath = Path.Combine(backupDir, Path.GetFileName(sourcePath));

                    if (!File.Exists(backupPath))
                    {
                        // Backup doesn't exist - consider it outdated
                        outdated.Add(new OutdatedBackup
                        {
                            ItemName = category.Name,
                            ItemType = BackupItemType.Category,
                            SourcePath = sourcePath,
                            BackupPath = backupPath,
                            SourceModified = sourceInfo.LastWriteTime,
                            BackupModified = DateTime.MinValue,
                            Node = node
                        });
                        continue;
                    }

                    var backupInfo = new FileInfo(backupPath);

                    // Check if backup is older than source (with 2 second tolerance)
                    if (sourceInfo.LastWriteTime > backupInfo.LastWriteTime.AddSeconds(2))
                    {
                        outdated.Add(new OutdatedBackup
                        {
                            ItemName = category.Name,
                            ItemType = BackupItemType.Category,
                            SourcePath = sourcePath,
                            BackupPath = backupPath,
                            SourceModified = sourceInfo.LastWriteTime,
                            BackupModified = backupInfo.LastWriteTime,
                            Node = node
                        });
                    }
                }
                catch
                {
                    // Ignore errors for individual backup checks
                }
            }
        });

        return outdated;
    }

    /// <summary>
    /// Recursively checks zip archive backups in a category.
    /// </summary>
    private async Task<List<OutdatedBackup>> CheckZipBackupsRecursiveAsync(TreeViewNode node)
    {
        var outdated = new List<OutdatedBackup>();

        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Check if it's a zip archive with backup directories
                if (link.IsDirectory && 
                    link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    link.HasBackupDirectories &&
                    !link.IsCatalogEntry)
                {
                    var zipOutdated = await CheckZipBackupsAsync(link, child);
                    outdated.AddRange(zipOutdated);
                }

                // Recursively check children (for nested structures)
                if (child.Children.Count > 0)
                {
                    var childOutdated = await CheckZipBackupsRecursiveAsync(child);
                    outdated.AddRange(childOutdated);
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively check subcategories
                var subOutdated = await CheckZipBackupsRecursiveAsync(child);
                outdated.AddRange(subOutdated);
            }
        }

        return outdated;
    }

    /// <summary>
    /// Checks if a zip archive's backups are up-to-date.
    /// </summary>
    private async Task<List<OutdatedBackup>> CheckZipBackupsAsync(LinkItem zipLink, TreeViewNode node)
    {
        var outdated = new List<OutdatedBackup>();

        if (!File.Exists(zipLink.Url))
            return outdated;

        var sourceInfo = new FileInfo(zipLink.Url);

        await Task.Run(() =>
        {
            foreach (var backupDir in zipLink.BackupDirectories)
            {
                try
                {
                    var backupPath = Path.Combine(backupDir, Path.GetFileName(zipLink.Url));

                    if (!File.Exists(backupPath))
                    {
                        // Backup doesn't exist - consider it outdated
                        outdated.Add(new OutdatedBackup
                        {
                            ItemName = zipLink.Title,
                            ItemType = BackupItemType.ZipArchive,
                            SourcePath = zipLink.Url,
                            BackupPath = backupPath,
                            SourceModified = sourceInfo.LastWriteTime,
                            BackupModified = DateTime.MinValue,
                            Node = node
                        });
                        continue;
                    }

                    var backupInfo = new FileInfo(backupPath);

                    // Check if backup is older than source (with 2 second tolerance)
                    if (sourceInfo.LastWriteTime > backupInfo.LastWriteTime.AddSeconds(2))
                    {
                        outdated.Add(new OutdatedBackup
                        {
                            ItemName = zipLink.Title,
                            ItemType = BackupItemType.ZipArchive,
                            SourcePath = zipLink.Url,
                            BackupPath = backupPath,
                            SourceModified = sourceInfo.LastWriteTime,
                            BackupModified = backupInfo.LastWriteTime,
                            Node = node
                        });
                    }
                }
                catch
                {
                    // Ignore errors for individual backup checks
                }
            }
        });

        return outdated;
    }

    /// <summary>
    /// Updates selected backups.
    /// </summary>
    /// <param name="backupsToUpdate">List of backups to update.</param>
    /// <returns>Summary of the update operation.</returns>
    public async Task<(int succeeded, int failed)> UpdateBackupsAsync(IEnumerable<OutdatedBackup> backupsToUpdate)
    {
        int succeeded = 0;
        int failed = 0;

        foreach (var backup in backupsToUpdate.Where(b => b.ShouldUpdate))
        {
            try
            {
                // Ensure the backup directory exists
                var backupDir = Path.GetDirectoryName(backup.BackupPath);
                if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Copy the file
                await Task.Run(() => File.Copy(backup.SourcePath, backup.BackupPath, overwrite: true));
                succeeded++;
            }
            catch
            {
                failed++;
            }
        }

        return (succeeded, failed);
    }
}
