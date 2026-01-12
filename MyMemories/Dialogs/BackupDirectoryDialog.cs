using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    
    public BackupDirectoryDialog(XamlRoot xamlRoot, FolderPickerService folderPickerService)
    {
        _xamlRoot = xamlRoot;
        _folderPickerService = folderPickerService;
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
            currentDirectories.Select(d => new BackupDirectoryItem { Path = d }));

        var mainPanel = new StackPanel { Spacing = 16, MinWidth = 500 };

        // Header
        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Configure backup directories for '{categoryName}'",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        mainPanel.Children.Add(new TextBlock
        {
            Text = "The category file will be automatically copied to these directories every time it is saved.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Directory list
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MinHeight = 150,
            MaxHeight = 250
        };

        // Populate list with directory items
        RefreshListView(listView, directories);

        mainPanel.Children.Add(listView);

        // Button panel
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 14 },
                    new TextBlock { Text = "Add Directory", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        var removeButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE738", FontSize = 14 },
                    new TextBlock { Text = "Remove Selected", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = false
        };

        var validateButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE73E", FontSize = 14 },
                    new TextBlock { Text = "Validate All", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        buttonPanel.Children.Add(addButton);
        buttonPanel.Children.Add(removeButton);
        buttonPanel.Children.Add(validateButton);
        mainPanel.Children.Add(buttonPanel);

        // Status area
        var statusText = new TextBlock
        {
            Text = directories.Count > 0 
                ? $"{directories.Count} backup directory(s) configured" 
                : "No backup directories configured",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 8, 0, 0)
        };
        mainPanel.Children.Add(statusText);

        // Event handlers
        listView.SelectionChanged += (s, e) =>
        {
            removeButton.IsEnabled = listView.SelectedItem != null;
        };

        addButton.Click += (s, e) =>
        {
            var folderPath = _folderPickerService.BrowseForFolder(null, "Select Backup Directory");
            if (!string.IsNullOrEmpty(folderPath))
            {
                // Check for duplicate
                if (directories.Any(d => d.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    // Show inline message instead of dialog to avoid nested dialog issues
                    statusText.Text = "?? This directory is already in the backup list.";
                    statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    return;
                }

                directories.Add(new BackupDirectoryItem { Path = folderPath });
                RefreshListView(listView, directories);
                statusText.Text = $"{directories.Count} backup directory(s) configured";
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
        };

        removeButton.Click += (s, e) =>
        {
            if (listView.SelectedIndex >= 0 && listView.SelectedIndex < directories.Count)
            {
                directories.RemoveAt(listView.SelectedIndex);
                RefreshListView(listView, directories);
                statusText.Text = directories.Count > 0 
                    ? $"{directories.Count} backup directory(s) configured" 
                    : "No backup directories configured";
                removeButton.IsEnabled = false;
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

            // Validate directories by actually attempting to write a test file
            var validationResults = await ValidateDirectoriesWithTestCopyAsync(directories.Select(d => d.Path));

            foreach (var dir in directories)
            {
                if (validationResults.TryGetValue(dir.Path, out var result))
                {
                    dir.IsValid = result.IsValid;
                    dir.ValidationMessage = result.IsValid ? "Valid" : result.ErrorMessage ?? "Invalid";
                }
            }

            // Refresh the list
            RefreshListView(listView, directories);

            var validCount = directories.Count(d => d.IsValid);
            statusText.Text = $"Validation complete: {validCount}/{directories.Count} valid";
            statusText.Foreground = validCount == directories.Count 
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
        };

        var dialog = new ContentDialog
        {
            Title = "Backup Directories",
            Content = new ScrollViewer
            {
                Content = mainPanel,
                MaxHeight = 500
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return directories.Select(d => d.Path).ToList();
        }

        return null;
    }

    /// <summary>
    /// Refreshes the ListView with directory items.
    /// </summary>
    private void RefreshListView(ListView listView, ObservableCollection<BackupDirectoryItem> directories)
    {
        listView.Items.Clear();
        
        foreach (var dir in directories)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = "\uE8B7", // Folder icon
                FontSize = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var pathText = new TextBlock
            {
                Text = dir.Path,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(pathText, 1);
            grid.Children.Add(pathText);

            // Show available disk space
            var availableSpace = GetAvailableDiskSpace(dir.Path);
            if (availableSpace != null)
            {
                var spacePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var spaceIcon = new FontIcon
                {
                    Glyph = "\uEDA2", // Hard drive icon
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                spacePanel.Children.Add(spaceIcon);

                var spaceText = new TextBlock
                {
                    Text = availableSpace,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                spacePanel.Children.Add(spaceText);

                Grid.SetColumn(spacePanel, 2);
                grid.Children.Add(spacePanel);
            }

            if (!string.IsNullOrEmpty(dir.ValidationMessage))
            {
                // Create a panel with icon and text for validation status
                var statusPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var statusIcon = new FontIcon
                {
                    Glyph = dir.IsValid ? "\uE73E" : "\uE711", // Checkmark or X
                    FontSize = 12,
                    Foreground = dir.IsValid
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                };
                statusPanel.Children.Add(statusIcon);

                var statusTextBlock = new TextBlock
                {
                    Text = dir.ValidationMessage,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = dir.IsValid
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                };
                statusPanel.Children.Add(statusTextBlock);

                Grid.SetColumn(statusPanel, 3);
                grid.Children.Add(statusPanel);
            }

            listView.Items.Add(grid);
        }
    }

    /// <summary>
    /// Gets the available disk space for a directory path.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>Formatted string with available space, or null if unable to determine.</returns>
    private static string? GetAvailableDiskSpace(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            // Get the drive root from the path
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
    /// Validates directories by actually attempting to write and delete a test file.
    /// This provides more accurate validation than just checking directory existence.
    /// </summary>
    private async Task<Dictionary<string, (bool IsValid, string? ErrorMessage)>> ValidateDirectoriesWithTestCopyAsync(
        IEnumerable<string> directories)
    {
        var results = new Dictionary<string, (bool IsValid, string? ErrorMessage)>();

        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            try
            {
                // Check if directory exists
                if (!System.IO.Directory.Exists(directory))
                {
                    // Try to create it
                    try
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        results[directory] = (false, $"Cannot create directory: {ex.Message}");
                        continue;
                    }
                }

                // Create a unique test file name
                var testFileName = $".backup_test_{Guid.NewGuid():N}.tmp";
                var testFilePath = System.IO.Path.Combine(directory, testFileName);

                // Try to write a test file with some content
                var testContent = $"Backup validation test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                
                await Task.Run(() =>
                {
                    // Write the test file
                    System.IO.File.WriteAllText(testFilePath, testContent);
                    
                    // Verify we can read it back
                    var readBack = System.IO.File.ReadAllText(testFilePath);
                    if (readBack != testContent)
                    {
                        throw new System.IO.IOException("File content verification failed");
                    }
                    
                    // Clean up - delete the test file
                    System.IO.File.Delete(testFilePath);
                });

                results[directory] = (true, null);
            }
            catch (UnauthorizedAccessException)
            {
                results[directory] = (false, "Access denied - no write permission");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                results[directory] = (false, "Directory not found");
            }
            catch (System.IO.PathTooLongException)
            {
                results[directory] = (false, "Path is too long");
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
        public bool IsValid { get; set; } = true;
        public string ValidationMessage { get; set; } = string.Empty;
    }
}
