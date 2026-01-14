using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for managing backup directories for a category.
/// </summary>
public class BackupDirectoryDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly FolderPickerService _folderPickerService;
    private string? _categoryFilePath;
    
    // Prefix used to mark manual backup directories in the stored list
    private const string ManualPrefix = "[MANUAL]";
    
    public BackupDirectoryDialog(XamlRoot xamlRoot, FolderPickerService folderPickerService)
    {
        _xamlRoot = xamlRoot;
        _folderPickerService = folderPickerService;
    }

    /// <summary>
    /// Sets the category file path for backup operations.
    /// </summary>
    public void SetCategoryFilePath(string? path)
    {
        _categoryFilePath = path;
    }

    /// <summary>
    /// Parses a stored directory string to extract path and mode.
    /// </summary>
    private static (string Path, bool IsAutomatic) ParseDirectoryEntry(string entry)
    {
        if (entry.StartsWith(ManualPrefix, StringComparison.Ordinal))
        {
            return (entry.Substring(ManualPrefix.Length), false);
        }
        return (entry, true);
    }

    /// <summary>
    /// Formats a directory entry for storage.
    /// </summary>
    private static string FormatDirectoryEntry(string path, bool isAutomatic)
    {
        return isAutomatic ? path : ManualPrefix + path;
    }

    /// <summary>
    /// Shows the backup directory management dialog.
    /// </summary>
    /// <param name="categoryName">Name of the category being configured.</param>
    /// <param name="currentDirectories">Current list of backup directories.</param>
    /// <returns>Updated list of backup directories, or null if cancelled.</returns>
    public async Task<List<string>?> ShowAsync(string categoryName, List<string> currentDirectories)
    {
        var directories = new ObservableCollection<BackupDirectoryItem>(
            currentDirectories.Select(d => 
            {
                var (path, isAuto) = ParseDirectoryEntry(d);
                return new BackupDirectoryItem { Path = path, IsAutomatic = isAuto };
            }));

        var mainPanel = new StackPanel { Spacing = 12, MinWidth = 750 };

        // Header
        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Configure backup directories for '{categoryName}'",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        mainPanel.Children.Add(new TextBlock
        {
            Text = "• Automatic: Backup occurs every time the category is saved\n• Manual: Use the Backup button to copy on demand",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Directory list
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MinHeight = 200,
            MaxHeight = 350
        };

        // Status area (declare early so RefreshListView can use it)
        var statusText = new TextBlock
        {
            Text = GetStatusText(directories),
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 4, 0, 0)
        };

        // Populate list with directory items
        RefreshListView(listView, directories, statusText);

        mainPanel.Children.Add(listView);

        // Button panel - use StackPanel with wrap for better layout
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 12 },
                    new TextBlock { Text = "Add", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        var editButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE70F", FontSize = 12 },
                    new TextBlock { Text = "Edit", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = false
        };

        var removeButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE738", FontSize = 12 },
                    new TextBlock { Text = "Remove", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = false
        };

        var validateButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE73E", FontSize = 12 },
                    new TextBlock { Text = "Validate", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        var backupAllButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE896", FontSize = 12 },
                    new TextBlock { Text = "Backup All", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = directories.Any(d => d.IsAutomatic) && !string.IsNullOrEmpty(_categoryFilePath) && File.Exists(_categoryFilePath)
        };
        ToolTipService.SetToolTip(backupAllButton, "Backup to all automatic directories now");

        buttonPanel.Children.Add(addButton);
        buttonPanel.Children.Add(editButton);
        buttonPanel.Children.Add(removeButton);
        buttonPanel.Children.Add(validateButton);
        buttonPanel.Children.Add(backupAllButton);
        mainPanel.Children.Add(buttonPanel);

        mainPanel.Children.Add(statusText);

        // Event handlers
        listView.SelectionChanged += (s, e) =>
        {
            var selectedItem = listView.SelectedItem;
            removeButton.IsEnabled = selectedItem != null;
            editButton.IsEnabled = selectedItem != null;
        };

        addButton.Click += (s, e) =>
        {
            var folderPath = _folderPickerService.BrowseForFolder(null, "Select Backup Directory");
            if (!string.IsNullOrEmpty(folderPath))
            {
                if (directories.Any(d => d.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    statusText.Text = "?? This directory is already in the backup list.";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    return;
                }

                directories.Add(new BackupDirectoryItem { Path = folderPath, IsAutomatic = true });
                RefreshListView(listView, directories, statusText);
                statusText.Text = GetStatusText(directories);
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                backupAllButton.IsEnabled = directories.Any(d => d.IsAutomatic) && !string.IsNullOrEmpty(_categoryFilePath) && File.Exists(_categoryFilePath);
            }
        };

        editButton.Click += (s, e) =>
        {
            if (listView.SelectedItem is Grid grid && grid.Tag is BackupDirectoryItem selectedDir)
            {
                var folderPath = _folderPickerService.BrowseForFolder(selectedDir.Path, "Select Backup Directory");
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (directories.Any(d => d != selectedDir && d.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        statusText.Text = "? This directory is already in the backup list.";
                        statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                        return;
                    }

                    selectedDir.Path = folderPath;
                    selectedDir.IsValid = true;
                    selectedDir.ValidationMessage = string.Empty;

                    RefreshListView(listView, directories, statusText);
                    statusText.Text = "? Updated backup directory";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                }
            }
        };

        removeButton.Click += (s, e) =>
        {
            if (listView.SelectedIndex >= 0 && listView.SelectedIndex < directories.Count)
            {
                directories.RemoveAt(listView.SelectedIndex);
                RefreshListView(listView, directories, statusText);
                statusText.Text = GetStatusText(directories);
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                removeButton.IsEnabled = false;
                editButton.IsEnabled = false;
                backupAllButton.IsEnabled = directories.Any(d => d.IsAutomatic) && !string.IsNullOrEmpty(_categoryFilePath) && File.Exists(_categoryFilePath);
            }
        };

        validateButton.Click += async (s, e) =>
        {
            if (directories.Count == 0)
            {
                statusText.Text = "No directories to validate.";
                return;
            }

            statusText.Text = "Validating directories...";
            statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

            var validationResults = await ValidateDirectoriesWithTestCopyAsync(directories.Select(d => d.Path));

            foreach (var dir in directories)
            {
                if (validationResults.TryGetValue(dir.Path, out var result))
                {
                    dir.IsValid = result.IsValid;
                    dir.ValidationMessage = result.IsValid ? "Valid" : result.ErrorMessage ?? "Invalid";
                }
            }

            RefreshListView(listView, directories, statusText);

            var validCount = directories.Count(d => d.IsValid);
            statusText.Text = $"Validation complete: {validCount}/{directories.Count} valid";
            statusText.Foreground = validCount == directories.Count 
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
        };

        backupAllButton.Click += async (s, e) =>
        {
            var autoDirectories = directories.Where(d => d.IsAutomatic).ToList();
            if (autoDirectories.Count == 0 || string.IsNullOrEmpty(_categoryFilePath) || !File.Exists(_categoryFilePath))
            {
                statusText.Text = "No automatic directories to backup.";
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                return;
            }

            backupAllButton.IsEnabled = false;
            statusText.Text = "Backing up to automatic directories...";
            statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

            try
            {
                var summary = await BackupService.Instance.BackupFileAsync(
                    _categoryFilePath, 
                    autoDirectories.Select(d => d.Path));

                if (summary.AllSuccessful)
                {
                    statusText.Text = $"? Backed up to {summary.SuccessCount} automatic location(s)";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                }
                else if (summary.HasFailures && summary.SuccessCount > 0)
                {
                    statusText.Text = $"? {summary.SuccessCount} succeeded, {summary.FailureCount} failed";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
                else
                {
                    statusText.Text = $"? All backups failed ({summary.FailureCount} errors)";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"? Error: {ex.Message}";
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            finally
            {
                backupAllButton.IsEnabled = true;
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Backup Directories",
            Content = new ScrollViewer
            {
                Content = mainPanel,
                MaxHeight = 550,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Convert back to storage format with [MANUAL] prefix for manual directories
            return directories.Select(d => FormatDirectoryEntry(d.Path, d.IsAutomatic)).ToList();
        }

        return null;
    }

    private string GetStatusText(ObservableCollection<BackupDirectoryItem> directories)
    {
        if (directories.Count == 0)
            return "No backup directories configured";
        
        var autoCount = directories.Count(d => d.IsAutomatic);
        var manualCount = directories.Count - autoCount;
        
        return $"{directories.Count} directory(s): {autoCount} automatic, {manualCount} manual";
    }

    /// <summary>
    /// Refreshes the ListView with directory items including mode toggle and backup button.
    /// </summary>
    private void RefreshListView(ListView listView, ObservableCollection<BackupDirectoryItem> directories, TextBlock statusText)
    {
        // Check file existence once for all buttons
        bool canBackup = !string.IsNullOrEmpty(_categoryFilePath) && File.Exists(_categoryFilePath);
        
        listView.Items.Clear();
        
        foreach (var dir in directories)
        {
            var grid = new Grid { Tag = dir, MinHeight = 40, Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) }); // Mode toggle
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Path
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // Disk space
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // Backup button
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Validation

            // Backup button (create first so toggle can reference it)
            var backupButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE896", FontSize = 12 },
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Center,
                // Show for manual directories
                Visibility = dir.IsAutomatic ? Visibility.Collapsed : Visibility.Visible,
                // Always enable - we'll check file existence when clicked
                IsEnabled = true
            };
            ToolTipService.SetToolTip(backupButton, canBackup ? "Backup now" : "Save category first to enable backup");

            // Mode toggle (Auto/Manual)  
            var modeToggle = new ToggleSwitch
            {
                IsOn = dir.IsAutomatic,
                OnContent = "Auto",
                OffContent = "Manual",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                MinWidth = 0
            };
            
            var currentDir = dir; // Capture for closure
            var capturedBackupButton = backupButton; // Capture for closure
            var capturedDirectories = directories; // Capture for closure
            var capturedStatusText = statusText; // Capture for closure
            
            modeToggle.Toggled += (s, e) =>
            {
                // Update the data model
                currentDir.IsAutomatic = modeToggle.IsOn;
                
                // Update backup button visibility and state
                if (modeToggle.IsOn)
                {
                    // Automatic mode - hide button
                    capturedBackupButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Manual mode - show and enable button
                    capturedBackupButton.Visibility = Visibility.Visible;
                    capturedBackupButton.IsEnabled = true;
                }
                
                // Update status text
                capturedStatusText.Text = GetStatusText(capturedDirectories);
            };
            Grid.SetColumn(modeToggle, 0);
            grid.Children.Add(modeToggle);

            // Path text with folder icon
            var pathPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            pathPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE8B7",
                FontSize = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            pathPanel.Children.Add(new TextBlock
            {
                Text = dir.Path,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300
            });
            Grid.SetColumn(pathPanel, 1);
            grid.Children.Add(pathPanel);

            // Disk space
            var availableSpace = GetAvailableDiskSpace(dir.Path);
            var spaceText = new TextBlock
            {
                Text = availableSpace ?? "",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(spaceText, 2);
            grid.Children.Add(spaceText);

            // Wire up backup button click
            backupButton.Click += async (s, e) =>
            {
                await PerformSingleBackupAsync(currentDir, (Button)s!);
            };
            
            Grid.SetColumn(backupButton, 3);
            grid.Children.Add(backupButton);

            // Validation status
            if (!string.IsNullOrEmpty(dir.ValidationMessage))
            {
                var statusIcon = new FontIcon
                {
                    Glyph = dir.IsValid ? "\uE73E" : "\uE711",
                    FontSize = 14,
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = dir.IsValid
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                };
                ToolTipService.SetToolTip(statusIcon, dir.ValidationMessage);
                Grid.SetColumn(statusIcon, 4);
                grid.Children.Add(statusIcon);
            }

            listView.Items.Add(grid);
        }
    }

    /// <summary>
    /// Performs a backup to a single directory.
    /// </summary>
    private async Task PerformSingleBackupAsync(BackupDirectoryItem dir, Button backupButton)
    {
        // Check if file exists
        if (string.IsNullOrEmpty(_categoryFilePath) || !File.Exists(_categoryFilePath))
        {
            // Show error feedback on button
            var originalContent = backupButton.Content;
            backupButton.Content = new FontIcon 
            { 
                Glyph = "\uE7BA", // Warning icon
                FontSize = 12, 
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange) 
            };
            ToolTipService.SetToolTip(backupButton, "Category file not found. Save the category first.");
            
            await Task.Delay(3000);
            backupButton.Content = originalContent;
            ToolTipService.SetToolTip(backupButton, "Backup now");
            return;
        }

        backupButton.IsEnabled = false;
        var originalButtonContent = backupButton.Content;
        backupButton.Content = new ProgressRing { Width = 12, Height = 12, IsActive = true };

        try
        {
            var summary = await BackupService.Instance.BackupFileAsync(_categoryFilePath, new[] { dir.Path });

            if (summary.AllSuccessful)
            {
                backupButton.Content = new FontIcon 
                { 
                    Glyph = "\uE73E", 
                    FontSize = 12, 
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                };
                ToolTipService.SetToolTip(backupButton, "Backup successful!");
                
                await Task.Delay(2000);
                backupButton.Content = originalButtonContent;
                ToolTipService.SetToolTip(backupButton, "Backup now");
            }
            else
            {
                var error = summary.Results.FirstOrDefault(r => !r.Success);
                backupButton.Content = new FontIcon 
                { 
                    Glyph = "\uE711", 
                    FontSize = 12, 
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) 
                };
                ToolTipService.SetToolTip(backupButton, $"Failed: {error?.ErrorMessage ?? "Unknown error"}");
                
                await Task.Delay(3000);
                backupButton.Content = originalButtonContent;
                ToolTipService.SetToolTip(backupButton, "Backup now");
            }
        }
        catch (Exception ex)
        {
            backupButton.Content = new FontIcon 
            { 
                Glyph = "\uE711", 
                FontSize = 12, 
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) 
            };
            ToolTipService.SetToolTip(backupButton, $"Error: {ex.Message}");
            
            await Task.Delay(3000);
            backupButton.Content = originalButtonContent;
            ToolTipService.SetToolTip(backupButton, "Backup now");
        }
        finally
        {
            backupButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Gets the available disk space for a directory path.
    /// </summary>
    private static string? GetAvailableDiskSpace(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var root = System.IO.Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
                return null;

            var driveInfo = new System.IO.DriveInfo(root);
            if (!driveInfo.IsReady)
                return null;

            var availableBytes = (ulong)driveInfo.AvailableFreeSpace;
            return $"{FormatFileSize(availableBytes)} free";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    /// <summary>
    /// Validates directories by attempting to write and delete a test file.
    /// </summary>
    private async Task<Dictionary<string, (bool IsValid, string? ErrorMessage)>> ValidateDirectoriesWithTestCopyAsync(
        IEnumerable<string> directories)
    {
        var results = new Dictionary<string, (bool IsValid, string? ErrorMessage)>();

        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            try
            {
                if (!System.IO.Directory.Exists(directory))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        results[directory] = (false, $"Cannot create: {ex.Message}");
                        continue;
                    }
                }

                var testFileName = $".backup_test_{Guid.NewGuid():N}.tmp";
                var testFilePath = System.IO.Path.Combine(directory, testFileName);
                var testContent = $"Backup validation test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                
                await Task.Run(() =>
                {
                    System.IO.File.WriteAllText(testFilePath, testContent);
                    var readBack = System.IO.File.ReadAllText(testFilePath);
                    if (readBack != testContent)
                    {
                        throw new System.IO.IOException("File content verification failed");
                    }
                    System.IO.File.Delete(testFilePath);
                });

                results[directory] = (true, null);
            }
            catch (UnauthorizedAccessException)
            {
                results[directory] = (false, "Access denied");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                results[directory] = (false, "Not found");
            }
            catch (System.IO.PathTooLongException)
            {
                results[directory] = (false, "Path too long");
            }
            catch (System.IO.IOException ex)
            {
                results[directory] = (false, $"IO error: {ex.Message}");
            }
            catch (Exception ex)
            {
                results[directory] = (false, ex.Message);
            }
        }

        return results;
    }

    /// <summary>
    /// Internal class for backup directory list items.
    /// </summary>
    private class BackupDirectoryItem
    {
        public string Path { get; set; } = string.Empty;
        public bool IsAutomatic { get; set; } = true;
        public bool IsValid { get; set; } = true;
        public string ValidationMessage { get; set; } = string.Empty;
    }
}
