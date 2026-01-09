using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace MyMemories;

public sealed partial class MainWindow
{
    private FolderPickerService? _folderPickerService;

    /// <summary>
    /// Validates configuration directories on startup and offers to create missing ones.
    /// </summary>
    /// <returns>True if validation passed or user fixed issues; False if critical errors remain.</returns>
    private async Task<bool> ValidateConfigurationDirectoriesAsync()
    {
        if (_configService == null)
            return false;

        var issues = new List<(string Type, string Path, string Issue)>();
        var workingDir = _configService.WorkingDirectory;
        var logDir = _configService.LogDirectory;

        // Validate working directory
        var workingValidation = PathValidationUtilities.ValidateDirectoryPath(workingDir, allowEmpty: false);
        if (!workingValidation.IsValid)
        {
            issues.Add(("Working Directory", workingDir, workingValidation.ErrorMessage ?? "Invalid path"));
        }
        else if (!Directory.Exists(workingDir))
        {
            issues.Add(("Working Directory", workingDir, "Directory does not exist"));
        }

        // Validate log directory if set
        if (!string.IsNullOrEmpty(logDir))
        {
            var logValidation = PathValidationUtilities.ValidateDirectoryPath(logDir, allowEmpty: true);
            if (!logValidation.IsValid)
            {
                issues.Add(("Log Directory", logDir, logValidation.ErrorMessage ?? "Invalid path"));
            }
            else if (!Directory.Exists(logDir))
            {
                issues.Add(("Log Directory", logDir, "Directory does not exist"));
            }
        }

        // Check for write access if directories exist
        if (Directory.Exists(workingDir))
        {
            if (!await TestDirectoryWriteAccessAsync(workingDir))
            {
                issues.Add(("Working Directory", workingDir, "No write access"));
            }
        }

        if (!string.IsNullOrEmpty(logDir) && Directory.Exists(logDir))
        {
            if (!await TestDirectoryWriteAccessAsync(logDir))
            {
                issues.Add(("Log Directory", logDir, "No write access"));
            }
        }

        // If no issues, return success
        if (!issues.Any())
            return true;

        // Build issue message
        var issueMessage = "Configuration validation found the following issues:\n\n";
        var canAutoFix = true;

        foreach (var issue in issues)
        {
            issueMessage += $"• {issue.Type}: {issue.Issue}\n  Path: {issue.Path}\n\n";
            
            // Can't auto-fix invalid paths or permission issues
            if (issue.Issue.Contains("Invalid") || issue.Issue.Contains("write access"))
            {
                canAutoFix = false;
            }
        }

        if (canAutoFix)
        {
            issueMessage += "Would you like to create the missing directories?";

            var fixDialog = new ContentDialog
            {
                Title = "Configuration Issues Detected",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = issueMessage,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                PrimaryButtonText = "Create Directories",
                SecondaryButtonText = "Open Settings",
                CloseButtonText = "Continue Anyway",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await fixDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Try to create missing directories
                var failedDirs = new List<string>();

                foreach (var issue in issues.Where(i => i.Issue == "Directory does not exist"))
                {
                    if (!await PathValidationUtilities.EnsureDirectoryExistsAsync(issue.Path))
                    {
                        failedDirs.Add($"{issue.Type}: {issue.Path}");
                    }
                    else
                    {
                        StatusText.Text = $"Created {issue.Type.ToLower()}: {issue.Path}";
                        
                        // Log the directory creation
                        if (_configService.IsLoggingEnabled())
                        {
                            await _configService.LogErrorAsync($"Created missing {issue.Type.ToLower()} during startup validation");
                        }
                    }
                }

                if (failedDirs.Any())
                {
                    await ShowErrorDialogAsync(
                        "Failed to Create Directories",
                        $"Could not create the following directories:\n\n{string.Join("\n", failedDirs)}\n\n" +
                        "Please check permissions or manually create these directories."
                    );
                    return false;
                }

                return true; // Successfully created directories
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Open settings dialog
                await ShowDirectorySetupDialogAsync();
                
                // Revalidate after settings change
                return await ValidateConfigurationDirectoriesAsync();
            }
            else
            {
                // Continue anyway - log warning
                StatusText.Text = "⚠️ Warning: Configuration issues detected but ignored";
                
                if (_configService.IsLoggingEnabled())
                {
                    await _configService.LogErrorAsync("Configuration validation issues ignored by user");
                }
                
                return true; // Allow app to continue
            }
        }
        else
        {
            // Can't auto-fix - show error and offer settings
            issueMessage += "\nThese issues require manual correction.\n\n" +
                           "Please update your configuration in Settings.";

            var errorDialog = new ContentDialog
            {
                Title = "Configuration Errors",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = issueMessage,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                PrimaryButtonText = "Open Settings",
                CloseButtonText = "Continue Anyway",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await errorDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ShowDirectorySetupDialogAsync();
                
                // Revalidate after settings change
                return await ValidateConfigurationDirectoriesAsync();
            }
            else
            {
                StatusText.Text = "⚠️ Warning: Running with invalid configuration";
                return true; // Allow app to continue (risky)
            }
        }
    }

    /// <summary>
    /// Tests if the application has write access to a directory.
    /// </summary>
    private async Task<bool> TestDirectoryWriteAccessAsync(string directoryPath)
    {
        try
        {
            var testFile = Path.Combine(directoryPath, $".writetest_{Guid.NewGuid()}.tmp");
            
            await Task.Run(() =>
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            });
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void MenuConfig_DirectorySetup_Click(object sender, RoutedEventArgs e)
    {
        await ShowDirectorySetupDialogAsync();
    }

    private async void MenuConfig_SecuritySetup_Click(object sender, RoutedEventArgs e)
    {
        await ShowSecuritySetupDialogAsync();
    }

    private async void MenuConfig_Options_Click(object sender, RoutedEventArgs e)
    {
        await ShowOptionsDialogAsync();
    }

    private async Task ShowDirectorySetupDialogAsync()
    {
        // Initialize folder picker service if needed
        _folderPickerService ??= new FolderPickerService(this);

        // Create UI for directory setup
        var stackPanel = new StackPanel { Spacing = 16 };

        // Info banner
        var infoBanner = new InfoBar
        {
            Title = "Default Directories",
            Message = "These directories are set to the default locations where JSON category files and logs are stored. You can type or paste paths directly.",
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
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
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 720  // 20% wider than 600
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
            var selectedPath = _folderPickerService?.BrowseForFolder(workingDirTextBox.Text, "Select Working Directory");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                workingDirTextBox.Text = selectedPath;
            }
        };

        openWorkingInExplorerButton.Click += async (s, args) =>
        {
            await OpenDirectoryInExplorerAsync(workingDirTextBox.Text);
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
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 720  // Same as working directory
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
            var selectedPath = _folderPickerService?.BrowseForFolder(logDirTextBox.Text, "Select Log Directory");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                logDirTextBox.Text = selectedPath;
            }
        };

        openLogInExplorerButton.Click += async (s, args) =>
        {
            await OpenDirectoryInExplorerAsync(logDirTextBox.Text);
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

        // Log Files Section
        var logFilesHeader = new TextBlock
        {
            Text = "Log Files in Directory (click to view):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        };
        stackPanel.Children.Add(logFilesHeader);

        var logFilesListView = new ListView
        {
            MaxHeight = 180,
            SelectionMode = ListViewSelectionMode.Single,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var logFilesEmptyText = new TextBlock
        {
            Text = "No log files found or directory not set.",
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Log file viewer panel (hidden initially)
        var logViewerPanel = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            Spacing = 8
        };

        // Back button and file info header
        var logViewerHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var backButton = new Button
        {
            Content = "← Back to List"
        };
        logViewerHeader.Children.Add(backButton);

        var viewerFileNameText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        logViewerHeader.Children.Add(viewerFileNameText);

        var viewerFileInfoText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 11
        };
        logViewerHeader.Children.Add(viewerFileInfoText);

        logViewerPanel.Children.Add(logViewerHeader);

        // Log content text box
        var logContentTextBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 11,
            MaxHeight = 350,
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };

        var logContentScrollViewer = new ScrollViewer
        {
            Content = logContentTextBox,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 350
        };
        logViewerPanel.Children.Add(logContentScrollViewer);

        // Action buttons for log viewer
        var logViewerButtonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var openInNotepadButton = new Button { Content = "Open in Notepad" };
        var openFolderButton = new Button { Content = "Open Folder" };
        logViewerButtonsPanel.Children.Add(openInNotepadButton);
        logViewerButtonsPanel.Children.Add(openFolderButton);
        logViewerPanel.Children.Add(logViewerButtonsPanel);

        // Variable to track current file path
        string? currentLogFilePath = null;

        // Back button click - return to list view
        backButton.Click += (s, args) =>
        {
            logViewerPanel.Visibility = Visibility.Collapsed;
            logFilesListView.Visibility = Visibility.Visible;
            logFilesHeader.Visibility = Visibility.Visible;
            currentLogFilePath = null;
        };

        // Open in Notepad
        openInNotepadButton.Click += (s, args) =>
        {
            if (!string.IsNullOrEmpty(currentLogFilePath))
            {
                try
                {
                    Process.Start("notepad.exe", currentLogFilePath);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error opening Notepad: {ex.Message}";
                }
            }
        };

        // Open containing folder
        openFolderButton.Click += (s, args) =>
        {
            if (!string.IsNullOrEmpty(currentLogFilePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{currentLogFilePath}\"");
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error opening folder: {ex.Message}";
                }
            }
        };

        // Store file paths for click handling
        var logFilePathMap = new Dictionary<Grid, string>();

        // Function to refresh log files list
        void RefreshLogFilesList(string logDir)
        {
            logFilesListView.Items.Clear();
            logFilePathMap.Clear();

            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
            {
                logFilesListView.Visibility = Visibility.Collapsed;
                logFilesEmptyText.Visibility = Visibility.Visible;
                logFilesEmptyText.Text = string.IsNullOrEmpty(logDir) 
                    ? "Log directory not set." 
                    : "Directory does not exist.";
                return;
            }

            try
            {
                var logFiles = Directory.GetFiles(logDir, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)  // Newest first
                    .ToList();

                if (logFiles.Count == 0)
                {
                    logFilesListView.Visibility = Visibility.Collapsed;
                    logFilesEmptyText.Visibility = Visibility.Visible;
                    logFilesEmptyText.Text = "No log files found in this directory.";
                    return;
                }

                logFilesListView.Visibility = Visibility.Visible;
                logFilesEmptyText.Visibility = Visibility.Collapsed;

                foreach (var file in logFiles)
                {
                    var lineCount = 0;
                    try
                    {
                        lineCount = File.ReadLines(file.FullName).Count();
                    }
                    catch
                    {
                        // Ignore read errors
                    }

                    var itemPanel = new Grid
                    {
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // File name with icon
                    var fileNamePanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6
                    };
                    fileNamePanel.Children.Add(new FontIcon
                    {
                        Glyph = "\uE8A5", // Document icon
                        FontSize = 14,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    });
                    fileNamePanel.Children.Add(new TextBlock
                    {
                        Text = file.Name,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    });
                    Grid.SetColumn(fileNamePanel, 0);
                    itemPanel.Children.Add(fileNamePanel);

                    // Modified date
                    var modifiedText = new TextBlock
                    {
                        Text = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    Grid.SetColumn(modifiedText, 1);
                    itemPanel.Children.Add(modifiedText);

                    // Line count
                    var lineCountText = new TextBlock
                    {
                        Text = $"{lineCount:N0} lines",
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 0, 0),
                        MinWidth = 70,
                        TextAlignment = TextAlignment.Right
                    };
                    Grid.SetColumn(lineCountText, 2);
                    itemPanel.Children.Add(lineCountText);

                    // File size
                    var sizeText = new TextBlock
                    {
                        Text = FormatFileSize(file.Length),
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 0, 0),
                        MinWidth = 60,
                        TextAlignment = TextAlignment.Right
                    };
                    Grid.SetColumn(sizeText, 3);
                    itemPanel.Children.Add(sizeText);

                    // Store file path for click handling
                    logFilePathMap[itemPanel] = file.FullName;

                    logFilesListView.Items.Add(itemPanel);
                }
            }
            catch (Exception ex)
            {
                logFilesListView.Visibility = Visibility.Collapsed;
                logFilesEmptyText.Visibility = Visibility.Visible;
                logFilesEmptyText.Text = $"Error reading directory: {ex.Message}";
            }
        }

        // Handle log file click to view contents inline
        logFilesListView.SelectionChanged += async (s, args) =>
        {
            if (logFilesListView.SelectedItem is Grid selectedGrid && logFilePathMap.TryGetValue(selectedGrid, out var filePath))
            {
                // Clear selection immediately so user can click the same file again
                logFilesListView.SelectedItem = null;

                // Read and display the log file content inline
                try
                {
                    if (!File.Exists(filePath))
                    {
                        StatusText.Text = "Log file no longer exists";
                        return;
                    }

                    var fileInfo = new FileInfo(filePath);
                    var content = await File.ReadAllTextAsync(filePath);
                    var lineCount = content.Split('\n').Length;

                    // Update viewer info
                    currentLogFilePath = filePath;
                    viewerFileNameText.Text = $"📄 {fileInfo.Name}";
                    viewerFileInfoText.Text = $"{lineCount:N0} lines • {FormatFileSize(fileInfo.Length)} • Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}";
                    logContentTextBox.Text = content;

                    // Switch to viewer mode
                    logFilesListView.Visibility = Visibility.Collapsed;
                    logFilesHeader.Visibility = Visibility.Collapsed;
                    logViewerPanel.Visibility = Visibility.Visible;
                    
                    // Scroll to bottom of log file after content is loaded
                    await Task.Delay(50); // Small delay to ensure UI is rendered
                    logContentScrollViewer.ChangeView(null, logContentScrollViewer.ScrollableHeight, null, disableAnimation: true);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error reading log file: {ex.Message}";
                }
            }
        };

        // Log file viewer panel initialization (moved up for clarity)
        stackPanel.Children.Add(logFilesListView);
        stackPanel.Children.Add(logFilesEmptyText);
        stackPanel.Children.Add(logViewerPanel);

        // Logging info
        var loggingInfo = new TextBlock
        {
            Text = "📋 Logging is enabled by default. Category operations are logged to:\n" +
                   "• [CategoryName].log - All category operations (add, remove, rename, changes)\n" +
                   "• error.log - Application errors\n\n" +
                   "Default location: %LocalAppData%\\MyMemories\\Logs\n\n" +
                   "💡 Tip: You can change the log directory or clear it to disable logging.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stackPanel.Children.Add(loggingInfo);

        // Set explicit width on the stack panel to force the dialog wider
        stackPanel.Width = 700;

        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            MaxHeight = 700,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Width = 700
        };

        var dialog = new ContentDialog
        {
            Title = "Directory Setup",
            Content = scrollViewer,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        // Add a Loaded event handler to debug the actual width
        dialog.Loaded += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Directory Setup Dialog] Loaded - ActualWidth: {dialog.ActualWidth}, Width: {dialog.Width}");
            System.Diagnostics.Debug.WriteLine($"[Directory Setup Dialog] StackPanel ActualWidth: {stackPanel.ActualWidth}, Width: {stackPanel.Width}");
            System.Diagnostics.Debug.WriteLine($"[Directory Setup Dialog] ScrollViewer ActualWidth: {scrollViewer.ActualWidth}, Width: {scrollViewer.Width}");
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (_configService != null)
            {
                // Validate working directory
                var workingDir = workingDirTextBox.Text.Trim();
                var workingValidation = PathValidationUtilities.ValidateDirectoryPath(workingDir, allowEmpty: false);
                if (!workingValidation.IsValid)
                {
                    await ShowErrorDialogAsync("Invalid Working Directory", workingValidation.ErrorMessage!);
                    return;
                }

                // Validate log directory if set
                var logDir = logDirTextBox.Text.Trim();
                var logValidation = PathValidationUtilities.ValidateDirectoryPath(logDir, allowEmpty: true);
                if (!logValidation.IsValid)
                {
                    await ShowErrorDialogAsync("Invalid Log Directory", logValidation.ErrorMessage!);
                    return;
                }

                // Check if directories need to be created
                var directoriesToCreate = new List<(string Path, string Type)>();
                
                if (!Directory.Exists(workingDir))
                {
                    directoriesToCreate.Add((workingDir, "Working"));
                }
                
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    directoriesToCreate.Add((logDir, "Log"));
                }

                // Prompt to create missing directories
                if (directoriesToCreate.Any())
                {
                    var createMessage = "The following directories do not exist:\n\n";
                    foreach (var dir in directoriesToCreate)
                    {
                        createMessage += $"• {dir.Type}: {dir.Path}\n";
                    }
                    createMessage += "\nWould you like to create them?";

                    var createDialog = new ContentDialog
                    {
                        Title = "Create Directories",
                        Content = createMessage,
                        PrimaryButtonText = "Create",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = Content.XamlRoot
                    };

                    if (await createDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        var failedDirs = new List<string>();
                        
                        foreach (var dir in directoriesToCreate)
                        {
                            if (!await PathValidationUtilities.EnsureDirectoryExistsAsync(dir.Path))
                            {
                                failedDirs.Add($"{dir.Type}: {dir.Path}");
                            }
                        }

                        if (failedDirs.Any())
                        {
                            await ShowErrorDialogAsync(
                                "Failed to Create Directories",
                                $"Could not create the following directories:\n\n{string.Join("\n", failedDirs)}"
                            );
                            return;
                        }
                    }
                    else
                    {
                        return; // User cancelled directory creation
                    }
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

    private async Task ShowOptionsDialogAsync()
    {
        if (_configService == null)
            return;

        // Create UI for options
        var stackPanel = new StackPanel { Spacing = 24 };

        // === Performance Section ===
        var performanceHeader = new TextBlock
        {
            Text = "⚡ Performance Settings",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stackPanel.Children.Add(performanceHeader);

        // Zip Compression Level
        var zipCompressionLabel = new TextBlock
        {
            Text = "Zip Compression Level:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(zipCompressionLabel);

        // Description
        var zipDescription = new TextBlock
        {
            Text = "Controls the balance between compression speed and file size when creating zip archives.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(zipDescription);

        // Slider container with labels
        var sliderContainer = new Grid
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Level labels (top row)
        var labelPanel = new Grid();
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fastLabel = new TextBlock
        {
            Text = "Fast",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(fastLabel, 0);
        labelPanel.Children.Add(fastLabel);

        var balancedLabel = new TextBlock
        {
            Text = "Balanced",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(balancedLabel, 1);
        labelPanel.Children.Add(balancedLabel);

        var maxLabel = new TextBlock
        {
            Text = "Maximum",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(maxLabel, 2);
        labelPanel.Children.Add(maxLabel);

        Grid.SetRow(labelPanel, 0);
        sliderContainer.Children.Add(labelPanel);

        // Slider
        var compressionSlider = new Slider
        {
            Minimum = 0,
            Maximum = 9,
            Value = _configService.ZipCompressionLevel,
            StepFrequency = 1,
            TickFrequency = 1,
            SnapsTo = Microsoft.UI.Xaml.Controls.Primitives.SliderSnapsTo.StepValues,
            Margin = new Thickness(0, 4, 0, 4)
        };
        Grid.SetRow(compressionSlider, 1);
        sliderContainer.Children.Add(compressionSlider);

        // Value display
        var valueDisplay = new TextBlock
        {
            Text = $"Level: {_configService.ZipCompressionLevel}",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(valueDisplay, 2);
        sliderContainer.Children.Add(valueDisplay);

        // Update value display when slider changes
        compressionSlider.ValueChanged += (s, args) =>
        {
            var level = (int)args.NewValue;
            valueDisplay.Text = $"Level: {level}";
        };

        stackPanel.Children.Add(sliderContainer);

        // Compression level guide
        var guidePanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 16)
        };

        guidePanel.Children.Add(new TextBlock
        {
            Text = "💡 Compression Guide:",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 0-3: Fast compression, larger files - Best for quick backups",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 4-6: Balanced - Good for everyday use",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 7-9: Maximum compression, slower - Best for archiving",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        stackPanel.Children.Add(guidePanel);

        // Info banner
        var infoBanner = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE946", // Info icon
                        FontSize = 16,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    },
                    new TextBlock
                    {
                        Text = "Changes will apply to all future zip operations. Existing zip files are not affected.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 12
                    }
                }
            }
        };
        stackPanel.Children.Add(infoBanner);

        // Create dialog
        var dialog = new ContentDialog
        {
            Title = "Options",
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
            var newLevel = (int)compressionSlider.Value;
            _configService.ZipCompressionLevel = newLevel;
            await _configService.SaveConfigurationAsync();

            StatusText.Text = $"Options saved - Zip compression level set to {newLevel}";

            // Log the change
            if (_configService.IsLoggingEnabled())
            {
                await _configService.LogErrorAsync($"Zip compression level changed to {newLevel}");
            }
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

    /// <summary>
    /// Opens a directory in Windows Explorer with proper error handling.
    /// </summary>
    private async Task OpenDirectoryInExplorerAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            await ShowErrorDialogAsync("No Directory", "No directory path specified.");
            return;
        }

        if (!Directory.Exists(path))
        {
            await ShowErrorDialogAsync("Directory Not Found", "The specified directory does not exist.");
            return;
        }

        try
        {
            await Launcher.LaunchFolderPathAsync(path);
        }
        catch
        {
            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error Opening Explorer", $"Failed to open directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N1} {suffixes[suffixIndex]}";
    }

    /// <summary>
    /// Shows a dialog to view the contents of a log file.
    /// </summary>
    private async Task ShowLogFileViewerAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            await ShowErrorDialogAsync("File Not Found", $"The log file no longer exists:\n{filePath}");
            return;
        }

        try
        {
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);
            var content = await File.ReadAllTextAsync(filePath);
            var lineCount = content.Split('\n').Length;

            // Create the viewer UI
            var viewerPanel = new StackPanel { Spacing = 8 };

            // File info header
            var infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"📄 {fileName}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"{lineCount:N0} lines",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            
            infoPanel.Children.Add(new TextBlock
            {
                Text = FormatFileSize(fileInfo.Length),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            
            viewerPanel.Children.Add(infoPanel);

            // Log content text box (read-only, scrollable)
            var logTextBox = new TextBox
            {
                Text = content,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 11,
                Height = 400,
                BorderThickness = new Thickness(1),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };

            // Wrap in scroll viewer for horizontal scrolling
            var scrollViewer = new ScrollViewer
            {
                Content = logTextBox,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };

            viewerPanel.Children.Add(scrollViewer);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var openInNotepadButton = new Button
            {
                Content = "Open in Notepad"
            };
            openInNotepadButton.Click += (s, args) =>
            {
                try
                {
                    Process.Start("notepad.exe", filePath);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error opening Notepad: {ex.Message}";
                }
            };
            buttonPanel.Children.Add(openInNotepadButton);

            var openFolderButton = new Button
            {
                Content = "Open Containing Folder"
            };
            openFolderButton.Click += async (s, args) =>
            {
                try
                {
                    var folder = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync("Error", $"Failed to open folder: {ex.Message}");
                }
            };
            buttonPanel.Children.Add(openFolderButton);

            viewerPanel.Children.Add(buttonPanel);

            // Show dialog
            var dialog = new ContentDialog
            {
                Title = "Log File Viewer",
                Content = viewerPanel,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
                MinWidth = 900,
                MaxHeight = 700
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Reading Log File", $"Failed to read log file:\n{ex.Message}");
        }
    }
}