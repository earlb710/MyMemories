using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for managing application configuration including directories and security settings.
/// </summary>
public class ConfigurationService
{
    private readonly string _configFilePath;
    private readonly string _defaultDataFolder;
    private AppConfiguration _config;
    
    // Logging services
    private ErrorLogService? _errorLogService;
    private AuditLogService? _auditLogService;

    public ConfigurationService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemories"
        );
        Directory.CreateDirectory(appDataFolder);
        
        _configFilePath = Path.Combine(appDataFolder, "config.json");
        _defaultDataFolder = Path.Combine(appDataFolder, "Categories");
        _config = new AppConfiguration();
    }

    public async Task LoadConfigurationAsync()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                _config = JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
                
                // Set default working directory if not configured
                if (string.IsNullOrEmpty(_config.WorkingDirectory))
                {
                    _config.WorkingDirectory = _defaultDataFolder;
                }
                
                // DO NOT set a default log directory - it should remain null/empty unless explicitly set
            }
            catch (Exception ex)
            {
                LogUtilities.LogError(
                    "ConfigurationService.LoadConfigurationAsync",
                    $"Failed to load configuration from '{_configFilePath}'",
                    ex);
                _config = CreateDefaultConfiguration();
            }
        }
        else
        {
            _config = CreateDefaultConfiguration();
            await SaveConfigurationAsync();
        }
        
        // Ensure directories exist
        EnsureDirectoriesExist();
        
        // Initialize logging services
        InitializeLoggingServices();
    }

    private AppConfiguration CreateDefaultConfiguration()
    {
        return new AppConfiguration
        {
            WorkingDirectory = _defaultDataFolder,
            LogDirectory = string.Empty, // No default log directory
            GlobalPasswordHash = string.Empty,
            CategoryPasswords = new Dictionary<string, string>(),
            AuditLoggingEnabled = false,
            CategoryAuditSettings = new Dictionary<string, bool>()
        };
    }

    private void EnsureDirectoriesExist()
    {
        if (!string.IsNullOrEmpty(_config.WorkingDirectory))
        {
            Directory.CreateDirectory(_config.WorkingDirectory);
        }
        
        if (!string.IsNullOrEmpty(_config.LogDirectory))
        {
            Directory.CreateDirectory(_config.LogDirectory);
        }
    }

    /// <summary>
    /// Initializes logging services based on current configuration.
    /// </summary>
    private void InitializeLoggingServices()
    {
        if (!string.IsNullOrEmpty(_config.LogDirectory))
        {
            _errorLogService = new ErrorLogService(_config.LogDirectory);
            _auditLogService = new AuditLogService(_config.LogDirectory);
        }
        else
        {
            _errorLogService = null;
            _auditLogService = null;
        }
    }

    public async Task SaveConfigurationAsync()
    {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_configFilePath, json);
        EnsureDirectoriesExist();
        InitializeLoggingServices();
    }

    public string WorkingDirectory
    {
        get => _config.WorkingDirectory;
        set
        {
            _config.WorkingDirectory = value;
            if (!string.IsNullOrEmpty(value))
            {
                Directory.CreateDirectory(value);
            }
        }
    }

    public string LogDirectory
    {
        get => _config.LogDirectory;
        set
        {
            _config.LogDirectory = value ?? string.Empty;
            if (!string.IsNullOrEmpty(value))
            {
                Directory.CreateDirectory(value);
            }
            InitializeLoggingServices();
        }
    }

    public string GlobalPasswordHash
    {
        get => _config.GlobalPasswordHash;
        set => _config.GlobalPasswordHash = value;
    }

    public Dictionary<string, string> CategoryPasswords => _config.CategoryPasswords;

    /// <summary>
    /// Gets or sets the zip compression level (0-9). Default is 2.
    /// </summary>
    public int ZipCompressionLevel
    {
        get => _config.ZipCompressionLevel;
        set => _config.ZipCompressionLevel = Math.Clamp(value, 0, 9);
    }

    /// <summary>
    /// Gets or sets the global audit logging enabled flag.
    /// </summary>
    public bool AuditLoggingEnabled
    {
        get => _config.AuditLoggingEnabled;
        set => _config.AuditLoggingEnabled = value;
    }

    /// <summary>
    /// Gets the per-category audit logging settings.
    /// </summary>
    public Dictionary<string, bool> CategoryAuditSettings => _config.CategoryAuditSettings;

    /// <summary>
    /// Gets the error log service instance.
    /// </summary>
    public ErrorLogService? ErrorLogService => _errorLogService;

    /// <summary>
    /// Gets the audit log service instance.
    /// </summary>
    public AuditLogService? AuditLogService => _auditLogService;

    public void SetCategoryPassword(string categoryPath, string passwordHash)
    {
        _config.CategoryPasswords[categoryPath] = passwordHash;
    }

    public void RemoveCategoryPassword(string categoryPath)
    {
        _config.CategoryPasswords.Remove(categoryPath);
    }

    public bool HasGlobalPassword() => !string.IsNullOrEmpty(_config.GlobalPasswordHash);

    public bool HasCategoryPassword(string categoryPath) => _config.CategoryPasswords.ContainsKey(categoryPath);

    /// <summary>
    /// Checks if logging is enabled (log directory is set).
    /// </summary>
    public bool IsLoggingEnabled() => !string.IsNullOrEmpty(_config.LogDirectory) && Directory.Exists(_config.LogDirectory);

    /// <summary>
    /// Checks if audit logging is enabled for a specific category.
    /// Uses the category's own IsAuditLoggingEnabled property.
    /// </summary>
    /// <param name="categoryName">The root category name.</param>
    /// <param name="categoryAuditEnabled">The category's IsAuditLoggingEnabled property value.</param>
    /// <returns>True if audit logging is enabled for this category.</returns>
    public bool IsCategoryAuditLoggingEnabled(string categoryName, bool? categoryAuditEnabled = null)
    {
        if (!IsLoggingEnabled())
        {
            return false;
        }

        // If category has explicit audit setting passed in, use it
        if (categoryAuditEnabled.HasValue)
        {
            return categoryAuditEnabled.Value;
        }

        // Check if category has explicit setting in config
        if (_config.CategoryAuditSettings.TryGetValue(categoryName, out var enabled))
        {
            return enabled;
        }

        // Default to global setting
        return AuditLoggingEnabled;
    }

    /// <summary>
    /// Enables or disables audit logging for a specific category.
    /// </summary>
    public void SetCategoryAuditLogging(string categoryName, bool enabled)
    {
        _config.CategoryAuditSettings[categoryName] = enabled;
    }

    /// <summary>
    /// Removes the audit logging setting for a category (falls back to global).
    /// </summary>
    public void RemoveCategoryAuditSetting(string categoryName)
    {
        _config.CategoryAuditSettings.Remove(categoryName);
    }

    /// <summary>
    /// Logs a change to a category. Always logs when log directory is set.
    /// Uses line-numbered format via AuditLogService.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="details">Optional additional details.</param>
    /// <param name="categoryAuditEnabled">Optional: ignored - always logs when log directory is set.</param>
    public async Task LogCategoryChangeAsync(string categoryName, string action, string details = "", bool? categoryAuditEnabled = null)
    {
        if (!IsLoggingEnabled())
            return;

        // Always log when log directory is set
        // Use the AuditLogService for consistent line-numbered format
        if (_auditLogService != null)
        {
            await _auditLogService.LogChangeAsync(categoryName, action, null, details);
        }
        else
        {
            // Fallback to direct file write (shouldn't happen if InitializeLoggingServices was called)
            try
            {
                var logFileName = SanitizeFileName(categoryName) + ".log";
                var logFilePath = Path.Combine(_config.LogDirectory, logFileName);
                
                // Get line number
                int lineNumber = 1;
                if (File.Exists(logFilePath))
                {
                    lineNumber = File.ReadLines(logFilePath).Count() + 1;
                }
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"{lineNumber}|[{timestamp}] [CHANGE] {action}";
                if (!string.IsNullOrEmpty(details))
                {
                    logEntry += $" | {details}";
                }
                logEntry += Environment.NewLine;
                
                await File.AppendAllTextAsync(logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                LogUtilities.LogError(
                    "ConfigurationService.LogCategoryChangeAsync",
                    $"Failed to write category log for '{categoryName}', action: '{action}'",
                    ex);
            }
        }
    }

    /// <summary>
    /// Logs an error with enhanced context information.
    /// </summary>
    public async Task LogErrorAsync(string errorMessage, Exception? exception = null)
    {
        if (!IsLoggingEnabled())
            return;

        // Use the new ErrorLogService
        if (_errorLogService != null)
        {
            await _errorLogService.LogErrorAsync(errorMessage, "Application", exception);
            return;
        }

        // Fallback to old method
        try
        {
            var logFilePath = Path.Combine(_config.LogDirectory, "errors.log");
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] ERROR: {errorMessage}";
            
            if (exception != null)
            {
                logEntry += $"\nException Type: {exception.GetType().FullName}";
                logEntry += $"\nMessage: {exception.Message}";
                logEntry += $"\nSource: {exception.Source}";
                logEntry += $"\nHResult: 0x{exception.HResult:X8}";
                
                if (exception.InnerException != null)
                {
                    logEntry += $"\n\nInner Exception Type: {exception.InnerException.GetType().FullName}";
                    logEntry += $"\nInner Message: {exception.InnerException.Message}";
                }
                
                if (exception.Data != null && exception.Data.Count > 0)
                {
                    logEntry += "\n\nAdditional Data:";
                    foreach (var key in exception.Data.Keys)
                    {
                        logEntry += $"\n  {key}: {exception.Data[key]}";
                    }
                }
                
                logEntry += $"\n\nStack Trace:\n{exception.StackTrace}";
                
                if (exception.InnerException?.StackTrace != null)
                {
                    logEntry += $"\n\nInner Stack Trace:\n{exception.InnerException.StackTrace}";
                }
            }
            
            logEntry += Environment.NewLine + new string('-', 80) + Environment.NewLine;
            
            await File.AppendAllTextAsync(logFilePath, logEntry);
        }
        catch (Exception ex)
        {
            LogUtilities.LogError(
                "ConfigurationService.LogErrorAsync",
                "Failed to write to error log file",
                ex);
        }
    }

    /// <summary>
    /// Logs an invalid password attempt for a category.
    /// </summary>
    public async Task LogInvalidPasswordAttemptAsync(string categoryName, string context = "Category access")
    {
        // Log to error log
        if (_errorLogService != null)
        {
            await _errorLogService.LogWarningAsync($"Invalid password attempt for category '{categoryName}'", context);
        }

        // Log to category audit log if enabled
        if (IsCategoryAuditLoggingEnabled(categoryName) && _auditLogService != null)
        {
            await _auditLogService.LogInvalidPasswordAsync(categoryName, context);
        }
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfiguration
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public string LogDirectory { get; set; } = string.Empty;
    public string GlobalPasswordHash { get; set; } = string.Empty;
    public Dictionary<string, string> CategoryPasswords { get; set; } = new();
    
    /// <summary>
    /// Zip compression level (0-9). Default is 2 (fast).
    /// 0 = No compression, 1-3 = Fast, 4-6 = Balanced, 7-9 = Maximum compression.
    /// </summary>
    public int ZipCompressionLevel { get; set; } = 2;
    
    /// <summary>
    /// Global toggle for audit logging. When true, categories can have individual audit logs.
    /// </summary>
    public bool AuditLoggingEnabled { get; set; } = false;
    
    /// <summary>
    /// Per-category audit logging settings. Key is category name, value is enabled/disabled.
    /// Categories not in this dictionary inherit the global AuditLoggingEnabled setting.
    /// </summary>
    public Dictionary<string, bool> CategoryAuditSettings { get; set; } = new();
}