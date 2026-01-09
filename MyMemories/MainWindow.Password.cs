using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Password-related helper methods for MainWindow.
/// </summary>
public sealed partial class MainWindow
{
    private PasswordDialogService? _passwordDialogService;

    /// <summary>
    /// Gets the password for a category (either from cache or prompts user).
    /// </summary>
    private async Task<string?> GetCategoryPasswordAsync(CategoryItem category)
    {
        // Initialize password dialog service if needed
        _passwordDialogService ??= new PasswordDialogService(Content.XamlRoot, _categoryService!, _configService);

        return await _passwordDialogService.GetCategoryPasswordAsync(category);
    }

    /// <summary>
    /// Shows a dialog to change the password protection settings for a category.
    /// </summary>
    private async Task ShowChangePasswordDialogAsync(CategoryItem category, TreeViewNode categoryNode)
    {
        // Build the dialog content
        var stackPanel = new StackPanel { Spacing = 16 };

        // Current status
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Current Protection: {GetPasswordProtectionText(category.PasswordProtection)}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Password protection type selection
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select password protection type:",
            Margin = new Thickness(0, 8, 0, 0)
        });

        var protectionComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = (int)category.PasswordProtection
        };
        protectionComboBox.Items.Add("None - No password protection");
        protectionComboBox.Items.Add("Global Password - Use the application's global password");
        protectionComboBox.Items.Add("Own Password - Set a unique password for this category");
        stackPanel.Children.Add(protectionComboBox);

        // Own password section (initially hidden)
        var ownPasswordPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = category.PasswordProtection == PasswordProtectionType.OwnPassword 
                ? Visibility.Visible 
                : Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0)
        };

        ownPasswordPanel.Children.Add(new TextBlock
        {
            Text = "Enter new password for this category:",
            FontSize = 12
        });

        var newPasswordBox = new PasswordBox
        {
            PlaceholderText = "New password"
        };
        ownPasswordPanel.Children.Add(newPasswordBox);

        var confirmPasswordBox = new PasswordBox
        {
            PlaceholderText = "Confirm password"
        };
        ownPasswordPanel.Children.Add(confirmPasswordBox);

        stackPanel.Children.Add(ownPasswordPanel);

        // Show/hide own password panel based on selection
        protectionComboBox.SelectionChanged += (s, e) =>
        {
            ownPasswordPanel.Visibility = protectionComboBox.SelectedIndex == 2 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        };

        // Global password warning
        var globalWarning = new InfoBar
        {
            Title = "Global Password Required",
            Message = "Make sure you have set a global password in Security Setup before using this option.",
            Severity = InfoBarSeverity.Warning,
            IsOpen = true,
            IsClosable = false,
            Visibility = category.PasswordProtection == PasswordProtectionType.GlobalPassword 
                ? Visibility.Visible 
                : Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0)
        };

        protectionComboBox.SelectionChanged += (s, e) =>
        {
            globalWarning.Visibility = protectionComboBox.SelectedIndex == 1 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        };

        stackPanel.Children.Add(globalWarning);

        var dialog = new ContentDialog
        {
            Title = $"Change Password - {category.Name}",
            Content = stackPanel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var newProtection = (PasswordProtectionType)protectionComboBox.SelectedIndex;

            // Validate own password if selected
            if (newProtection == PasswordProtectionType.OwnPassword)
            {
                if (string.IsNullOrEmpty(newPasswordBox.Password))
                {
                    await ShowErrorDialogAsync("Password Required", "Please enter a password for this category.");
                    return;
                }

                if (newPasswordBox.Password != confirmPasswordBox.Password)
                {
                    await ShowErrorDialogAsync("Password Mismatch", "The passwords do not match.");
                    return;
                }

                // Hash and store the password
                category.OwnPasswordHash = PasswordUtilities.HashPassword(newPasswordBox.Password);
                
                // Cache the password for encryption
                _categoryService?.CacheCategoryPassword(category.Name, newPasswordBox.Password);
            }
            else if (newProtection == PasswordProtectionType.GlobalPassword)
            {
                // Verify global password is set
                if (_configService == null || !_configService.HasGlobalPassword())
                {
                    await ShowErrorDialogAsync("Global Password Not Set", 
                        "Please set a global password in Config > Security Setup before using this option.");
                    return;
                }

                category.OwnPasswordHash = null;
            }
            else
            {
                // No protection
                category.OwnPasswordHash = null;
            }

            category.PasswordProtection = newProtection;
            category.ModifiedDate = DateTime.Now;

            // Save the category
            var rootNode = GetRootCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Log the change
            if (_configService?.IsLoggingEnabled() ?? false)
            {
                await _configService.LogCategoryChangeAsync(category.Name, 
                    $"Password protection changed to: {GetPasswordProtectionText(newProtection)}");
            }

            StatusText.Text = $"Password protection updated for '{category.Name}'";

            // Refresh the node visual
            RefreshNodeVisual(categoryNode);
        }
    }

    /// <summary>
    /// Shows a dialog to change the password for a zip file.
    /// </summary>
    private async Task ShowChangeZipPasswordDialogAsync(LinkItem zipLink, TreeViewNode zipNode)
    {
        // Build the dialog content
        var stackPanel = new StackPanel { Spacing = 16 };

        // Current status
        stackPanel.Children.Add(new TextBlock
        {
            Text = zipLink.IsZipPasswordProtected 
                ? "?? This zip file is currently password-protected" 
                : "?? This zip file is not password-protected",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Action selection
        var actionComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0)
        };

        if (zipLink.IsZipPasswordProtected)
        {
            actionComboBox.Items.Add("Remove password protection");
            actionComboBox.Items.Add("Change password");
        }
        else
        {
            actionComboBox.Items.Add("Add password protection");
        }

        actionComboBox.SelectedIndex = 0;
        stackPanel.Children.Add(actionComboBox);

        // Password input panel
        var passwordPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = zipLink.IsZipPasswordProtected ? Visibility.Collapsed : Visibility.Visible,
            Margin = new Thickness(0, 8, 0, 0)
        };

        passwordPanel.Children.Add(new TextBlock
        {
            Text = "Enter new password:",
            FontSize = 12
        });

        var newPasswordBox = new PasswordBox
        {
            PlaceholderText = "New password"
        };
        passwordPanel.Children.Add(newPasswordBox);

        var confirmPasswordBox = new PasswordBox
        {
            PlaceholderText = "Confirm password"
        };
        passwordPanel.Children.Add(confirmPasswordBox);

        stackPanel.Children.Add(passwordPanel);

        // Update visibility based on action selection
        actionComboBox.SelectionChanged += (s, e) =>
        {
            if (zipLink.IsZipPasswordProtected)
            {
                // "Remove password" = 0, "Change password" = 1
                passwordPanel.Visibility = actionComboBox.SelectedIndex == 1 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            else
            {
                // Always show password input for adding protection
                passwordPanel.Visibility = Visibility.Visible;
            }
        };

        // Backup option
        var keepBackupCheckBox = new CheckBox
        {
            Content = "Keep backup of original zip file",
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stackPanel.Children.Add(keepBackupCheckBox);

        // Warning
        stackPanel.Children.Add(new InfoBar
        {
            Title = "Warning",
            Message = "This operation will recreate the zip file. Make sure you have enough disk space.",
            Severity = InfoBarSeverity.Warning,
            IsOpen = true,
            IsClosable = false,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var dialog = new ContentDialog
        {
            Title = $"Change Zip Password - {zipLink.Title}",
            Content = stackPanel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            bool usePassword;
            string? password = null;

            if (zipLink.IsZipPasswordProtected)
            {
                // Action 0 = Remove, Action 1 = Change
                usePassword = actionComboBox.SelectedIndex == 1;
            }
            else
            {
                // Adding password
                usePassword = true;
            }

            if (usePassword)
            {
                if (string.IsNullOrEmpty(newPasswordBox.Password))
                {
                    await ShowErrorDialogAsync("Password Required", "Please enter a password.");
                    return;
                }

                if (newPasswordBox.Password != confirmPasswordBox.Password)
                {
                    await ShowErrorDialogAsync("Password Mismatch", "The passwords do not match.");
                    return;
                }

                password = newPasswordBox.Password;
            }

            bool keepBackup = keepBackupCheckBox.IsChecked ?? true;

            // Perform the password change operation
            await ChangeZipPasswordAsync(zipLink, zipNode, usePassword, password, keepBackup);
        }
    }

    /// <summary>
    /// Gets a human-readable text for password protection type.
    /// </summary>
    private static string GetPasswordProtectionText(PasswordProtectionType type)
    {
        return type switch
        {
            PasswordProtectionType.None => "No Password",
            PasswordProtectionType.GlobalPassword => "Global Password",
            PasswordProtectionType.OwnPassword => "Own Password",
            _ => "Unknown"
        };
    }
}
