# Enhanced Logging System

## Overview
The logging system has been significantly enhanced to provide comprehensive error context and debugging information throughout the application.

## Key Enhancements

### 1. Automatic Source Location Tracking
Using C# Caller Information attributes, all log entries now automatically include:
- **Source File Name**: The file where the log was called
- **Line Number**: The exact line number in the source code
- **Caller Method**: The method that invoked the logging

### 2. Enhanced Error Information
Error logs now include:
- **Timestamp**: High-precision timestamp (yyyy-MM-dd HH:mm:ss.fff)
- **Exception Type**: Full type name including namespace
- **Exception Message**: The exception's message
- **Exception Source**: Where the exception originated
- **HResult Code**: The COM error code (0x format)
- **Inner Exceptions**: Full details of nested exceptions
- **Exception Data**: Any custom data attached to the exception
- **Stack Trace**: Complete stack trace for debugging
- **Inner Stack Trace**: Stack trace of inner exceptions

### 3. Context-Aware Logging
New `LogErrorWithContext` method allows passing additional context data:
```csharp
LogUtilities.LogErrorWithContext(
    "MethodName",
    "Error description",
    exception,
    new {
        FilePath = filePath,
        UserId = userId,
        OperationType = "Save"
    },
    statusUpdateAction);
```

The context object is automatically introspected and logged, showing all properties and their values.

### 4. Performance Tracking
New `LogPerformance` method for measuring operation duration:
```csharp
var stopwatch = Stopwatch.StartNew();
// ... perform operation ...
stopwatch.Stop();
LogUtilities.LogPerformance("MethodName", "Operation description", stopwatch.Elapsed);
```

## Updated Log Methods

### LogError
```csharp
LogUtilities.LogError(
    string method,
    string message,
    Exception? ex = null,
    Action<string>? updateStatus = null,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0,
    [CallerMemberName] string callerMethod = "")
```

**Output Format:**
```
[ERROR] [2024-01-15 14:30:45.123] [MainWindow.cs:245] [SaveCategory] (Called from: SaveButton_Click) Failed to save category
Exception Details:
  Type: System.IO.IOException
  Message: The process cannot access the file because it is being used by another process
  Source: System.Private.CoreLib
  HResult: 0x80070020
  Stack Trace:
    at System.IO.FileStream...
```

### LogErrorWithContext
```csharp
LogUtilities.LogErrorWithContext(
    string method,
    string message,
    Exception? ex = null,
    object? contextData = null,
    Action<string>? updateStatus = null,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0,
    [CallerMemberName] string callerMethod = "")
```

**Output Format:**
```
[ERROR] [2024-01-15 14:30:45.123] [FileLauncherService.cs:58] [OpenFileAsync] Failed to open file
Context Data:
  FilePath: C:\Users\Test\document.pdf
  FileSize: 1024000
  IsReadOnly: False
Exception Details:
  Type: System.UnauthorizedAccessException
  Message: Access to the path is denied
  ...
```

### LogDebug
```csharp
LogUtilities.LogDebug(
    string method,
    string message,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0)
```

**Output Format:**
```
[DEBUG] [MainWindow.cs:123] [LoadCategory] Loading category 'My Photos'
```

### LogWarning
```csharp
LogUtilities.LogWarning(
    string method,
    string message,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0)
```

**Output Format:**
```
[WARNING] [2024-01-15 14:30:45.123] [CategoryService.cs:456] [ValidateCategory] Category name contains invalid characters
```

### LogInfo
```csharp
LogUtilities.LogInfo(
    string method,
    string message,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0)
```

**Output Format:**
```
[INFO] [2024-01-15 14:30:45.123] [CatalogService.cs:234] [CreateCatalog] Successfully cataloged 1,234 files
```

### LogPerformance
```csharp
LogUtilities.LogPerformance(
    string method,
    string operation,
    TimeSpan duration,
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int lineNumber = 0)
```

**Output Format:**
```
[PERF] [ZipService.cs:89] [CreateZip] Zip creation completed in 1,234.56ms
```

## Enhanced ConfigurationService Logging

The `ConfigurationService` has been updated to write enhanced error logs to disk:

### Error Log File Format (errors.log)
```
[2024-01-15 14:30:45.123] ERROR: Failed to load configuration
Exception Type: System.Text.Json.JsonException
Message: The JSON value could not be converted to System.String
Source: System.Text.Json
HResult: 0x80131500

Inner Exception Type: System.InvalidOperationException
Inner Message: Cannot convert null value

Additional Data:
  JsonPath: $.WorkingDirectory
  LineNumber: 5

Stack Trace:
  at System.Text.Json.ThrowHelper.ThrowJsonException(String message)
  at System.Text.Json.Serialization.Converters.StringConverter.Read(...)
  ...

Inner Stack Trace:
  at System.Text.Json.Utf8JsonReader.GetString()
  ...

--------------------------------------------------------------------------------
```

## Usage Examples

### Basic Error Logging
```csharp
try
{
    // ... code that might fail ...
}
catch (Exception ex)
{
    LogUtilities.LogError(
        "MyClass.MyMethod",
        "Operation failed",
        ex);
}
```

### Error Logging with Status Update
```csharp
try
{
    // ... code that might fail ...
}
catch (Exception ex)
{
    LogUtilities.LogError(
        "MyClass.MyMethod",
        "Operation failed",
        ex,
        updateStatus: msg => StatusText.Text = msg);
}
```

### Context-Aware Error Logging
```csharp
try
{
    await SaveFileAsync(filePath, content);
}
catch (Exception ex)
{
    LogUtilities.LogErrorWithContext(
        "MyClass.SaveFile",
        "Failed to save file",
        ex,
        new {
            FilePath = filePath,
            ContentLength = content.Length,
            IsBackup = false,
            Timestamp = DateTime.Now
        });
}
```

### Performance Tracking
```csharp
var sw = Stopwatch.StartNew();
try
{
    // ... perform operation ...
}
finally
{
    sw.Stop();
    LogUtilities.LogPerformance(
        "MyClass.ExpensiveOperation",
        "Database query",
        sw.Elapsed);
}
```

## Benefits

1. **Faster Debugging**: Exact file and line number where error occurred
2. **Better Context**: Understanding what was happening when error occurred
3. **Complete Information**: Full exception chain with stack traces
4. **Production Ready**: Logs work in both DEBUG and RELEASE builds
5. **Non-Intrusive**: Caller information is automatically captured
6. **Flexible**: Support for status updates and custom context data
7. **Performance Insights**: Track operation durations

## Files Modified

1. **MyMemories\Utilities\LogUtilities.cs**
   - Added CallerInformation attributes to all methods
   - Added timestamps to all log entries
   - Enhanced exception detail logging
   - Added `LogErrorWithContext` method
   - Added `LogPerformance` method
   - Improved formatting with file names and line numbers

2. **MyMemories\Services\ConfigurationService.cs**
   - Enhanced error logging in `LoadConfigurationAsync`
   - Enhanced error logging in `LogCategoryChangeAsync`
   - Enhanced error logging in `LogErrorAsync` with full exception details
   - Added using statement for LogUtilities

3. **MyMemories\Services\FileLauncherService.cs**
   - Replaced Debug.WriteLine with LogUtilities calls
   - Added context data to error logs
   - Enhanced all exception handling with contextual information

## Configuration

Logging can be controlled via the `IsDevelopmentMode` property:
- **DEBUG builds**: Automatically enabled
- **RELEASE builds**: Automatically disabled (but can be enabled programmatically)

To enable logging in RELEASE builds:
```csharp
LogUtilities.IsDevelopmentMode = true;
```

## Best Practices

1. **Always include context**: Use `LogErrorWithContext` when you have relevant data
2. **Use descriptive method names**: Format as "ClassName.MethodName"
3. **Include operation details**: Describe what was being attempted
4. **Don't log sensitive data**: Be careful with passwords, tokens, etc.
5. **Use appropriate log levels**: Debug for tracing, Info for events, Warning for issues, Error for exceptions
6. **Measure performance**: Use LogPerformance for slow operations

## Future Enhancements

Potential future improvements:
- Structured logging (JSON format)
- Log file rotation
- Remote logging support
- Log aggregation
- Configurable log levels per namespace
- Integration with Application Insights or similar services
