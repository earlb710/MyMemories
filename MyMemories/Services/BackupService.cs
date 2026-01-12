using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Modular service for backing up files to multiple directories.
/// Designed to be reusable for category files, zip archives, and other file types.
/// </summary>
public class BackupService
{
    private static BackupService? _instance;
    
    /// <summary>
    /// Singleton instance of the BackupService.
    /// </summary>
    public static BackupService Instance => _instance ??= new BackupService();

    /// <summary>
    /// Result of a backup operation.
    /// </summary>
    public class BackupResult
    {
        public bool Success { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime BackupTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Summary of a batch backup operation.
    /// </summary>
    public class BackupSummary
    {
        public List<BackupResult> Results { get; set; } = new();
        public int SuccessCount => Results.Count(r => r.Success);
        public int FailureCount => Results.Count(r => !r.Success);
        public bool AllSuccessful => Results.All(r => r.Success);
        public bool HasFailures => Results.Any(r => !r.Success);
    }

    /// <summary>
    /// Copies a file to multiple backup directories.
    /// </summary>
    /// <param name="sourceFilePath">The source file to backup.</param>
    /// <param name="backupDirectories">List of directories to copy the file to.</param>
    /// <param name="preserveTimestamp">If true, preserves the original file's timestamps.</param>
    /// <returns>Summary of the backup operation.</returns>
    public async Task<BackupSummary> BackupFileAsync(
        string sourceFilePath, 
        IEnumerable<string> backupDirectories,
        bool preserveTimestamp = true)
    {
        var summary = new BackupSummary();

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            summary.Results.Add(new BackupResult
            {
                Success = false,
                SourcePath = sourceFilePath ?? string.Empty,
                ErrorMessage = "Source file does not exist"
            });
            return summary;
        }

        var fileName = Path.GetFileName(sourceFilePath);
        var sourceInfo = new FileInfo(sourceFilePath);

        foreach (var directory in backupDirectories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            var result = await BackupToDirectoryAsync(
                sourceFilePath, 
                fileName, 
                directory, 
                sourceInfo,
                preserveTimestamp);
            summary.Results.Add(result);
        }

        return summary;
    }

    /// <summary>
    /// Copies a file to a single backup directory.
    /// </summary>
    private async Task<BackupResult> BackupToDirectoryAsync(
        string sourceFilePath,
        string fileName,
        string backupDirectory,
        FileInfo sourceInfo,
        bool preserveTimestamp)
    {
        var result = new BackupResult
        {
            SourcePath = sourceFilePath,
            BackupTime = DateTime.Now
        };

        try
        {
            // Ensure directory exists
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                LogUtilities.LogInfo("BackupService.BackupToDirectoryAsync",
                    $"Created backup directory: {backupDirectory}");
            }

            var destinationPath = Path.Combine(backupDirectory, fileName);
            result.DestinationPath = destinationPath;

            // Copy the file
            await Task.Run(() =>
            {
                File.Copy(sourceFilePath, destinationPath, overwrite: true);

                // Preserve original timestamps if requested
                if (preserveTimestamp)
                {
                    File.SetCreationTime(destinationPath, sourceInfo.CreationTime);
                    File.SetLastWriteTime(destinationPath, sourceInfo.LastWriteTime);
                }
            });

            result.Success = true;

            LogUtilities.LogInfo("BackupService.BackupToDirectoryAsync",
                $"Successfully backed up {fileName} to {backupDirectory}");
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Access denied: {ex.Message}";
            LogUtilities.LogError("BackupService.BackupToDirectoryAsync",
                $"Access denied backing up to {backupDirectory}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Directory not found: {ex.Message}";
            LogUtilities.LogError("BackupService.BackupToDirectoryAsync",
                $"Directory not found: {backupDirectory}", ex);
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"IO error: {ex.Message}";
            LogUtilities.LogError("BackupService.BackupToDirectoryAsync",
                $"IO error backing up to {backupDirectory}", ex);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogUtilities.LogError("BackupService.BackupToDirectoryAsync",
                $"Error backing up to {backupDirectory}", ex);
        }

        return result;
    }

    /// <summary>
    /// Copies a file with a timestamped name to backup directories.
    /// Useful for versioned backups.
    /// </summary>
    /// <param name="sourceFilePath">The source file to backup.</param>
    /// <param name="backupDirectories">List of directories to copy the file to.</param>
    /// <param name="timestampFormat">Format for the timestamp suffix (default: yyyyMMdd_HHmmss).</param>
    /// <returns>Summary of the backup operation.</returns>
    public async Task<BackupSummary> BackupFileWithTimestampAsync(
        string sourceFilePath,
        IEnumerable<string> backupDirectories,
        string timestampFormat = "yyyyMMdd_HHmmss")
    {
        var summary = new BackupSummary();

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            summary.Results.Add(new BackupResult
            {
                Success = false,
                SourcePath = sourceFilePath ?? string.Empty,
                ErrorMessage = "Source file does not exist"
            });
            return summary;
        }

        var originalFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var extension = Path.GetExtension(sourceFilePath);
        var timestamp = DateTime.Now.ToString(timestampFormat);
        var timestampedFileName = $"{originalFileName}_{timestamp}{extension}";

        var sourceInfo = new FileInfo(sourceFilePath);

        foreach (var directory in backupDirectories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            var result = await BackupToDirectoryAsync(
                sourceFilePath,
                timestampedFileName,
                directory,
                sourceInfo,
                preserveTimestamp: false);
            summary.Results.Add(result);
        }

        return summary;
    }

    /// <summary>
    /// Validates that all backup directories exist and are writable.
    /// </summary>
    /// <param name="directories">Directories to validate.</param>
    /// <returns>Dictionary mapping directory path to validation result (true if valid).</returns>
    public Dictionary<string, (bool IsValid, string? ErrorMessage)> ValidateBackupDirectories(
        IEnumerable<string> directories)
    {
        var results = new Dictionary<string, (bool IsValid, string? ErrorMessage)>();

        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    // Try to create it
                    Directory.CreateDirectory(directory);
                    results[directory] = (true, null);
                }
                else
                {
                    // Test write access by creating a temp file
                    var testFile = Path.Combine(directory, $".backup_test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    results[directory] = (true, null);
                }
            }
            catch (UnauthorizedAccessException)
            {
                results[directory] = (false, "Access denied - no write permission");
            }
            catch (DirectoryNotFoundException)
            {
                results[directory] = (false, "Parent directory does not exist");
            }
            catch (PathTooLongException)
            {
                results[directory] = (false, "Path is too long");
            }
            catch (Exception ex)
            {
                results[directory] = (false, ex.Message);
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the backup file path for a given source file and backup directory.
    /// </summary>
    public string GetBackupFilePath(string sourceFilePath, string backupDirectory)
    {
        var fileName = Path.GetFileName(sourceFilePath);
        return Path.Combine(backupDirectory, fileName);
    }

    /// <summary>
    /// Checks if a backup exists in the specified directory.
    /// </summary>
    public bool BackupExists(string sourceFilePath, string backupDirectory)
    {
        var backupPath = GetBackupFilePath(sourceFilePath, backupDirectory);
        return File.Exists(backupPath);
    }

    /// <summary>
    /// Gets information about existing backups.
    /// </summary>
    public List<FileInfo> GetExistingBackups(string sourceFilePath, IEnumerable<string> backupDirectories)
    {
        var backups = new List<FileInfo>();
        var fileName = Path.GetFileName(sourceFilePath);

        foreach (var directory in backupDirectories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            var backupPath = Path.Combine(directory, fileName);
            if (File.Exists(backupPath))
            {
                backups.Add(new FileInfo(backupPath));
            }
        }

        return backups;
    }
}
