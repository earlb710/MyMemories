using System;
using System.Diagnostics;

namespace MyMemories.Utilities;

public static class LogUtilities
{
    /// <summary>
    /// Logs an error to Debug output and optionally to status bar.
    /// </summary>
    public static void LogError(
        string method, 
        string message, 
        Exception? ex = null,
        Action<string>? updateStatus = null)
    {
        var logMessage = $"[{method}] {message}";
        if (ex != null)
            logMessage += $": {ex.Message}";
        
        Debug.WriteLine(logMessage);
        
        updateStatus?.Invoke(message);
    }
}