using MyMemories.Services;
using System;
using System.Threading.Tasks;

namespace MyMemories.Utilities;

/// <summary>
/// Extension methods for consistent exception handling across the application.
/// </summary>
public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Executes an action with exception handling.
    /// </summary>
    public static void ExecuteWithExceptionHandling(this Action action, string source = "Application")
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Instance.HandleUnhandledException(ex, source);
        }
    }

    /// <summary>
    /// Executes an async action with exception handling.
    /// </summary>
    public static async Task ExecuteWithExceptionHandlingAsync(this Func<Task> action, string source = "Application", string? userMessage = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (userMessage != null)
            {
                await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(ex, userMessage, source);
            }
            else
            {
                GlobalExceptionHandler.Instance.HandleUnhandledException(ex, source);
            }
        }
    }

    /// <summary>
    /// Executes a function with exception handling and returns a result.
    /// </summary>
    public static T? ExecuteWithExceptionHandling<T>(this Func<T> func, string source = "Application", T? defaultValue = default)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Instance.HandleUnhandledException(ex, source);
            return defaultValue;
        }
    }

    /// <summary>
    /// Executes an async function with exception handling and returns a result.
    /// </summary>
    public static async Task<T?> ExecuteWithExceptionHandlingAsync<T>(this Func<Task<T>> func, string source = "Application", T? defaultValue = default)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Instance.HandleUnhandledException(ex, source);
            return defaultValue;
        }
    }
}
