using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace MyMemories;

public sealed partial class MainWindow
{
    // COM interface for IFileOpenDialog
    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    private enum SIGDN : uint
    {
        FILESYSPATH = 0x80058000
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    private const uint FOS_PICKFOLDERS = 0x00000020;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItem ppv);

    private async void MenuConfig_DirectorySetup_Click(object sender, RoutedEventArgs e)
    {
        await ShowDirectorySetupDialogAsync();
    }

    private async void MenuConfig_SecuritySetup_Click(object sender, RoutedEventArgs e)
    {
        await ShowSecuritySetupDialogAsync();
    }

    private async Task ShowDirectorySetupDialogAsync()
    {
        // Create UI for directory setup
        var stackPanel = new StackPanel { Spacing = 16 };

        // Info banner
        var infoBanner = new InfoBar
        {
            Title = "Default Directories",
            Message = "These directories are set to the default locations where JSON category files and logs are stored. You can type or paste paths directly.",
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            Margin = new Thickness(0, 0, 0, 16)
        };
        stackPanel.Children.Add(infoBanner);

        // Working Directory
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Working Directory (Category JSON Files):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var workingDirTextBox = new TextBox
        {
            Text = _configService?.WorkingDirectory ?? string.Empty,
            PlaceholderText = "Type or select working directory...",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var workingDirButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var browseWorkingButton = new Button
        {
            Content = "Browse..."
        };

        var openWorkingInExplorerButton = new Button
        {
            Content = "Open in Explorer"
        };

        browseWorkingButton.Click += (s, args) =>
        {
            var selectedPath = BrowseForFolder(workingDirTextBox.Text, "Select Working Directory");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                workingDirTextBox.Text = selectedPath;
            }
        };

        openWorkingInExplorerButton.Click += async (s, args) =>
        {
            var path = workingDirTextBox.Text;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    await Launcher.LaunchFolderPathAsync(path);
                }
                catch
                {
                    // Fallback to Process.Start
                    try
                    {
                        Process.Start("explorer.exe", path);
                    }
                    catch
                    {
                        // Ignore if both fail
                    }
                }
            }
            else
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Directory Not Found",
                    Content = "The specified directory does not exist.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        };

        workingDirButtonPanel.Children.Add(browseWorkingButton);
        workingDirButtonPanel.Children.Add(openWorkingInExplorerButton);

        var resetWorkingButton = new Button
        {
            Content = "Reset to Default",
            Margin = new Thickness(0, 0, 0, 16)
        };

        resetWorkingButton.Click += (s, args) =>
        {
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMemories",
                "Categories"
            );
            workingDirTextBox.Text = defaultPath;
        };

        stackPanel.Children.Add(workingDirTextBox);
        stackPanel.Children.Add(workingDirButtonPanel);
        stackPanel.Children.Add(resetWorkingButton);

        // Log Directory
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Log Directory (Optional):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var logDirTextBox = new TextBox
        {
            Text = _configService?.LogDirectory ?? string.Empty,
            PlaceholderText = "Type or select log directory (leave empty to disable)...",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var logDirButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var browseLogButton = new Button
        {
            Content = "Browse..."
        };

        var openLogInExplorerButton = new Button
        {
            Content = "Open in Explorer"
        };

        browseLogButton.Click += (s, args) =>
        {
            var selectedPath = BrowseForFolder(logDirTextBox.Text, "Select Log Directory");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                logDirTextBox.Text = selectedPath;
            }
        };

        openLogInExplorerButton.Click += async (s, args) =>
        {
            var path = logDirTextBox.Text;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    await Launcher.LaunchFolderPathAsync(path);
                }
                catch
                {
                    // Fallback to Process.Start
                    try
                    {
                        Process.Start("explorer.exe", path);
                    }
                    catch
                    {
                        // Ignore if both fail
                    }
                }
            }
            else if (!string.IsNullOrEmpty(path))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Directory Not Found",
                    Content = "The specified directory does not exist.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        };

        logDirButtonPanel.Children.Add(browseLogButton);
        logDirButtonPanel.Children.Add(openLogInExplorerButton);

        var clearLogButton = new Button
        {
            Content = "Clear (Disable Logging)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        clearLogButton.Click += (s, args) =>
        {
            logDirTextBox.Text = string.Empty;
        };

        stackPanel.Children.Add(logDirTextBox);
        stackPanel.Children.Add(logDirButtonPanel);
        stackPanel.Children.Add(clearLogButton);

        // Logging info
        var loggingInfo = new TextBlock
        {
            Text = "📋 If the log directory is set, all changes to root categories and errors will be logged with timestamps.\n\n" +
                   "• Category changes: Saved to [CategoryName].log\n" +
                   "• Application errors: Saved to errors.log\n\n" +
                   "⚠️ Leave empty to disable logging entirely.\n\n" +
                   "💡 Tip: You can type or paste directory paths directly into the text boxes.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stackPanel.Children.Add(loggingInfo);

        var dialog = new ContentDialog
        {
            Title = "Directory Setup",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (_configService != null)
            {
                // Validate working directory
                var workingDir = workingDirTextBox.Text.Trim();
                if (string.IsNullOrEmpty(workingDir))
                {
                    await ShowErrorDialogAsync("Invalid Directory", "Working directory cannot be empty.");
                    return;
                }

                // Validate log directory if set
                var logDir = logDirTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(logDir) && !Path.IsPathRooted(logDir))
                {
                    await ShowErrorDialogAsync("Invalid Directory", "Log directory must be a valid absolute path or empty.");
                    return;
                }

                var oldWorkingDir = _configService.WorkingDirectory;
                var oldLogDir = _configService.LogDirectory;
                var workingDirChanged = oldWorkingDir != workingDir;

                _configService.WorkingDirectory = workingDir;
                _configService.LogDirectory = logDir;
                await _configService.SaveConfigurationAsync();

                // Log the configuration change
                if (_configService.IsLoggingEnabled())
                {
                    if (workingDirChanged)
                    {
                        await _configService.LogErrorAsync($"Working directory changed from '{oldWorkingDir}' to '{_configService.WorkingDirectory}'");
                    }
                    if (oldLogDir != _configService.LogDirectory)
                    {
                        await _configService.LogErrorAsync($"Log directory changed from '{oldLogDir}' to '{_configService.LogDirectory}'");
                    }
                }

                // If working directory changed, reload all categories
                if (workingDirChanged)
                {
                    await ReloadCategoriesFromNewDirectoryAsync(_configService.WorkingDirectory);
                }
                else
                {
                    StatusText.Text = "Directory settings saved successfully";
                }
            }
        }
    }

    /// <summary>
    /// Opens a modern Windows folder browser dialog starting at the specified directory.
    /// </summary>
    private string? BrowseForFolder(string? startingDirectory, string title)
    {
        try
        {
            var dialog = new FileOpenDialog() as IFileOpenDialog;
            if (dialog == null)
                return null;

            try
            {
                // Set options for folder picker
                dialog.SetOptions(FOS_PICKFOLDERS);
                dialog.SetTitle(title);

                // Set starting directory if provided and valid
                if (!string.IsNullOrEmpty(startingDirectory) && Directory.Exists(startingDirectory))
                {
                    try
                    {
                        var guid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); // IShellItem
                        SHCreateItemFromParsingName(startingDirectory, IntPtr.Zero, guid, out IShellItem item);
                        dialog.SetFolder(item);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not set starting directory: {ex.Message}");
                    }
                }

                var hWnd = WindowNative.GetWindowHandle(this);
                var hr = dialog.Show(hWnd);

                if (hr == 0) // S_OK
                {
                    dialog.GetResult(out IShellItem result);
                    result.GetDisplayName(SIGDN.FILESYSPATH, out string path);
                    return path;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in BrowseForFolder: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reloads all categories from a new working directory.
    /// </summary>
    private async Task ReloadCategoriesFromNewDirectoryAsync(string newWorkingDirectory)
    {
        try
        {
            StatusText.Text = "Reloading categories from new directory...";

            // Clear the tree view
            LinksTreeView.RootNodes.Clear();

            // Hide all viewers and show welcome screen
            HideAllViewers();
            WelcomePanel.Visibility = Visibility.Visible;

            // Reinitialize CategoryService with new directory
            _categoryService = new CategoryService(newWorkingDirectory);

            // Load categories from the new directory
            await LoadAllCategoriesAsync();

            StatusText.Text = $"Categories reloaded from: {newWorkingDirectory}";

            // Show info dialog
            var infoDialog = new ContentDialog
            {
                Title = "Categories Reloaded",
                Content = $"Successfully reloaded categories from:\n\n{newWorkingDirectory}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await infoDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error reloading categories: {ex.Message}";

            // Log the error
            if (_configService?.IsLoggingEnabled() ?? false)
            {
                await _configService.LogErrorAsync("Failed to reload categories from new directory", ex);
            }

            var errorDialog = new ContentDialog
            {
                Title = "Error Reloading Categories",
                Content = $"An error occurred while reloading categories from the new directory:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

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
            Text = hasGlobalPassword ? "✓ Global password is set" : "⚠️ No global password set",
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
                    Text = hasPassword ? "✓ Password protected" : "⚠️ Not protected",
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
                                catch (Exception ex)
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
                                Content = $"Re-encrypted {successCount} categor{(successCount == 1 ? "y" : "ies")} successfully.\n\n" +
                                         $"{errorCount} categor{(errorCount == 1 ? "y" : "ies")} failed to re-encrypt. " +
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

    // Add logging to category operations
    private async Task LogCategoryOperationAsync(string categoryName, string operation, string details = "")
    {
        if (_configService?.IsLoggingEnabled() ?? false)
        {
            await _configService.LogCategoryChangeAsync(categoryName, operation, details);
        }
    }

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}