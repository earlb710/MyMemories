using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories.Dialogs;

/// <summary>
/// Builder for zip-related dialogs.
/// </summary>
public class ZipDialogBuilder
{
    private readonly Window _parentWindow;
    private readonly XamlRoot _xamlRoot;

    public ZipDialogBuilder(Window parentWindow, XamlRoot xamlRoot)
    {
        _parentWindow = parentWindow;
        _xamlRoot = xamlRoot;
    }

    public async Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory)
    {
        var (stackPanel, zipFileNameTextBox, targetDirectoryTextBox) = BuildZipDialogUI(folderTitle, defaultTargetDirectory);

        var dialog = new ContentDialog
        {
            Title = "Create Zip Archive",
            Content = stackPanel,
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
                stackPanel.Children[^1] as CheckBox);
        }

        return null;
    }

    private (StackPanel, TextBox, TextBox) BuildZipDialogUI(string folderTitle, string defaultTargetDirectory)
    {
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

        var browseButton = new Button
        {
            Content = "Browse...",
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        browseButton.Click += async (s, args) => 
            await BrowseForTargetDirectory(targetDirectoryTextBox);

        var linkToCategoryCheckBox = new CheckBox
        {
            Content = "Link zip file to parent category",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock
        {
            Text = "This will create a zip archive of the folder and optionally add it as a link in the parent category.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        });
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Zip File Name: *", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(zipFileNameTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Target Directory: *", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(targetDirectoryTextBox);
        stackPanel.Children.Add(browseButton);
        stackPanel.Children.Add(linkToCategoryCheckBox);

        return (stackPanel, zipFileNameTextBox, targetDirectoryTextBox);
    }

    private async Task BrowseForTargetDirectory(TextBox targetDirectoryTextBox)
    {
        try
        {
            var folderPicker = new FolderPicker();
            var hWnd = WindowNative.GetWindowHandle(_parentWindow);
            InitializeWithWindow.Initialize(folderPicker, hWnd);
            folderPicker.FileTypeFilter.Add("*");
            
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                targetDirectoryTextBox.Text = folder.Path;
            }
        }
        catch { }
    }

    private async Task<ZipFolderResult?> CreateZipResult(
        TextBox zipFileNameTextBox, 
        TextBox targetDirectoryTextBox,
        CheckBox? linkToCategoryCheckBox)
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

        return new ZipFolderResult
        {
            ZipFileName = zipFileName,
            TargetDirectory = targetDirectory,
            LinkToCategory = linkToCategoryCheckBox?.IsChecked == true
        };
    }
}