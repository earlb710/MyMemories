using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Log message types for audit logging.
/// </summary>
public enum AuditLogType
{
    Info,
    Warning,
    Error,
    Change,
    Security,
    Add,
    Remove
}

/// <summary>
/// Service for managing per-category audit logs.
/// Each root category (xyz.json) can have its own audit log (xyz.log).
/// Log entries include line numbers and automatic rotation after 2000 entries when older than 1 day.
/// </summary>
public class AuditLogService
{
    private readonly string _logDirectory;
    private const int MaxEntriesBeforeRotation = 2000;
    private static readonly object _lockObject = new();

    public AuditLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        
        if (!string.IsNullOrEmpty(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    /// <summary>
    /// Checks if audit logging is available (log directory is configured).
    /// </summary>
    public bool IsLoggingAvailable => !string.IsNullOrEmpty(_logDirectory) && Directory.Exists(_logDirectory);

    /// <summary>
    /// Gets the log file path for a category.
    /// </summary>
    /// <param name="categoryName">The root category name (matches the JSON file name without extension).</param>
    /// <returns>The full path to the log file.</returns>
    public string GetLogFilePath(string categoryName)
    {
        var sanitizedName = SanitizeFileName(categoryName);
        return Path.Combine(_logDirectory, $"{sanitizedName}.log");
    }

    /// <summary>
    /// Gets the current line count from a log file.
    /// </summary>
    private int GetCurrentLineCount(string logFilePath)
    {
        if (!File.Exists(logFilePath))
        {
            return 0;
        }

        try
        {
            return File.ReadLines(logFilePath).Count();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets the date from the first entry in a log file.
    /// </summary>
    private DateTime? GetFirstEntryDate(string logFilePath)
    {
        if (!File.Exists(logFilePath))
        {
            return null;
        }

        try
        {
            var firstLine = File.ReadLines(logFilePath).FirstOrDefault();
            if (string.IsNullOrEmpty(firstLine))
            {
                return null;
            }

            // Extract date from format: "1|[2024-01-09 12:34:56.789]..."
            var pipeIndex = firstLine.IndexOf('|');
            if (pipeIndex < 0)
            {
                return null;
            }

            var afterPipe = firstLine.Substring(pipeIndex + 1);
            if (afterPipe.StartsWith("[") && afterPipe.Length > 24)
            {
                var dateStr = afterPipe.Substring(1, 23); // "2024-01-09 12:34:56.789"
                if (DateTime.TryParse(dateStr, out var date))
                {
                    return date;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    /// <summary>
    /// Checks if the log file should be rotated and performs rotation if needed.
    /// Rotation occurs when: log has more than 2000 entries AND first entry is older than 1 day.
    /// </summary>
    private async Task RotateLogIfNeededAsync(string logFilePath, string categoryName)
    {
        if (!File.Exists(logFilePath))
        {
            return;
        }

        try
        {
            var lineCount = GetCurrentLineCount(logFilePath);
            if (lineCount < MaxEntriesBeforeRotation)
            {
                return; // Not enough entries to rotate
            }

            var firstEntryDate = GetFirstEntryDate(logFilePath);
            if (!firstEntryDate.HasValue)
            {
                return; // Can't determine first entry date
            }

            var daysSinceFirstEntry = (DateTime.Now - firstEntryDate.Value).TotalDays;
            if (daysSinceFirstEntry < 1)
            {
                return; // First entry is less than 1 day old
            }

            // Rotate the log file
            var sanitizedName = SanitizeFileName(categoryName);
            var archiveDateStr = firstEntryDate.Value.ToString("yyyy-MM-dd");
            var archivePath = Path.Combine(_logDirectory, $"{sanitizedName}_{archiveDateStr}.log");

            // If archive already exists, add a counter
            var counter = 1;
            while (File.Exists(archivePath))
            {
                archivePath = Path.Combine(_logDirectory, $"{sanitizedName}_{archiveDateStr}_{counter}.log");
                counter++;
            }

            File.Move(logFilePath, archivePath);
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.RotateLogIfNeededAsync",
                $"Failed to rotate audit log for category '{categoryName}'",
                ex);
        }
    }

    /// <summary>
    /// Writes an audit log entry for a category.
    /// </summary>
    /// <param name="categoryName">The root category name.</param>
    /// <param name="logType">The type of log message.</param>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional additional details.</param>
    public async Task LogAsync(string categoryName, AuditLogType logType, string message, string? details = null)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            var logFilePath = GetLogFilePath(categoryName);
            
            // Check for rotation before writing
            await RotateLogIfNeededAsync(logFilePath, categoryName);
            
            // Get next line number
            int lineNumber;
            lock (_lockObject)
            {
                lineNumber = GetCurrentLineCount(logFilePath) + 1;
            }
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var typeTag = GetLogTypeTag(logType);
            
            var logEntry = $"{lineNumber}|[{timestamp}] [{typeTag}] {message}";
            if (!string.IsNullOrWhiteSpace(details))
            {
                logEntry += $" | {details}";
            }
            logEntry += Environment.NewLine;

            lock (_lockObject)
            {
                File.AppendAllText(logFilePath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.LogAsync",
                $"Failed to write audit log for category '{categoryName}'",
                ex);
        }
    }

    /// <summary>
    /// Logs a category change (create, update, delete).
    /// </summary>
    public async Task LogChangeAsync(string categoryName, string action, string? itemName = null, string? details = null)
    {
        var message = string.IsNullOrEmpty(itemName) 
            ? action 
            : $"{action}: {itemName}";
        
        await LogAsync(categoryName, AuditLogType.Change, message, details);
    }

    /// <summary>
    /// Logs a link operation (add, edit, delete, move).
    /// </summary>
    public async Task LogLinkChangeAsync(string categoryName, string action, string linkTitle, string? url = null)
    {
        var details = !string.IsNullOrEmpty(url) ? $"URL: {url}" : null;
        await LogAsync(categoryName, AuditLogType.Change, $"Link {action}: {linkTitle}", details);
    }

    /// <summary>
    /// Logs a subcategory operation (add, edit, delete).
    /// </summary>
    public async Task LogSubcategoryChangeAsync(string categoryName, string action, string subcategoryName, string? path = null)
    {
        var details = !string.IsNullOrEmpty(path) ? $"Path: {path}" : null;
        await LogAsync(categoryName, AuditLogType.Change, $"Subcategory {action}: {subcategoryName}", details);
    }

    /// <summary>
    /// Logs a security event (invalid password, access denied, etc.).
    /// </summary>
    public async Task LogSecurityEventAsync(string categoryName, string eventDescription, string? details = null)
    {
        await LogAsync(categoryName, AuditLogType.Security, eventDescription, details);
    }

    /// <summary>
    /// Logs an invalid password attempt.
    /// </summary>
    public async Task LogInvalidPasswordAsync(string categoryName, string context = "Category access")
    {
        await LogAsync(categoryName, AuditLogType.Security, "Invalid password entered", context);
    }

    /// <summary>
    /// Logs an error specific to a category.
    /// </summary>
    public async Task LogCategoryErrorAsync(string categoryName, string errorMessage, Exception? exception = null)
    {
        var details = exception != null 
            ? $"{exception.GetType().Name}: {exception.Message}" 
            : null;
        
        await LogAsync(categoryName, AuditLogType.Error, errorMessage, details);
    }

    /// <summary>
    /// Logs a category save operation.
    /// </summary>
    public async Task LogCategorySavedAsync(string categoryName, int linkCount, int subcategoryCount)
    {
        await LogAsync(
            categoryName, 
            AuditLogType.Info, 
            "Category saved", 
            $"Links: {linkCount}, Subcategories: {subcategoryCount}");
    }

    /// <summary>
    /// Logs a category load operation.
    /// </summary>
    public async Task LogCategoryLoadedAsync(string categoryName)
    {
        await LogAsync(categoryName, AuditLogType.Info, "Category loaded");
    }

    /// <summary>
    /// Logs a new category being added.
    /// </summary>
    public async Task LogCategoryAddedAsync(string categoryName, string? description = null)
    {
        var details = !string.IsNullOrEmpty(description) ? $"Description: {description}" : null;
        await LogAsync(categoryName, AuditLogType.Add, "Category created", details);
    }

    /// <summary>
    /// Logs a category being removed.
    /// </summary>
    public async Task LogCategoryRemovedAsync(string categoryName, int linkCount = 0, int subcategoryCount = 0)
    {
        var details = $"Links: {linkCount}, Subcategories: {subcategoryCount}";
        await LogAsync(categoryName, AuditLogType.Remove, "Category removed", details);
    }

    /// <summary>
    /// Logs a category being renamed.
    /// </summary>
    public async Task LogCategoryRenamedAsync(string categoryName, string oldName, string newName)
    {
        await LogAsync(categoryName, AuditLogType.Change, $"Category renamed from '{oldName}' to '{newName}'");
    }

    /// <summary>
    /// Logs a category password change.
    /// </summary>
    public async Task LogCategoryPasswordChangedAsync(string categoryName, PasswordProtectionType oldType, PasswordProtectionType newType)
    {
        string oldTypeStr = GetPasswordProtectionString(oldType);
        string newTypeStr = GetPasswordProtectionString(newType);
        await LogAsync(categoryName, AuditLogType.Change, $"Password protection changed from '{oldTypeStr}' to '{newTypeStr}'");
    }

    /// <summary>
    /// Gets a friendly string for password protection type.
    /// </summary>
    private static string GetPasswordProtectionString(PasswordProtectionType type)
    {
        return type switch
        {
            PasswordProtectionType.None => "None",
            PasswordProtectionType.GlobalPassword => "Global Password",
            PasswordProtectionType.OwnPassword => "Own Password",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the string representation of a log type.
    /// </summary>
    private static string GetLogTypeTag(AuditLogType logType)
    {
        return logType switch
        {
            AuditLogType.Info => "INFO",
            AuditLogType.Warning => "WARNING",
            AuditLogType.Error => "ERROR",
            AuditLogType.Change => "CHANGE",
            AuditLogType.Security => "SECURITY",
            AuditLogType.Add => "ADD",
            AuditLogType.Remove => "REMOVE",
            _ => "INFO"
        };
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    /// <summary>
    /// Reads the audit log for a category.
    /// </summary>
    /// <param name="categoryName">The root category name.</param>
    /// <param name="maxLines">Maximum number of lines to read (0 = all).</param>
    /// <returns>The log content or null if not available.</returns>
    public async Task<string?> ReadLogAsync(string categoryName, int maxLines = 0)
    {
        if (!IsLoggingAvailable)
        {
            return null;
        }

        var logFilePath = GetLogFilePath(categoryName);
        if (!File.Exists(logFilePath))
        {
            return null;
        }

        try
        {
            if (maxLines <= 0)
            {
                return await File.ReadAllTextAsync(logFilePath);
            }

            // Read last N lines
            var lines = await File.ReadAllLinesAsync(logFilePath);
            var startIndex = Math.Max(0, lines.Length - maxLines);
            return string.Join(Environment.NewLine, lines.Skip(startIndex));
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.ReadLogAsync",
                $"Failed to read audit log for category '{categoryName}'",
                ex);
            return null;
        }
    }

    /// <summary>
    /// Clears the audit log for a category (starts fresh with line number 1).
    /// </summary>
    public async Task ClearLogAsync(string categoryName)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        var logFilePath = GetLogFilePath(categoryName);
        if (File.Exists(logFilePath))
        {
            try
            {
                File.Delete(logFilePath);
                await LogAsync(categoryName, AuditLogType.Info, "Audit log cleared");
            }
            catch (Exception ex)
            {
                LogUtilities.LogError(
                    "AuditLogService.ClearLogAsync",
                    $"Failed to clear audit log for category '{categoryName}'",
                    ex);
            }
        }
    }

    /// <summary>
    /// Deletes the audit log file for a category.
    /// </summary>
    public void DeleteLog(string categoryName)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        var logFilePath = GetLogFilePath(categoryName);
        if (File.Exists(logFilePath))
        {
            try
            {
                File.Delete(logFilePath);
            }
            catch (Exception ex)
            {
                LogUtilities.LogError(
                    "AuditLogService.DeleteLog",
                    $"Failed to delete audit log for category '{categoryName}'",
                    ex);
            }
        }
    }

    /// <summary>
    /// Gets the current entry count in the log file.
    /// </summary>
    public int GetEntryCount(string categoryName)
    {
        var logFilePath = GetLogFilePath(categoryName);
        return GetCurrentLineCount(logFilePath);
    }

    /// <summary>
    /// Renames the audit log file when a category is renamed.
    /// Logs the rename action to the new log file.
    /// </summary>
    /// <param name="oldCategoryName">The old category name.</param>
    /// <param name="newCategoryName">The new category name.</param>
    /// <returns>True if the rename was successful, false otherwise.</returns>
    public async Task<bool> RenameLogAsync(string oldCategoryName, string newCategoryName)
    {
        if (!IsLoggingAvailable)
        {
            return false;
        }

        var oldLogPath = GetLogFilePath(oldCategoryName);
        var newLogPath = GetLogFilePath(newCategoryName);

        try
        {
            // Check if old log file exists
            if (!File.Exists(oldLogPath))
            {
                // No log file to rename - just log the rename to the new log
                await LogAsync(newCategoryName, AuditLogType.Info, 
                    $"Category renamed from '{oldCategoryName}' to '{newCategoryName}'", 
                    "No previous log file existed");
                return true;
            }

            // Check if new log file already exists (shouldn't happen but handle it)
            if (File.Exists(newLogPath))
            {
                // Append the old log content to the new log file
                var oldContent = await File.ReadAllTextAsync(oldLogPath);
                await File.AppendAllTextAsync(newLogPath, oldContent);
                File.Delete(oldLogPath);
                
                await LogAsync(newCategoryName, AuditLogType.Info, 
                    $"Category renamed from '{oldCategoryName}' to '{newCategoryName}'", 
                    "Log files merged");
                return true;
            }

            // Rename the log file
            File.Move(oldLogPath, newLogPath);

            // Log the rename action to the new log file
            await LogAsync(newCategoryName, AuditLogType.Info, 
                $"Category renamed from '{oldCategoryName}' to '{newCategoryName}'", 
                "Log file renamed");

            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.RenameLogAsync",
                $"Failed to rename audit log from '{oldCategoryName}' to '{newCategoryName}'",
                ex);
            return false;
        }
    }

    /// <summary>
    /// Logs a category being removed to a SYSTEM log (not the category's own log).
    /// This ensures the removal is logged even after the category and its log file are deleted.
    /// </summary>
    public async Task LogCategoryRemovedToSystemLogAsync(string categoryName, int linkCount = 0, int subcategoryCount = 0)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            // Log to a system-level log file that persists after category deletion
            var systemLogPath = Path.Combine(_logDirectory, "system.log");
            
            // Get next line number
            int lineNumber = 1;
            if (File.Exists(systemLogPath))
            {
                try
                {
                    lineNumber = File.ReadLines(systemLogPath).Count() + 1;
                }
                catch
                {
                    lineNumber = 1;
                }
            }
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var details = $"Links: {linkCount}, Subcategories: {subcategoryCount}";
            var logEntry = $"{lineNumber}|[{timestamp}] [REMOVE] Category removed: {categoryName} | {details}{Environment.NewLine}";

            lock (_lockObject)
            {
                File.AppendAllText(systemLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.LogCategoryRemovedToSystemLogAsync",
                $"Failed to log category removal to system log for '{categoryName}'",
                ex);
        }
    }

    /// <summary>
    /// Logs a category being added to a SYSTEM log.
    /// </summary>
    public async Task LogCategoryAddedToSystemLogAsync(string categoryName, string? description = null)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            var systemLogPath = Path.Combine(_logDirectory, "system.log");
            
            int lineNumber = 1;
            if (File.Exists(systemLogPath))
            {
                try
                {
                    lineNumber = File.ReadLines(systemLogPath).Count() + 1;
                }
                catch
                {
                    lineNumber = 1;
                }
            }
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var details = !string.IsNullOrEmpty(description) ? $" | Description: {description}" : "";
            var logEntry = $"{lineNumber}|[{timestamp}] [ADD] Category created: {categoryName}{details}{Environment.NewLine}";

            lock (_lockObject)
            {
                File.AppendAllText(systemLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.LogCategoryAddedToSystemLogAsync",
                $"Failed to log category addition to system log for '{categoryName}'",
                ex);
        }
    }

    /// <summary>
    /// Logs a category being renamed to a SYSTEM log.
    /// </summary>
    public async Task LogCategoryRenamedToSystemLogAsync(string oldName, string newName)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            var systemLogPath = Path.Combine(_logDirectory, "system.log");
            
            int lineNumber = 1;
            if (File.Exists(systemLogPath))
            {
                try
                {
                    lineNumber = File.ReadLines(systemLogPath).Count() + 1;
                }
                catch
                {
                    lineNumber = 1;
                }
            }
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"{lineNumber}|[{timestamp}] [CHANGE] Category renamed from '{oldName}' to '{newName}'{Environment.NewLine}";

            lock (_lockObject)
            {
                File.AppendAllText(systemLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.LogCategoryRenamedToSystemLogAsync",
                $"Failed to log category rename to system log",
                ex);
        }
    }

    /// <summary>
    /// Logs a category password change to a SYSTEM log.
    /// </summary>
    public async Task LogCategoryPasswordChangedToSystemLogAsync(string categoryName, PasswordProtectionType oldType, PasswordProtectionType newType)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            var systemLogPath = Path.Combine(_logDirectory, "system.log");
            
            int lineNumber = 1;
            if (File.Exists(systemLogPath))
            {
                try
                {
                    lineNumber = File.ReadLines(systemLogPath).Count() + 1;
                }
                catch
                {
                    lineNumber = 1;
                }
            }
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string oldTypeStr = GetPasswordProtectionString(oldType);
            string newTypeStr = GetPasswordProtectionString(newType);
            var logEntry = $"{lineNumber}|[{timestamp}] [CHANGE] Category '{categoryName}' password protection changed from '{oldTypeStr}' to '{newTypeStr}'{Environment.NewLine}";

            lock (_lockObject)
            {
                File.AppendAllText(systemLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "AuditLogService.LogCategoryPasswordChangedToSystemLogAsync",
                $"Failed to log category password change to system log",
                ex);
        }
    }

    /// <summary>
    /// Logs a catalog refresh operation.
    /// </summary>
    public async Task LogCatalogRefreshAsync(string categoryName, string linkTitle, int fileCount)
    {
        await LogAsync(
            categoryName, 
            AuditLogType.Change, 
            $"Catalog refreshed: {linkTitle}", 
            $"Files: {fileCount}");
    }
}
