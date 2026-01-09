using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.System;

namespace MyMemories;

public sealed partial class MainWindow
{
    private FolderPickerService? _folderPickerService;

    // Menu click handlers - delegate to extracted methods in partial classes
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

    // Helper methods
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
}