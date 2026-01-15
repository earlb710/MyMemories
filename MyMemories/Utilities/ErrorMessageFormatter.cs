using System;
using System.IO;
using System.Text;

namespace MyMemories.Utilities;

/// <summary>
/// Provides consistent error message formatting across the application.
/// Centralizes error message creation to ensure uniform user experience.
/// </summary>
public static class ErrorMessageFormatter
{
    /// <summary>
    /// Formats a generic error message with context.
    /// </summary>
    public static string FormatError(string operation, string details)
    {
        return $"Failed to {operation}.\n\nDetails: {details}";
    }

    /// <summary>
    /// Formats an error message from an exception.
    /// </summary>
    public static string FormatError(string operation, Exception ex)
    {
        return FormatError(operation, ex.Message);
    }

    /// <summary>
    /// Formats a file operation error with helpful context.
    /// </summary>
    public static string FormatFileError(string operation, string filePath, Exception ex)
    {
        var message = new StringBuilder();
        message.AppendLine($"Failed to {operation} file.");
        message.AppendLine();
        message.AppendLine($"File: {Path.GetFileName(filePath)}");
        message.AppendLine($"Location: {Path.GetDirectoryName(filePath)}");
        message.AppendLine();

        // Add user-friendly explanation based on exception type
        message.AppendLine(ex switch
        {
            UnauthorizedAccessException => "You don't have permission to access this file. Check file permissions.",
            FileNotFoundException => "The file could not be found. It may have been moved or deleted.",
            DirectoryNotFoundException => "The folder could not be found. It may have been moved or deleted.",
            PathTooLongException => "The file path is too long. Try moving the file to a location with a shorter path.",
            IOException ioEx when ioEx.Message.Contains("being used") => "The file is currently in use by another program. Close it and try again.",
            IOException => "An I/O error occurred. The disk may be full or the file may be corrupted.",
            _ => $"Error: {ex.Message}"
        });

        return message.ToString();
    }

    /// <summary>
    /// Formats a directory operation error with helpful context.
    /// </summary>
    public static string FormatDirectoryError(string operation, string directoryPath, Exception ex)
    {
        var message = new StringBuilder();
        message.AppendLine($"Failed to {operation} folder.");
        message.AppendLine();
        message.AppendLine($"Folder: {Path.GetFileName(directoryPath)}");
        message.AppendLine($"Location: {Path.GetDirectoryName(directoryPath)}");
        message.AppendLine();

        message.AppendLine(ex switch
        {
            UnauthorizedAccessException => "You don't have permission to access this folder. Check folder permissions.",
            DirectoryNotFoundException => "The folder could not be found. It may have been moved or deleted.",
            PathTooLongException => "The folder path is too long. Try using a shorter path.",
            IOException => "An I/O error occurred. The disk may be full or the folder may be in use.",
            _ => $"Error: {ex.Message}"
        });

        return message.ToString();
    }

    /// <summary>
    /// Formats a validation error message.
    /// </summary>
    public static string FormatValidationError(string field, string requirement)
    {
        return $"Invalid {field}.\n\nRequirement: {requirement}";
    }

    /// <summary>
    /// Formats a validation error message with multiple issues.
    /// </summary>
    public static string FormatValidationErrors(string context, params string[] errors)
    {
        var message = new StringBuilder();
        message.AppendLine($"Validation failed for {context}:");
        message.AppendLine();

        foreach (var error in errors)
        {
            message.AppendLine($"• {error}");
        }

        return message.ToString();
    }

    /// <summary>
    /// Formats a network/URL error message.
    /// </summary>
    public static string FormatNetworkError(string operation, string url, Exception ex)
    {
        var message = new StringBuilder();
        message.AppendLine($"Failed to {operation}.");
        message.AppendLine();
        message.AppendLine($"URL: {url}");
        message.AppendLine();

        message.AppendLine(ex switch
        {
            System.Net.WebException => "Could not connect to the server. Check your internet connection.",
            System.Net.Http.HttpRequestException => "The server returned an error. The page may not exist or the server may be down.",
            UriFormatException => "The URL format is invalid.",
            TimeoutException => "The operation timed out. The server may be slow or unreachable.",
            _ => $"Error: {ex.Message}"
        });

        return message.ToString();
    }

    /// <summary>
    /// Formats a database/data operation error.
    /// </summary>
    public static string FormatDataError(string operation, string dataType, Exception ex)
    {
        return $"Failed to {operation} {dataType}.\n\n" +
               $"The data may be corrupted or in an unexpected format.\n\n" +
               $"Error: {ex.Message}";
    }

    /// <summary>
    /// Formats a permission denied error.
    /// </summary>
    public static string FormatPermissionError(string operation, string resource)
    {
        return $"Permission denied.\n\n" +
               $"You don't have permission to {operation} {resource}.\n\n" +
               $"Contact your system administrator or check the file/folder permissions.";
    }

    /// <summary>
    /// Formats a resource not found error.
    /// </summary>
    public static string FormatNotFoundError(string resourceType, string resourceName)
    {
        return $"{resourceType} not found.\n\n" +
               $"Could not find {resourceType}: {resourceName}\n\n" +
               $"It may have been moved, renamed, or deleted.";
    }

    /// <summary>
    /// Formats a cancellation message.
    /// </summary>
    public static string FormatCancellationMessage(string operation)
    {
        return $"The {operation} operation was cancelled by the user.";
    }

    /// <summary>
    /// Formats a timeout error.
    /// </summary>
    public static string FormatTimeoutError(string operation, TimeSpan timeout)
    {
        return $"The {operation} operation timed out after {timeout.TotalSeconds:F0} seconds.\n\n" +
               $"The operation is taking longer than expected. This may be due to:\n" +
               $"• Slow network connection\n" +
               $"• Large amount of data being processed\n" +
               $"• Server overload\n\n" +
               $"Try again later or contact support if the problem persists.";
    }

    /// <summary>
    /// Formats an out of memory error.
    /// </summary>
    public static string FormatOutOfMemoryError(string operation)
    {
        return $"The application ran out of memory while trying to {operation}.\n\n" +
               $"Try the following:\n" +
               $"• Close other applications to free up memory\n" +
               $"• Restart the application\n" +
               $"• Process smaller amounts of data at a time\n" +
               $"• Close and reopen the file if working with large files";
    }

    /// <summary>
    /// Formats a configuration error.
    /// </summary>
    public static string FormatConfigurationError(string setting, string problem)
    {
        return $"Configuration error in '{setting}'.\n\n" +
               $"{problem}\n\n" +
               $"Please check your configuration settings and try again.";
    }

    /// <summary>
    /// Formats an import/export error.
    /// </summary>
    public static string FormatImportExportError(string operation, string fileName, Exception ex, int? lineNumber = null)
    {
        var message = new StringBuilder();
        message.AppendLine($"Failed to {operation}.");
        message.AppendLine();
        message.AppendLine($"File: {fileName}");
        
        if (lineNumber.HasValue)
        {
            message.AppendLine($"Line: {lineNumber.Value}");
        }
        
        message.AppendLine();
        message.AppendLine(ex switch
        {
            System.Text.Json.JsonException => "The file contains invalid JSON. Check the file format and try again.",
            FormatException => "The file format is incorrect. Make sure you're using the correct file type.",
            _ => $"Error: {ex.Message}"
        });

        return message.ToString();
    }

    /// <summary>
    /// Formats a password/security error.
    /// </summary>
    public static string FormatSecurityError(string operation, string reason)
    {
        return $"Security error during {operation}.\n\n" +
               $"{reason}\n\n" +
               $"Please verify your credentials and try again.";
    }

    /// <summary>
    /// Formats a suggestion message to accompany an error.
    /// </summary>
    public static string FormatWithSuggestion(string errorMessage, string suggestion)
    {
        return $"{errorMessage}\n\n?? Suggestion: {suggestion}";
    }

    /// <summary>
    /// Formats multiple errors into a single message.
    /// </summary>
    public static string FormatMultipleErrors(string operation, Exception[] exceptions)
    {
        var message = new StringBuilder();
        message.AppendLine($"Multiple errors occurred during {operation}:");
        message.AppendLine();

        for (int i = 0; i < exceptions.Length; i++)
        {
            message.AppendLine($"{i + 1}. {exceptions[i].Message}");
        }

        return message.ToString();
    }

    /// <summary>
    /// Formats a warning message (for non-critical issues).
    /// </summary>
    public static string FormatWarning(string operation, string warning)
    {
        return $"Warning during {operation}:\n\n{warning}\n\n" +
               $"The operation may still succeed, but there might be issues.";
    }

    /// <summary>
    /// Formats a success message with additional info.
    /// </summary>
    public static string FormatSuccess(string operation, string additionalInfo = "")
    {
        var message = $"Successfully completed {operation}.";
        
        if (!string.IsNullOrWhiteSpace(additionalInfo))
        {
            message += $"\n\n{additionalInfo}";
        }

        return message;
    }

    /// <summary>
    /// Formats a partial success message (some items succeeded, some failed).
    /// </summary>
    public static string FormatPartialSuccess(string operation, int succeeded, int failed)
    {
        return $"Partially completed {operation}.\n\n" +
               $"? Succeeded: {succeeded}\n" +
               $"? Failed: {failed}\n\n" +
               $"Check the log file for details about failed items.";
    }
}
