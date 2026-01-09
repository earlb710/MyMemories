using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for handling password-related dialogs.
/// </summary>
public class PasswordDialogService
{
    private readonly XamlRoot _xamlRoot;
    private readonly CategoryService _categoryService;
    private ConfigurationService? _configService;

    public PasswordDialogService(XamlRoot xamlRoot, CategoryService categoryService, ConfigurationService? configService = null)
    {
        _xamlRoot = xamlRoot;
        _categoryService = categoryService;
        _configService = configService;
    }

    /// <summary>
    /// Sets the configuration service for logging.
    /// </summary>
    public void SetConfigurationService(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Gets the password for a category (either from cache or prompts user).
    /// Passwords are cached for the session once verified.
    /// </summary>
    public async Task<string?> GetCategoryPasswordAsync(CategoryItem category)
    {
        if (category.PasswordProtection == PasswordProtectionType.GlobalPassword)
        {
            // Use global password from category service
            var globalPassword = _categoryService.GetCachedGlobalPassword();
            if (!string.IsNullOrEmpty(globalPassword))
            {
                return globalPassword;
            }

            // Global password not cached - prompt for it
            return await PromptForGlobalPasswordAsync();
        }
        else if (category.PasswordProtection == PasswordProtectionType.OwnPassword)
        {
            // First check if password is already cached for this category
            var categoryPath = category.Name; // For root categories, the name is the path
            var cachedPassword = _categoryService.GetCachedCategoryPassword(categoryPath);
            if (!string.IsNullOrEmpty(cachedPassword))
            {
                return cachedPassword;
            }

            // Prompt user for category's own password
            var passwordDialog = new ContentDialog
            {
                Title = "Category Password Required",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Enter the password for category '{category.Name}':",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new PasswordBox
                        {
                            Name = "CategoryPasswordInput",
                            PlaceholderText = "Enter category password"
                        }
                    }
                },
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            var result = await passwordDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var passwordBox = (passwordDialog.Content as StackPanel)
                    ?.Children.OfType<PasswordBox>()
                    .FirstOrDefault();

                if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    // Verify password
                    var enteredHash = PasswordUtilities.HashPassword(passwordBox.Password);
                    if (enteredHash == category.OwnPasswordHash)
                    {
                        // Cache the password for the session
                        _categoryService.CacheCategoryPassword(categoryPath, passwordBox.Password);
                        return passwordBox.Password;
                    }
                    else
                    {
                        // Log invalid password attempt
                        await LogInvalidPasswordAttemptAsync(category.Name, "Category access - own password");
                        
                        var errorDialog = new ContentDialog
                        {
                            Title = "Incorrect Password",
                            Content = "The password you entered is incorrect.",
                            CloseButtonText = "OK",
                            XamlRoot = _xamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return null;
                    }
                }
            }
            
            return null; // User cancelled
        }

        return null;
    }

    /// <summary>
    /// Prompts for global password and caches it if correct.
    /// </summary>
    private async Task<string?> PromptForGlobalPasswordAsync()
    {
        if (_configService == null || !_configService.HasGlobalPassword())
        {
            return null;
        }

        var passwordDialog = new ContentDialog
        {
            Title = "Global Password Required",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter the global password to continue:",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    new PasswordBox
                    {
                        Name = "GlobalPasswordInput",
                        PlaceholderText = "Enter global password"
                    }
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await passwordDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var passwordBox = (passwordDialog.Content as StackPanel)
                ?.Children.OfType<PasswordBox>()
                .FirstOrDefault();

            if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
            {
                // Verify password
                var enteredHash = PasswordUtilities.HashPassword(passwordBox.Password);
                if (enteredHash == _configService.GlobalPasswordHash)
                {
                    // Cache the global password for the session
                    _categoryService.CacheGlobalPassword(passwordBox.Password);
                    return passwordBox.Password;
                }
                else
                {
                    // Log invalid global password attempt to error.log
                    if (_configService.IsLoggingEnabled() && _configService.ErrorLogService != null)
                    {
                        await _configService.ErrorLogService.LogWarningAsync(
                            "Invalid global password attempt",
                            "PasswordDialogService.PromptForGlobalPasswordAsync");
                    }
                    
                    var errorDialog = new ContentDialog
                    {
                        Title = "Incorrect Password",
                        Content = "The global password you entered is incorrect.",
                        CloseButtonText = "OK",
                        XamlRoot = _xamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return null;
                }
            }
        }

        return null; // User cancelled
    }

    /// <summary>
    /// Logs an invalid password attempt if logging is enabled.
    /// </summary>
    private async Task LogInvalidPasswordAttemptAsync(string categoryName, string context)
    {
        if (_configService != null)
        {
            await _configService.LogInvalidPasswordAttemptAsync(categoryName, context);
        }
    }

    /// <summary>
    /// Gets password status text for display.
    /// </summary>
    public static string GetPasswordStatusText(PasswordProtectionType type)
    {
        return type switch
        {
            PasswordProtectionType.None => "? No Password",
            PasswordProtectionType.GlobalPassword => "? Global Password",
            PasswordProtectionType.OwnPassword => "?? Own Password",
            _ => "Unknown"
        };
    }
}
