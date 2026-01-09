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

namespace MyMemories;

public sealed partial class MainWindow
{
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
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE0A6", // Back arrow icon
                        FontSize = 12
                    },
                    new TextBlock
                    {
                        Text = "Back to List",
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        logViewerHeader.Children.Add(backButton);

        var viewerFileIcon = new FontIcon
        {
            Glyph = "\uE8A5", // Document icon
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
        logViewerHeader.Children.Add(viewerFileIcon);

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
                    viewerFileNameText.Text = fileInfo.Name;
                    viewerFileInfoText.Text = $"{lineCount:N0} lines • {FormatFileSize(fileInfo.Length)} • Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}";
                    logContentTextBox.Text = content;

                    // Switch to viewer mode
                    logFilesListView.Visibility = Visibility.Collapsed;
                    logFilesHeader.Visibility = Visibility.Collapsed;
                    logViewerPanel.Visibility = Visibility.Visible;
                    
                    // Scroll to bottom by setting the selection to the end
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        // Move selection to the end of the text to scroll to bottom
                        logContentTextBox.SelectionStart = logContentTextBox.Text.Length;
                        logContentTextBox.SelectionLength = 0;
                        
                        // Also try ChangeView as backup
                        if (logContentScrollViewer.ScrollableHeight > 0)
                        {
                            logContentScrollViewer.ChangeView(null, logContentScrollViewer.ScrollableHeight, null, disableAnimation: false);
                        }
                    });
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

        // Initialize the log files list on dialog open
        RefreshLogFilesList(_configService?.LogDirectory ?? string.Empty);

        // Refresh log files list when log directory text box changes
        logDirTextBox.TextChanged += (s, args) =>
        {
            RefreshLogFilesList(logDirTextBox.Text.Trim());
        };

        // Logging info
        var loggingInfoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var loggingIcon = new FontIcon
        {
            Glyph = "\uE7C3", // Clipboard icon
            FontSize = 16,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0)
        };
        loggingInfoPanel.Children.Add(loggingIcon);

        var loggingInfo = new TextBlock
        {
            Text = "Logging is enabled by default. Category operations are logged to:\n" +
                   "• [CategoryName].log - All category operations (add, remove, rename, changes)\n" +
                   "• error.log - Application errors\n\n" +
                   "Default location: %LocalAppData%\\MyMemories\\Logs\n\n" +
                   "Tip: You can change the log directory or clear it to disable logging.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };
        loggingInfoPanel.Children.Add(loggingInfo);

        stackPanel.Children.Add(loggingInfoPanel);

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
}
