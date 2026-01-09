using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async Task ShowSecuritySetupDialogAsync()
    {
        var tabView = new TabView
        {
            MinHeight = 400
        };

        // Global Password Tab
        var globalTab = new TabViewItem
        {
            Header = "Global Password",
            IconSource = new SymbolIconSource { Symbol = Symbol.ProtectedDocument }
        };

        var globalPanel = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        // Current status
        var hasGlobalPassword = _configService?.HasGlobalPassword() ?? false;
        globalPanel.Children.Add(new TextBlock
        {
            Text = hasGlobalPassword ? "? Global password is set" : "?? No global password set",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                hasGlobalPassword ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Orange
            ),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        globalPanel.Children.Add(new TextBlock
        {
            Text = "Set a global password to protect the entire application:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var newGlobalPasswordBox = new PasswordBox
        {
            PlaceholderText = "Enter new password",
            Margin = new Thickness(0, 8, 0, 0)
        };
        newGlobalPasswordBox.PasswordChanged += (s, e) => { /* Handle password strength display if needed */ };

        var confirmGlobalPasswordBox = new PasswordBox
        {
            PlaceholderText = "Confirm new password",
            Margin = new Thickness(0, 0, 0, 8)
        };

        globalPanel.Children.Add(newGlobalPasswordBox);
        globalPanel.Children.Add(confirmGlobalPasswordBox);

        if (hasGlobalPassword)
        {
            var removeGlobalButton = new Button
            {
                Content = "Remove Global Password",
                Margin = new Thickness(0, 8, 0, 0)
            };

            removeGlobalButton.Click += async (s, args) =>
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Remove Global Password",
                    Content = "Are you sure you want to remove the global password?",
                    PrimaryButtonText = "Remove",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };

                if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (_configService != null)
                    {
                        _configService.GlobalPasswordHash = string.Empty;
                        await _configService.SaveConfigurationAsync();
                        await _configService.LogErrorAsync("Global password removed");
                        StatusText.Text = "Global password removed";
                    }
                }
            };

            globalPanel.Children.Add(removeGlobalButton);
        }

        globalTab.Content = new ScrollViewer { Content = globalPanel };
        tabView.TabItems.Add(globalTab);

        // Category Passwords Tab
        var categoryTab = new TabViewItem
        {
            Header = "Category Passwords",
            IconSource = new SymbolIconSource { Symbol = Symbol.Folder }
        };

        var categoryPanel = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        categoryPanel.Children.Add(new TextBlock
        {
            Text = "Set passwords for individual root categories:",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Get all root categories
        var rootCategories = LinksTreeView.RootNodes
            .Where(n => n.Content is CategoryItem)
            .Select(n => new
            {
                Node = n,
                Category = (CategoryItem)n.Content,
                Path = _treeViewService!.GetCategoryPath(n)
            })
            .ToList();

        if (rootCategories.Any())
        {
            foreach (var cat in rootCategories)
            {
                var catCard = new Border
                {
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var catStackPanel = new StackPanel { Spacing = 8 };

                catStackPanel.Children.Add(new TextBlock
                {
                    Text = $"{cat.Category.Icon} {cat.Category.Name}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                var hasPassword = _configService?.HasCategoryPassword(cat.Path) ?? false;
                catStackPanel.Children.Add(new TextBlock
                {
                    Text = hasPassword ? "? Password protected" : "?? Not protected",
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        hasPassword ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Orange
                    )
                });

                var passwordBox = new PasswordBox
                {
                    PlaceholderText = "Enter password for this category",
                    Tag = cat.Path,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                catStackPanel.Children.Add(passwordBox);

                if (hasPassword)
                {
                    var removeButton = new Button
                    {
                        Content = "Remove Password",
                        Tag = cat.Path,
                        Margin = new Thickness(0, 4, 0, 0)
                    };

                    removeButton.Click += async (s, args) =>
                    {
                        if (_configService != null)
                        {
                            var categoryPath = (string)((Button)s).Tag;
                            _configService.RemoveCategoryPassword(categoryPath);
                            await _configService.SaveConfigurationAsync();
                            await _configService.LogCategoryChangeAsync(cat.Category.Name, "Password removed");
                            StatusText.Text = $"Password removed for category: {cat.Category.Name}";
                        }
                    };

                    catStackPanel.Children.Add(removeButton);
                }

                catCard.Child = catStackPanel;
                categoryPanel.Children.Add(catCard);
            }
        }
        else
        {
            categoryPanel.Children.Add(new TextBlock
            {
                Text = "No root categories available. Create a category first.",
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
        }

        categoryTab.Content = new ScrollViewer { Content = categoryPanel };
        tabView.TabItems.Add(categoryTab);

        var dialog = new ContentDialog
        {
            Title = "Security Setup",
            Content = tabView,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && _configService != null)
        {
            bool globalPasswordChanged = false;
            string? newGlobalPassword = null;
            
            // Save global password
            if (!string.IsNullOrEmpty(newGlobalPasswordBox.Password))
            {
                if (newGlobalPasswordBox.Password == confirmGlobalPasswordBox.Password)
                {
                    newGlobalPassword = newGlobalPasswordBox.Password; // Store the plain text password
                    
                    // Check if global password is being changed (not just set for the first time)
                    bool isPasswordChange = hasGlobalPassword;
                    
                    // Count categories using global password
                    int globalPasswordCategoryCount = 0;
                    if (isPasswordChange)
                    {
                        foreach (var rootNode in LinksTreeView.RootNodes)
                        {
                            if (rootNode.Content is CategoryItem cat && 
                                cat.PasswordProtection == PasswordProtectionType.GlobalPassword)
                            {
                                globalPasswordCategoryCount++;
                            }
                        }
                    }
                    
                    // If changing password and categories exist, warn and confirm
                    if (isPasswordChange && globalPasswordCategoryCount > 0)
                    {
                        var confirmReEncryptDialog = new ContentDialog
                        {
                            Title = "Re-encrypt Categories?",
                            Content = $"Changing the global password will re-encrypt {globalPasswordCategoryCount} " +
                                     $"categor{(globalPasswordCategoryCount == 1 ? "y" : "ies")} that use the global password.\n\n" +
                                     "All categories will be saved with the new password encryption.\n\n" +
                                     "Do you want to continue?",
                            PrimaryButtonText = "Yes, Re-encrypt",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Close,
                            XamlRoot = Content.XamlRoot
                        };
                        
                        if (await confirmReEncryptDialog.ShowAsync() != ContentDialogResult.Primary)
                        {
                            // User cancelled the password change
                            return;
                        }
                    }
                    
                    _configService.GlobalPasswordHash = PasswordUtilities.HashPassword(newGlobalPassword);
                    
                    // Cache the global password in CategoryService for encryption
                    _categoryService?.CacheGlobalPassword(newGlobalPassword);
                    
                    await _configService.SaveConfigurationAsync();
                    await _configService.LogErrorAsync("Global password set/changed");
                    globalPasswordChanged = true;
                    
                    // Re-save all categories that use global password
                    if (isPasswordChange && globalPasswordCategoryCount > 0)
                    {
                        StatusText.Text = "Re-encrypting categories with new password...";
                        
                        int successCount = 0;
                        int errorCount = 0;
                        
                        foreach (var rootNode in LinksTreeView.RootNodes)
                        {
                            if (rootNode.Content is CategoryItem cat && 
                                cat.PasswordProtection == PasswordProtectionType.GlobalPassword)
                            {
                                try
                                {
                                    await _categoryService!.SaveCategoryAsync(rootNode);
                                    successCount++;
                                }
                                catch (System.Exception ex)
                                {
                                    errorCount++;
                                    System.Diagnostics.Debug.WriteLine($"Error re-encrypting category {cat.Name}: {ex.Message}");
                                    
                                    if (_configService.IsLoggingEnabled())
                                    {
                                        await _configService.LogErrorAsync($"Failed to re-encrypt category {cat.Name}", ex);
                                    }
                                }
                            }
                        }
                        
                        if (errorCount > 0)
                        {
                            var errorDialog = new ContentDialog
                            {
                                Title = "Re-encryption Completed with Errors",
                                Content = $"Re-encrypted {successCount} {(successCount == 1 ? "category" : "categories")} successfully.\n\n" +
                                         $"{errorCount} {(errorCount == 1 ? "category" : "categories")} failed to re-encrypt. " +
                                         "Check the error log for details.",
                                CloseButtonText = "OK",
                                XamlRoot = Content.XamlRoot
                            };
                            await errorDialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    await ShowErrorDialogAsync("Password Mismatch", "The passwords do not match.");
                    return;
                }
            }

            // Save category passwords
            foreach (var child in categoryPanel.Children)
            {
                if (child is Border border && border.Child is StackPanel sp)
                {
                    var passwordBox = sp.Children.OfType<PasswordBox>().FirstOrDefault();
                    if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                    {
                        var categoryPath = (string)passwordBox.Tag;
                        var categoryName = rootCategories.First(c => c.Path == categoryPath).Category.Name;
                        
                        var plainPassword = passwordBox.Password;
                        
                        // Cache the category password in CategoryService for encryption
                        _categoryService?.CacheCategoryPassword(categoryPath, plainPassword);
                        
                        _configService.SetCategoryPassword(categoryPath, PasswordUtilities.HashPassword(plainPassword));
                        await _configService.LogCategoryChangeAsync(categoryName, "Password set/changed");
                    }
                }
            }

            await _configService.SaveConfigurationAsync();
            
            // Reload the _linkDialog if global password was changed
            // This ensures CategoryDialogBuilder has the updated ConfigurationService
            if (globalPasswordChanged && _linkDialog != null)
            {
                _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
            }
            
            StatusText.Text = "Security settings saved successfully";
        }
    }
}
