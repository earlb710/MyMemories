using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for managing the generic system error log (error.log).
/// Records system errors with timestamps in a centralized log file.
/// Log entries include line numbers and automatic rotation after 2000 entries when older than 1 day.
/// </summary>
public class ErrorLogService
{
    private readonly string _logDirectory;
    private const string ErrorLogFileName = "error.log";
    private const int MaxEntriesBeforeRotation = 2000;
    private static readonly object _lockObject = new();

    public ErrorLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        
        if (!string.IsNullOrEmpty(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    /// <summary>
    /// Checks if error logging is available (log directory is configured).
    /// </summary>
    public bool IsLoggingAvailable => !string.IsNullOrEmpty(_logDirectory) && Directory.Exists(_logDirectory);

    /// <summary>
    /// Gets the full path to the error log file.
    /// </summary>
    public string ErrorLogPath => Path.Combine(_logDirectory, ErrorLogFileName);

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
    private void RotateLogIfNeeded()
    {
        if (!File.Exists(ErrorLogPath))
        {
            return;
        }

        try
        {
            var lineCount = GetCurrentLineCount(ErrorLogPath);
            if (lineCount < MaxEntriesBeforeRotation)
            {
                return; // Not enough entries to rotate
            }

            var firstEntryDate = GetFirstEntryDate(ErrorLogPath);
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
            var archiveDateStr = firstEntryDate.Value.ToString("yyyy-MM-dd");
            var archivePath = Path.Combine(_logDirectory, $"error_{archiveDateStr}.log");

            // If archive already exists, add a counter
            var counter = 1;
            while (File.Exists(archivePath))
            {
                archivePath = Path.Combine(_logDirectory, $"error_{archiveDateStr}_{counter}.log");
                counter++;
            }

            File.Move(ErrorLogPath, archivePath);
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.RotateLogIfNeeded",
                "Failed to rotate error log",
                ex);
        }
    }

    /// <summary>
    /// Logs a system error with timestamp and line number.
    /// </summary>
    /// <param name="errorMessage">The error message to log.</param>
    /// <param name="source">The source of the error (class/method name).</param>
    /// <param name="exception">Optional exception details.</param>
    public async Task LogErrorAsync(string errorMessage, string source, Exception? exception = null)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            // Check for rotation before writing
            lock (_lockObject)
            {
                RotateLogIfNeeded();
            }

            var logEntry = BuildLogEntry(errorMessage, source, exception);
            
            lock (_lockObject)
            {
                File.AppendAllText(ErrorLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            // Fall back to debug logging if file logging fails
            LogUtilities.LogError(
                "ErrorLogService.LogErrorAsync",
                $"Failed to write to error log: {errorMessage}",
                ex);
        }
    }

    /// <summary>
    /// Logs a system error synchronously.
    /// </summary>
    public void LogError(string errorMessage, string source, Exception? exception = null)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            lock (_lockObject)
            {
                // Check for rotation before writing
                RotateLogIfNeeded();
                
                var logEntry = BuildLogEntry(errorMessage, source, exception);
                File.AppendAllText(ErrorLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.LogError",
                $"Failed to write to error log: {errorMessage}",
                ex);
        }
    }

    /// <summary>
    /// Builds a formatted log entry with line number.
    /// </summary>
    private string BuildLogEntry(string errorMessage, string source, Exception? exception)
    {
        var sb = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        // Get the next line number
        int lineNumber = GetCurrentLineCount(ErrorLogPath) + 1;
        
        sb.AppendLine($"{lineNumber}|[{timestamp}] [ERROR] [{source}]");
        
        // Increment line number for each additional line in the entry
        lineNumber++;
        sb.AppendLine($"{lineNumber}|Message: {errorMessage}");

        if (exception != null)
        {
            lineNumber++;
            sb.AppendLine($"{lineNumber}|Exception Type: {exception.GetType().FullName}");
            lineNumber++;
            sb.AppendLine($"{lineNumber}|Exception Message: {exception.Message}");
            lineNumber++;
            sb.AppendLine($"{lineNumber}|Source: {exception.Source}");
            lineNumber++;
            sb.AppendLine($"{lineNumber}|HResult: 0x{exception.HResult:X8}");

            if (exception.InnerException != null)
            {
                lineNumber++;
                sb.AppendLine($"{lineNumber}|");
                lineNumber++;
                sb.AppendLine($"{lineNumber}|Inner Exception:");
                lineNumber++;
                sb.AppendLine($"{lineNumber}|  Type: {exception.InnerException.GetType().FullName}");
                lineNumber++;
                sb.AppendLine($"{lineNumber}|  Message: {exception.InnerException.Message}");
            }

            if (exception.Data != null && exception.Data.Count > 0)
            {
                lineNumber++;
                sb.AppendLine($"{lineNumber}|");
                lineNumber++;
                sb.AppendLine($"{lineNumber}|Additional Data:");
                foreach (var key in exception.Data.Keys)
                {
                    lineNumber++;
                    sb.AppendLine($"{lineNumber}|  {key}: {exception.Data[key]}");
                }
            }

            lineNumber++;
            sb.AppendLine($"{lineNumber}|");
            lineNumber++;
            sb.AppendLine($"{lineNumber}|Stack Trace:");
            
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                var stackLines = exception.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var stackLine in stackLines)
                {
                    lineNumber++;
                    sb.AppendLine($"{lineNumber}|{stackLine}");
                }
            }

            if (exception.InnerException?.StackTrace != null)
            {
                lineNumber++;
                sb.AppendLine($"{lineNumber}|");
                lineNumber++;
                sb.AppendLine($"{lineNumber}|Inner Stack Trace:");
                var innerStackLines = exception.InnerException.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var stackLine in innerStackLines)
                {
                    lineNumber++;
                    sb.AppendLine($"{lineNumber}|{stackLine}");
                }
            }
        }

        lineNumber++;
        sb.AppendLine($"{lineNumber}|{new string('-', 80)}");
        lineNumber++;
        sb.AppendLine($"{lineNumber}|");

        return sb.ToString();
    }

    /// <summary>
    /// Logs a warning message with line number.
    /// </summary>
    public async Task LogWarningAsync(string warningMessage, string source)
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            lock (_lockObject)
            {
                // Check for rotation before writing
                RotateLogIfNeeded();
                
                int lineNumber = GetCurrentLineCount(ErrorLogPath) + 1;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"{lineNumber}|[{timestamp}] [WARNING] [{source}] {warningMessage}{Environment.NewLine}";
                
                File.AppendAllText(ErrorLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.LogWarningAsync",
                $"Failed to write warning to error log: {warningMessage}",
                ex);
        }
    }

    /// <summary>
    /// Reads the error log content.
    /// </summary>
    /// <param name="maxLines">Maximum number of lines to read from the end (0 = all).</param>
    public async Task<string?> ReadLogAsync(int maxLines = 0)
    {
        if (!IsLoggingAvailable || !File.Exists(ErrorLogPath))
        {
            return null;
        }

        try
        {
            if (maxLines <= 0)
            {
                return await File.ReadAllTextAsync(ErrorLogPath);
            }

            var lines = await File.ReadAllLinesAsync(ErrorLogPath);
            var startIndex = Math.Max(0, lines.Length - maxLines);
            return string.Join(Environment.NewLine, lines.Skip(startIndex));
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.ReadLogAsync",
                "Failed to read error log",
                ex);
            return null;
        }
    }

    /// <summary>
    /// Clears the error log (deletes and starts fresh with line number 1).
    /// </summary>
    public async Task ClearLogAsync()
    {
        if (!IsLoggingAvailable)
        {
            return;
        }

        try
        {
            lock (_lockObject)
            {
                if (File.Exists(ErrorLogPath))
                {
                    File.Delete(ErrorLogPath);
                }
                
                // Start new log with line 1
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"1|[{timestamp}] [INFO] [ErrorLogService] Error log cleared{Environment.NewLine}";
                File.WriteAllText(ErrorLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.ClearLogAsync",
                "Failed to clear error log",
                ex);
        }
    }

    /// <summary>
    /// Gets the size of the error log file in bytes.
    /// </summary>
    public long GetLogSize()
    {
        if (!IsLoggingAvailable || !File.Exists(ErrorLogPath))
        {
            return 0;
        }

        try
        {
            return new FileInfo(ErrorLogPath).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets the current entry count in the error log.
    /// </summary>
    public int GetEntryCount()
    {
        return GetCurrentLineCount(ErrorLogPath);
    }

    /// <summary>
    /// Rotates the error log if it exceeds the specified size (legacy method - size-based).
    /// Note: The service now uses entry count + age based rotation automatically.
    /// </summary>
    /// <param name="maxSizeInBytes">Maximum size in bytes before rotation (default: 10MB).</param>
    public async Task RotateLogIfNeededAsync(long maxSizeInBytes = 10 * 1024 * 1024)
    {
        if (!IsLoggingAvailable || !File.Exists(ErrorLogPath))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(ErrorLogPath);
            if (fileInfo.Length <= maxSizeInBytes)
            {
                return;
            }

            // Get first entry date for archive name
            var firstEntryDate = GetFirstEntryDate(ErrorLogPath);
            var archiveDateStr = firstEntryDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var archivePath = Path.Combine(_logDirectory, $"error_{archiveDateStr}.log");
            
            // If archive already exists, add a counter
            var counter = 1;
            while (File.Exists(archivePath))
            {
                archivePath = Path.Combine(_logDirectory, $"error_{archiveDateStr}_{counter}.log");
                counter++;
            }

            File.Move(ErrorLogPath, archivePath);
            
            // Start new log with line 1
            lock (_lockObject)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"1|[{timestamp}] [INFO] [ErrorLogService] New error log started. Previous log archived to: {Path.GetFileName(archivePath)}{Environment.NewLine}";
                File.WriteAllText(ErrorLogPath, logEntry);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ErrorLogService.RotateLogIfNeededAsync",
                "Failed to rotate error log",
                ex);
        }
    }
}
