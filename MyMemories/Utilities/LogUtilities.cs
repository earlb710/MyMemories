using System;
using System.Diagnostics;

namespace MyMemories.Utilities;

public static class LogUtilities
{
    /// <summary>
    /// Development mode flag. Set to true to enable debug messages.
    /// </summary>
#if DEBUG
    public static bool IsDevelopmentMode { get; set; } = true;
#else
    public static bool IsDevelopmentMode { get; set; } = false;
#endif

    /// <summary>
    /// Logs a debug message (only in development mode).
    /// </summary>
    public static void LogDebug(string method, string message)
    {
        if (IsDevelopmentMode)
        {
            Debug.WriteLine($"[DEBUG] [{method}] {message}");
        }
    }

    /// <summary>
    /// Logs an error message (always logged in development mode).
    /// </summary>
    public static void LogError(
        string method, 
        string message, 
        Exception? ex = null,
        Action<string>? updateStatus = null)
    {
        var logMessage = $"[ERROR] [{method}] {message}";
        if (ex != null)
            logMessage += $": {ex.Message}";
        
        if (IsDevelopmentMode)
        {
            Debug.WriteLine(logMessage);
            if (ex != null)
            {
                Debug.WriteLine($"[ERROR] [{method}] Exception Type: {ex.GetType().Name}");
                Debug.WriteLine($"[ERROR] [{method}] Stack trace: {ex.StackTrace}");
            }
        }
        
        updateStatus?.Invoke(message);
    }

    /// <summary>
    /// Logs a warning message (only in development mode).
    /// </summary>
    public static void LogWarning(string method, string message)
    {
        if (IsDevelopmentMode)
        {
            Debug.WriteLine($"[WARNING] [{method}] {message}");
        }
    }

    /// <summary>
    /// Logs an info message (only in development mode).
    /// </summary>
    public static void LogInfo(string method, string message)
    {
        if (IsDevelopmentMode)
        {
            Debug.WriteLine($"[INFO] [{method}] {message}");
        }
    }
}