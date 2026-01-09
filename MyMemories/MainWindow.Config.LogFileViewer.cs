using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
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
                Text = $"?? {fileName}",
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
