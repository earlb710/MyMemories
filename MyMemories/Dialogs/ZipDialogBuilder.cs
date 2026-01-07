using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Builder for zip-related dialogs.
/// </summary>
public class ZipDialogBuilder
{
    private readonly Window _parentWindow;
    private readonly XamlRoot _xamlRoot;
    private readonly FolderPickerService _folderPickerService;

    public ZipDialogBuilder(Window parentWindow, XamlRoot xamlRoot)
    {
        _parentWindow = parentWindow;
        _xamlRoot = xamlRoot;
        _folderPickerService = new FolderPickerService(parentWindow);
    }

    public async Task<ZipFolderResult?> ShowZipFolderDialogAsync(
        string folderTitle, 
        string defaultTargetDirectory, 
        string[] sourceFolderPaths,
        bool categoryHasPassword = false,
        string? categoryPassword = null)
    {
        var (stackPanel, zipFileNameTextBox, targetDirectoryTextBox, statsTextBlock, availableSpaceTextBlock, linkToCategoryCheckBox, usePasswordCheckBox) = 
            BuildZipDialogUI(folderTitle, defaultTargetDirectory, sourceFolderPaths, categoryHasPassword);

        // Calculate and display folder statistics
        await UpdateFolderStatisticsAsync(sourceFolderPaths, statsTextBlock);
        
        // Update available space for target directory
        await UpdateAvailableSpaceAsync(defaultTargetDirectory, availableSpaceTextBlock);

        var dialog = new ContentDialog
        {
            Title = "Create Zip Archive",
            Content = new ScrollViewer 
            { 
                Content = stackPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = "Create Zip",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                    !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text)
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return await CreateZipResult(zipFileNameTextBox, targetDirectoryTextBox, 
                linkToCategoryCheckBox, usePasswordCheckBox, categoryPassword);
        }

        return null;
    }

    public async Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory, string sourceFolderPath)
    {
        // For single source, wrap in array
        return await ShowZipFolderDialogAsync(folderTitle, defaultTargetDirectory, new[] { sourceFolderPath }, false, null);
    }

    private (StackPanel, TextBox, TextBox, TextBlock, TextBlock, CheckBox, CheckBox?) BuildZipDialogUI(
        string folderTitle, 
        string defaultTargetDirectory, 
        string[] sourceFolderPaths,
        bool categoryHasPassword)
    {
        // ADD THIS DEBUG LINE
        System.Diagnostics.Debug.WriteLine($"[ZipDialogBuilder] Building UI - categoryHasPassword: {categoryHasPassword}");
        
        var zipFileNameTextBox = new TextBox
        {
            Text = folderTitle,
            PlaceholderText = "Enter zip file name (without .zip extension)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var targetDirectoryTextBox = new TextBox
        {
            Text = defaultTargetDirectory,
            PlaceholderText = "Enter target directory path",
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Statistics text block
        var statsTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 16),
            Text = "📊 Calculating folder statistics..."
        };

        // Available space text block
        var availableSpaceTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 16),
            Text = "💾 Calculating available space..."
        };

        var browseButton = new Button
        {
            Content = "Browse...",
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Browse button updates target directory and recalculates available space
        browseButton.Click += async (s, args) => 
        {
            var currentPath = targetDirectoryTextBox.Text.Trim();
            var startingDirectory = !string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath) 
                ? currentPath 
                : null;

            var selectedPath = _folderPickerService.BrowseForFolder(startingDirectory, "Select Target Directory");
            
            if (!string.IsNullOrEmpty(selectedPath))
            {
                targetDirectoryTextBox.Text = selectedPath;
                
                // Update available space for new target directory
                await UpdateAvailableSpaceAsync(selectedPath, availableSpaceTextBlock);
            }
        };

        var linkToCategoryCheckBox = new CheckBox
        {
            Content = "Link zip file to parent category",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Password checkbox (only show if category has password protection)
        CheckBox? usePasswordCheckBox = null;
        if (categoryHasPassword)
        {
            usePasswordCheckBox = new CheckBox
            {
                Content = "🔒 Password protect zip with category password",
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        var stackPanel = new StackPanel();
        
        // Description at top
        stackPanel.Children.Add(new TextBlock
        {
            Text = "This will create a zip archive of the folder and optionally add it as a link in the parent category.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 16)
        });
        
        // Source directories section (if multiple)
        if (sourceFolderPaths.Length > 0)
        {
            stackPanel.Children.Add(DialogHelpers.CreateLabel("Source Folders:", 
                new Thickness(0, 0, 0, 4)));
            
            var sourcePanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            
            foreach (var path in sourceFolderPaths)
            {
                var pathTextBlock = new TextBlock
                {
                    Text = $"📁 {path}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(16, 0, 0, 4)
                };
                sourcePanel.Children.Add(pathTextBlock);
            }
            
            stackPanel.Children.Add(sourcePanel);
        }
        
        // Statistics section
        stackPanel.Children.Add(statsTextBlock);
        
        // Zip file name
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Zip File Name: *", 
            new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(zipFileNameTextBox);
        
        // Target directory
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Target Directory: *", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(targetDirectoryTextBox);
        stackPanel.Children.Add(browseButton);
        
        // Available space below target directory
        stackPanel.Children.Add(availableSpaceTextBlock);
        
        // Link to category checkbox
        stackPanel.Children.Add(linkToCategoryCheckBox);

        // Password checkbox (if available)
        if (usePasswordCheckBox != null)
        {
            stackPanel.Children.Add(usePasswordCheckBox);
        }

        return (stackPanel, zipFileNameTextBox, targetDirectoryTextBox, statsTextBlock, availableSpaceTextBlock, linkToCategoryCheckBox, usePasswordCheckBox);
    }

    /// <summary>
    /// Calculates and updates the folder statistics for multiple source folders.
    /// </summary>
    private async Task UpdateFolderStatisticsAsync(string[] folderPaths, TextBlock statsTextBlock)
    {
        try
        {
            // Show calculating message on UI thread
            statsTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                statsTextBlock.Text = "📊 Calculating folder statistics...";
            });

            // Calculate on background thread
            var stats = await Task.Run(() => CalculateMultipleFoldersStatistics(folderPaths));
            
            // Update UI on UI thread
            statsTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                if (stats.FolderCount == 0)
                {
                    statsTextBlock.Text = "📊 Source folder statistics unavailable";
                }
                else
                {
                    statsTextBlock.Text = $"📊 Source Folder Statistics:\n" +
                                         $"   • Folders to zip: {stats.FolderCount:N0}\n" +
                                         $"   • Subdirectories: {stats.SubdirectoryCount:N0}\n" +
                                         $"   • Files: {stats.FileCount:N0}\n" +
                                         $"   • Total Size: {FileUtilities.FormatFileSize(stats.TotalSize)}";
                }
            });
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipDialogBuilder.UpdateFolderStatisticsAsync", "Error calculating folder statistics", ex);
            statsTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                statsTextBlock.Text = "📊 Error calculating folder statistics";
            });
        }
    }

    /// <summary>
    /// Calculates and updates the available space for the target directory.
    /// </summary>
    private async Task UpdateAvailableSpaceAsync(string targetDirectory, TextBlock availableSpaceTextBlock)
    {
        try
        {
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                availableSpaceTextBlock.DispatcherQueue.TryEnqueue(() =>
                {
                    availableSpaceTextBlock.Text = "💾 Available space unavailable";
                });
                return;
            }

            // Show calculating message
            availableSpaceTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                availableSpaceTextBlock.Text = "💾 Calculating available space...";
            });

            // Calculate on background thread
            var availableSpace = await Task.Run(() => GetAvailableSpace(targetDirectory));
            
            // Update UI on UI thread
            availableSpaceTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                availableSpaceTextBlock.Text = $"💾 Available Space on Target Drive:\n" +
                                               $"   • Free Space: {FileUtilities.FormatFileSize(availableSpace)}";
            });
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipDialogBuilder.UpdateAvailableSpaceAsync", "Error calculating available space", ex);
            availableSpaceTextBlock.DispatcherQueue.TryEnqueue(() =>
            {
                availableSpaceTextBlock.Text = "💾 Error calculating available space";
            });
        }
    }

    /// <summary>
    /// Gets the available free space on the drive containing the target directory.
    /// </summary>
    private ulong GetAvailableSpace(string targetDirectory)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(targetDirectory) ?? targetDirectory);
            return (ulong)driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipDialogBuilder.GetAvailableSpace", "Error getting drive info", ex);
            return 0;
        }
    }

    /// <summary>
    /// Calculates statistics for multiple folders.
    /// </summary>
    private (int FolderCount, int SubdirectoryCount, int FileCount, ulong TotalSize) CalculateMultipleFoldersStatistics(string[] folderPaths)
    {
        int folderCount = 0;
        int subdirectoryCount = 0;
        int fileCount = 0;
        ulong totalSize = 0;

        foreach (var folderPath in folderPaths)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                continue;

            folderCount++;
            var stats = CalculateFolderStatistics(folderPath);
            subdirectoryCount += stats.SubdirectoryCount;
            fileCount += stats.FileCount;
            totalSize += stats.TotalSize;
        }

        return (folderCount, subdirectoryCount, fileCount, totalSize);
    }

    /// <summary>
    /// Calculates folder statistics recursively.
    /// </summary>
    private (int SubdirectoryCount, int FileCount, ulong TotalSize) CalculateFolderStatistics(string folderPath)
    {
        int subdirectoryCount = 0;
        int fileCount = 0;
        ulong totalSize = 0;

        try
        {
            // Get all subdirectories recursively
            var directories = Directory.GetDirectories(folderPath, "*", SearchOption.AllDirectories);
            subdirectoryCount = directories.Length;

            // Get all files recursively
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            fileCount = files.Length;

            // Calculate total size
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += (ulong)fileInfo.Length;
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            LogUtilities.LogWarning("ZipDialogBuilder.CalculateFolderStatistics", $"Access denied to some folders in: {folderPath}");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipDialogBuilder.CalculateFolderStatistics", "Error during statistics calculation", ex);
        }

        return (subdirectoryCount, fileCount, totalSize);
    }

    private async Task<ZipFolderResult?> CreateZipResult(
        TextBox zipFileNameTextBox, 
        TextBox targetDirectoryTextBox,
        CheckBox? linkToCategoryCheckBox,
        CheckBox? usePasswordCheckBox,
        string? categoryPassword)
    {
        string zipFileName = zipFileNameTextBox.Text.Trim();
        string targetDirectory = targetDirectoryTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(zipFileName) || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return null;
        }

        if (!Directory.Exists(targetDirectory))
        {
            await DialogHelpers.ShowErrorAsync(_xamlRoot,
                "Invalid Directory",
                "The target directory does not exist. Please select a valid directory.");
            return null;
        }

        bool usePassword = usePasswordCheckBox?.IsChecked == true;

        return new ZipFolderResult
        {
            ZipFileName = zipFileName,
            TargetDirectory = targetDirectory,
            LinkToCategory = linkToCategoryCheckBox?.IsChecked == true,
            UsePassword = usePassword,
            Password = usePassword ? categoryPassword : null
        };
    }
}