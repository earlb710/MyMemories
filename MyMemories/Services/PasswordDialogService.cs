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

    public PasswordDialogService(XamlRoot xamlRoot, CategoryService categoryService)
    {
        _xamlRoot = xamlRoot;
        _categoryService = categoryService;
    }

    /// <summary>
    /// Gets the password for a category (either from cache or prompts user).
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

            // Global password not cached, this shouldn't happen but handle it
            return null;
        }
        else if (category.PasswordProtection == PasswordProtectionType.OwnPassword)
        {
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
                        return passwordBox.Password;
                    }
                    else
                    {
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
