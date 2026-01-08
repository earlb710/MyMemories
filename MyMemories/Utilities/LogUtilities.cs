using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
    public static void LogDebug(
        string method, 
        string message,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (IsDevelopmentMode)
        {
            var fileName = System.IO.Path.GetFileName(sourceFile);
            Debug.WriteLine($"[DEBUG] [{fileName}:{lineNumber}] [{method}] {message}");
        }
    }

    /// <summary>
    /// Logs an error message with comprehensive context information.
    /// </summary>
    public static void LogError(
        string method, 
        string message, 
        Exception? ex = null,
        Action<string>? updateStatus = null,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMethod = "")
    {
        var fileName = System.IO.Path.GetFileName(sourceFile);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        var logMessage = $"[ERROR] [{timestamp}] [{fileName}:{lineNumber}] [{method}]";
        
        // Add caller method if different from the method parameter
        if (!string.IsNullOrEmpty(callerMethod) && callerMethod != method)
        {
            logMessage += $" (Called from: {callerMethod})";
        }
        
        logMessage += $" {message}";
        
        if (IsDevelopmentMode)
        {
            Debug.WriteLine(logMessage);
            
            if (ex != null)
            {
                Debug.WriteLine($"[ERROR] Exception Details:");
                Debug.WriteLine($"  Type: {ex.GetType().FullName}");
                Debug.WriteLine($"  Message: {ex.Message}");
                Debug.WriteLine($"  Source: {ex.Source}");
                
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                    Debug.WriteLine($"  Inner Message: {ex.InnerException.Message}");
                }
                
                if (ex.Data != null && ex.Data.Count > 0)
                {
                    Debug.WriteLine("  Additional Data:");
                    foreach (var key in ex.Data.Keys)
                    {
                        Debug.WriteLine($"    {key}: {ex.Data[key]}");
                    }
                }
                
                Debug.WriteLine($"  Stack Trace:");
                Debug.WriteLine($"{ex.StackTrace}");
                
                if (ex.InnerException != null && ex.InnerException.StackTrace != null)
                {
                    Debug.WriteLine($"  Inner Stack Trace:");
                    Debug.WriteLine($"{ex.InnerException.StackTrace}");
                }
            }
        }
        
        updateStatus?.Invoke(message);
    }

    /// <summary>
    /// Logs an error with additional context parameters.
    /// </summary>
    public static void LogErrorWithContext(
        string method,
        string message,
        Exception? ex = null,
        object? contextData = null,
        Action<string>? updateStatus = null,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMethod = "")
    {
        var fileName = System.IO.Path.GetFileName(sourceFile);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        var logMessage = $"[ERROR] [{timestamp}] [{fileName}:{lineNumber}] [{method}]";
        
        if (!string.IsNullOrEmpty(callerMethod) && callerMethod != method)
        {
            logMessage += $" (Called from: {callerMethod})";
        }
        
        logMessage += $" {message}";
        
        if (IsDevelopmentMode)
        {
            Debug.WriteLine(logMessage);
            
            // Log context data if provided
            if (contextData != null)
            {
                Debug.WriteLine($"[ERROR] Context Data:");
                var contextType = contextData.GetType();
                
                if (contextType.IsPrimitive || contextType == typeof(string))
                {
                    Debug.WriteLine($"  Value: {contextData}");
                }
                else
                {
                    // Use reflection to log object properties
                    var properties = contextType.GetProperties();
                    foreach (var prop in properties)
                    {
                        try
                        {
                            var value = prop.GetValue(contextData);
                            Debug.WriteLine($"  {prop.Name}: {value ?? "null"}");
                        }
                        catch
                        {
                            Debug.WriteLine($"  {prop.Name}: <unable to read>");
                        }
                    }
                }
            }
            
            if (ex != null)
            {
                Debug.WriteLine($"[ERROR] Exception Details:");
                Debug.WriteLine($"  Type: {ex.GetType().FullName}");
                Debug.WriteLine($"  Message: {ex.Message}");
                Debug.WriteLine($"  Source: {ex.Source}");
                Debug.WriteLine($"  HResult: 0x{ex.HResult:X8}");
                
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"  Inner Exception: {ex.InnerException.GetType().FullName}");
                    Debug.WriteLine($"  Inner Message: {ex.InnerException.Message}");
                }
                
                if (ex.Data != null && ex.Data.Count > 0)
                {
                    Debug.WriteLine("  Additional Data:");
                    foreach (var key in ex.Data.Keys)
                    {
                        Debug.WriteLine($"    {key}: {ex.Data[key]}");
                    }
                }
                
                Debug.WriteLine($"  Stack Trace:");
                Debug.WriteLine($"{ex.StackTrace}");
                
                if (ex.InnerException != null && ex.InnerException.StackTrace != null)
                {
                    Debug.WriteLine($"  Inner Stack Trace:");
                    Debug.WriteLine($"{ex.InnerException.StackTrace}");
                }
            }
        }
        
        updateStatus?.Invoke(message);
    }

    /// <summary>
    /// Logs a warning message (only in development mode).
    /// </summary>
    public static void LogWarning(
        string method, 
        string message,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (IsDevelopmentMode)
        {
            var fileName = System.IO.Path.GetFileName(sourceFile);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Debug.WriteLine($"[WARNING] [{timestamp}] [{fileName}:{lineNumber}] [{method}] {message}");
        }
    }

    /// <summary>
    /// Logs an info message (only in development mode).
    /// </summary>
    public static void LogInfo(
        string method, 
        string message,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (IsDevelopmentMode)
        {
            var fileName = System.IO.Path.GetFileName(sourceFile);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Debug.WriteLine($"[INFO] [{timestamp}] [{fileName}:{lineNumber}] [{method}] {message}");
        }
    }

    /// <summary>
    /// Logs a performance metric (only in development mode).
    /// </summary>
    public static void LogPerformance(
        string method,
        string operation,
        TimeSpan duration,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (IsDevelopmentMode)
        {
            var fileName = System.IO.Path.GetFileName(sourceFile);
            Debug.WriteLine($"[PERF] [{fileName}:{lineNumber}] [{method}] {operation} completed in {duration.TotalMilliseconds:F2}ms");
        }
    }
}