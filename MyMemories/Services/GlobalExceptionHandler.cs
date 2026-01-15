using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Centralized global exception handler that catches unhandled exceptions
/// from UI thread, background threads, and async tasks.
/// </summary>
public class GlobalExceptionHandler
{
    private static GlobalExceptionHandler? _instance;
    private static readonly object _lock = new object();
    
    private ErrorLogService? _errorLogger;
    private XamlRoot? _xamlRoot;
    private bool _isHandlingException = false;

    /// <summary>
    /// Gets the singleton instance of the global exception handler.
    /// </summary>
    public static GlobalExceptionHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GlobalExceptionHandler();
                }
            }
            return _instance;
        }
    }

    private GlobalExceptionHandler()
    {
    }

    /// <summary>
    /// Initializes the global exception handler with required services.
    /// </summary>
    public void Initialize(ErrorLogService? errorLogger, XamlRoot? xamlRoot)
    {
        _errorLogger = errorLogger;
        _xamlRoot = xamlRoot;
        
        Debug.WriteLine("[GlobalExceptionHandler] Initialized with error logger and XamlRoot");
    }

    /// <summary>
    /// Handles an unhandled exception from the UI thread.
    /// </summary>
    public void HandleUnhandledException(Exception exception, string source = "Unknown")
    {
        // Prevent recursive error handling
        if (_isHandlingException)
        {
            Debug.WriteLine($"[GlobalExceptionHandler] Recursive exception detected, ignoring: {exception.Message}");
            return;
        }

        _isHandlingException = true;

        try
        {
            // Log the exception
            LogException(exception, source, "Unhandled UI Exception");

            // Show user-friendly error dialog
            _ = ShowErrorDialogAsync(exception, source, isUnhandled: true);
        }
        catch (Exception handlerEx)
        {
            // Last resort: write to debug output
            Debug.WriteLine($"[GlobalExceptionHandler] FATAL: Exception handler failed: {handlerEx.Message}");
            Debug.WriteLine($"[GlobalExceptionHandler] Original exception: {exception}");
        }
        finally
        {
            _isHandlingException = false;
        }
    }

    /// <summary>
    /// Handles an unobserved task exception from async operations.
    /// </summary>
    public void HandleUnobservedTaskException(Exception exception)
    {
        // Log but don't show dialog for unobserved task exceptions
        // as they often occur during shutdown or are less critical
        LogException(exception, "Async Task", "Unobserved Task Exception");
        
        Debug.WriteLine($"[GlobalExceptionHandler] Unobserved task exception: {exception.Message}");
    }

    /// <summary>
    /// Handles a known exception with user-friendly message.
    /// Use this for expected/handled exceptions that need user notification.
    /// </summary>
    public async Task HandleKnownExceptionAsync(Exception exception, string userMessage, string source = "Application")
    {
        // Log the exception
        LogException(exception, source, "Known Exception");

        // Show user-friendly error dialog
        await ShowErrorDialogAsync(exception, source, isUnhandled: false, customMessage: userMessage);
    }

    /// <summary>
    /// Logs an exception using the error logging service.
    /// </summary>
    private void LogException(Exception exception, string source, string category)
    {
        var logMessage = FormatExceptionForLog(exception, source, category);
        
        try
        {
            // Try to use the error logger if available
            if (_errorLogger != null)
            {
                _ = _errorLogger.LogErrorAsync(logMessage, source);
            }
        }
        catch
        {
            // If error logger fails, fall back to debug output
        }

        // Always write to debug output
        Debug.WriteLine($"[GlobalExceptionHandler] {category}");
        Debug.WriteLine($"  Source: {source}");
        Debug.WriteLine($"  Exception: {exception.GetType().Name}");
        Debug.WriteLine($"  Message: {exception.Message}");
        Debug.WriteLine($"  StackTrace: {exception.StackTrace}");
        
        if (exception.InnerException != null)
        {
            Debug.WriteLine($"  InnerException: {exception.InnerException.GetType().Name}");
            Debug.WriteLine($"  InnerMessage: {exception.InnerException.Message}");
        }
    }

    /// <summary>
    /// Formats exception details for logging.
    /// </summary>
    private string FormatExceptionForLog(Exception exception, string source, string category)
    {
        var message = $"{category}\n" +
                      $"Source: {source}\n" +
                      $"Exception Type: {exception.GetType().FullName}\n" +
                      $"Message: {exception.Message}\n" +
                      $"Stack Trace:\n{exception.StackTrace}";

        if (exception.InnerException != null)
        {
            message += $"\n\nInner Exception:\n" +
                       $"Type: {exception.InnerException.GetType().FullName}\n" +
                       $"Message: {exception.InnerException.Message}\n" +
                       $"Stack Trace:\n{exception.InnerException.StackTrace}";
        }

        return message;
    }

    /// <summary>
    /// Shows a user-friendly error dialog.
    /// </summary>
    private async Task ShowErrorDialogAsync(Exception exception, string source, bool isUnhandled, string? customMessage = null)
    {
        if (_xamlRoot == null)
        {
            Debug.WriteLine("[GlobalExceptionHandler] Cannot show error dialog - XamlRoot is null");
            return;
        }

        try
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = isUnhandled ? "?? Unexpected Error" : "? Error",
                XamlRoot = _xamlRoot,
                PrimaryButtonText = "OK",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary
            };

            // Build user-friendly message
            string userMessage;
            if (!string.IsNullOrEmpty(customMessage))
            {
                userMessage = customMessage;
            }
            else
            {
                userMessage = GetUserFriendlyMessage(exception, source, isUnhandled);
            }

            // Create content with message and details expander
            var content = new Microsoft.UI.Xaml.Controls.StackPanel
            {
                Spacing = 12
            };

            // Main message
            var messageBlock = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = userMessage,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            content.Children.Add(messageBlock);

            // Technical details expander (for advanced users)
            var expander = new Microsoft.UI.Xaml.Controls.Expander
            {
                Header = "Technical Details",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var technicalDetails = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = $"Exception Type: {exception.GetType().Name}\n" +
                       $"Message: {exception.Message}\n" +
                       $"Source: {source}",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12,
                MaxHeight = 200
            };

            var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer
            {
                Content = technicalDetails,
                VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto
            };

            expander.Content = scrollViewer;
            content.Children.Add(expander);

            dialog.Content = content;

            await dialog.ShowAsync();
        }
        catch (Exception dialogEx)
        {
            Debug.WriteLine($"[GlobalExceptionHandler] Failed to show error dialog: {dialogEx.Message}");
        }
    }

    /// <summary>
    /// Generates a user-friendly error message based on the exception type.
    /// </summary>
    private string GetUserFriendlyMessage(Exception exception, string source, bool isUnhandled)
    {
        var baseMessage = isUnhandled 
            ? "An unexpected error occurred in the application." 
            : "An error occurred.";

        var specificMessage = exception switch
        {
            UnauthorizedAccessException => "Access to a file or folder was denied. Please check permissions.",
            IOException ioEx when ioEx is FileNotFoundException => "A required file was not found.",
            IOException ioEx when ioEx is DirectoryNotFoundException => "A required folder was not found.",
            IOException => "A file operation failed. The file may be in use or the disk may be full.",
            OutOfMemoryException => "The application ran out of memory. Try closing other applications.",
            ArgumentException => "Invalid input was provided to the operation.",
            InvalidOperationException => "The operation could not be completed in the current state.",
            NullReferenceException when isUnhandled => "A critical error occurred in the application.",
            TaskCanceledException => "The operation was cancelled.",
            TimeoutException => "The operation timed out.",
            _ => null
        };

        if (specificMessage != null)
        {
            return $"{baseMessage}\n\n{specificMessage}\n\nError Source: {source}";
        }

        return $"{baseMessage}\n\nError: {exception.Message}\nSource: {source}";
    }

    /// <summary>
    /// Determines if an exception is critical and the application should terminate.
    /// </summary>
    public bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException ||
               exception is StackOverflowException ||
               exception is AccessViolationException ||
               exception is AppDomainUnloadedException;
    }
}
