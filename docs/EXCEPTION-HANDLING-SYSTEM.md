# Global Exception Handling System

**Last Updated:** 2026-01-14

## Overview

MyMemories implements a comprehensive global exception handling system that catches and handles all unhandled exceptions across:
- UI thread exceptions
- Background thread exceptions
- Async task exceptions (unobserved)
- AppDomain exceptions

## Architecture

### Components

1. **GlobalExceptionHandler** (`Services/GlobalExceptionHandler.cs`)
   - Singleton service that handles all exceptions
   - Provides user-friendly error dialogs
   - Logs exceptions to error.log
   - Determines if exceptions are critical

2. **App.xaml.cs Exception Hooks**
   - `UnhandledException` - Catches UI thread exceptions
   - `TaskScheduler.UnobservedTaskException` - Catches async exceptions
   - `AppDomain.UnhandledException` - Catches non-UI thread exceptions

3. **ExceptionHandlingExtensions** (`Utilities/ExceptionHandlingExtensions.cs`)
   - Extension methods for consistent exception handling
   - Simplifies try-catch blocks across the codebase

## Usage

### Automatic Handling

All unhandled exceptions are automatically caught and handled. No code changes needed for basic protection.

### Manual Handling (Recommended for Known Exceptions)

For operations that might fail expectedly (file operations, network calls, etc.), use the global handler explicitly:

```csharp
try
{
    // Risky operation
    await LoadFileAsync(path);
}
catch (Exception ex)
{
    await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(
        ex,
        "Failed to load the selected file. The file may be corrupted or inaccessible.",
        "FileLoader");
}
```

### Using Extension Methods

Simplify exception handling with extension methods:

```csharp
// Sync action
await ExecuteWithExceptionHandlingAsync(
    async () => await SomeRiskyOperationAsync(),
    "RiskyOperation",
    "Failed to complete the operation. Please try again.");

// Async function with return value
var result = await ExecuteWithExceptionHandlingAsync(
    async () => await GetDataAsync(),
    "DataLoader",
    defaultValue: null);
```

## Exception Categories

### User-Friendly Messages

The system provides context-aware messages based on exception type:

| Exception Type | User Message |
|---------------|--------------|
| `UnauthorizedAccessException` | "Access to a file or folder was denied. Please check permissions." |
| `IOException` | "A file operation failed. The file may be in use or the disk may be full." |
| `OutOfMemoryException` | "The application ran out of memory. Try closing other applications." |
| `FileNotFoundException` | "A required file was not found." |
| `ArgumentException` | "Invalid input was provided to the operation." |
| `TaskCanceledException` | "The operation was cancelled." |
| Other | Generic message with technical details |

### Critical Exceptions

These exceptions cause immediate application termination:
- `OutOfMemoryException`
- `StackOverflowException`
- `AccessViolationException`
- `AppDomainUnloadedException`

## Error Dialog

The error dialog shows:
1. **User-friendly message** - What went wrong in plain English
2. **Technical details expander** - For advanced users/debugging
   - Exception type
   - Exception message
   - Source location

## Logging

All exceptions are logged to `error.log` via `ErrorLogService`:
- Full exception details
- Stack trace
- Inner exceptions
- Source location
- Timestamp

## Best Practices

### ? DO:

1. **Use the global handler for known exceptions:**
   ```csharp
   catch (IOException ex)
   {
       await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(
           ex, "Could not save the file.", "SaveOperation");
   }
   ```

2. **Provide context in source parameter:**
   ```csharp
   "CategoryService.LoadAsync"  // Good
   "Application"                // Too generic
   ```

3. **Give user-actionable messages:**
   ```csharp
   "Failed to load categories. Check that the categories folder exists."  // Good
   "An error occurred."                                                    // Too vague
   ```

### ? DON'T:

1. **Don't use empty catch blocks:**
   ```csharp
   catch { }  // BAD - swallows errors silently
   ```

2. **Don't catch generic Exception without re-throwing or handling:**
   ```csharp
   catch (Exception ex)
   {
       // No logging or handling - BAD
   }
   ```

3. **Don't show technical details to users in custom dialogs:**
   ```csharp
   // BAD - confusing to users
   ShowMessage($"Exception: {ex.GetType().Name}\nStack: {ex.StackTrace}");
   
   // GOOD - use global handler
   await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(ex, 
       "Operation failed.", "Source");
   ```

## Testing

### Verify Exception Handling

1. **Test UI thread exceptions:**
   - Add a button that throws an exception
   - Verify error dialog appears
   - Verify error is logged

2. **Test async exceptions:**
   - Create a Task that throws without await
   - Verify UnobservedTaskException is caught
   - Verify error is logged

3. **Test critical exceptions:**
   - Simulate OutOfMemoryException (difficult)
   - Verify app terminates gracefully

### Example Test Cases

```csharp
// Test user-friendly message for file not found
try
{
    File.ReadAllText("nonexistent.txt");
}
catch (Exception ex)
{
    await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(ex, 
        "Test message", "Test");
    // Should show: "A required file was not found."
}

// Test unobserved task exception
_ = Task.Run(() => throw new InvalidOperationException("Test"));
GC.Collect();
GC.WaitForPendingFinalizers();
// Should log to error.log
```

## Monitoring

Check `error.log` regularly for:
- Frequent exception types
- Common failure points
- Patterns in errors

Use this data to:
- Add retry logic
- Improve validation
- Enhance error messages
- Fix bugs

## Future Enhancements

Potential improvements:
- [ ] Telemetry integration (Application Insights, etc.)
- [ ] Exception filtering by severity
- [ ] Automatic retry for transient failures
- [ ] Error report submission to developers
- [ ] Exception statistics dashboard

## Related Files

- `MyMemories\Services\GlobalExceptionHandler.cs` - Main handler
- `MyMemories\App.xaml.cs` - Exception hooks
- `MyMemories\Utilities\ExceptionHandlingExtensions.cs` - Extensions
- `MyMemories\Services\ErrorLogService.cs` - Logging backend
- `docs\TODO-IMPROVEMENTS.md` - Improvement backlog

---

**Questions or Issues?** Check `error.log` for details or refer to the TODO improvements document.
