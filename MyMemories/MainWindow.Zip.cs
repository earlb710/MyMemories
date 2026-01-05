using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Zips a folder and optionally links it to the parent category.
    /// </summary>
    private async Task ZipFolderAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        if (!Directory.Exists(linkItem.Url))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Folder Not Found",
                Content = $"The folder '{linkItem.Url}' does not exist or is not accessible.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Get parent directory for default target location
        var folderInfo = new DirectoryInfo(linkItem.Url);
        var parentDirectory = folderInfo.Parent?.FullName ?? folderInfo.Root.FullName;

        // Show zip configuration dialog
        var result = await _linkDialog!.ShowZipFolderDialogAsync(
            linkItem.Title,
            parentDirectory
        );

        if (result == null)
        {
            return; // User cancelled
        }

        // Build full zip file path
        var zipFileName = result.ZipFileName;
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        var zipFilePath = Path.Combine(result.TargetDirectory, zipFileName);

        // Check if zip file already exists
        if (File.Exists(zipFilePath))
        {
            var confirmDialog = new ContentDialog
            {
                Title = "File Already Exists",
                Content = $"The file '{zipFileName}' already exists in the target directory. Do you want to overwrite it?",
                PrimaryButtonText = "Overwrite",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var confirmResult = await confirmDialog.ShowAsync();
            if (confirmResult != ContentDialogResult.Primary)
            {
                return; // User cancelled overwrite
            }

            // Delete existing file
            try
            {
                File.Delete(zipFilePath);
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error Deleting File",
                    Content = $"Could not delete existing file: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
        }

        // Create zip file
        try
        {
            StatusText.Text = $"Creating zip file '{zipFileName}'...";

            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(linkItem.Url, zipFilePath, CompressionLevel.Optimal, false);
            });

            StatusText.Text = $"Successfully created '{zipFileName}'";

            // If user wants to link the zip to the parent category
            if (result.LinkToCategory && linkNode.Parent != null)
            {
                var parentCategoryNode = _treeViewService!.GetParentCategoryNode(linkNode);
                if (parentCategoryNode != null)
                {
                    var categoryPath = _treeViewService.GetCategoryPath(parentCategoryNode);

                    // Create a new link for the zip file
                    var zipLinkNode = new TreeViewNode
                    {
                        Content = new LinkItem
                        {
                            Title = Path.GetFileNameWithoutExtension(zipFileName),
                            Url = zipFilePath,
                            Description = $"Zip archive of '{linkItem.Title}'",
                            IsDirectory = false,
                            CategoryPath = categoryPath,
                            CreatedDate = DateTime.Now,
                            ModifiedDate = DateTime.Now,
                            FolderType = FolderLinkType.LinkOnly,
                            FileSize = (ulong)new FileInfo(zipFilePath).Length
                        }
                    };

                    // Add the zip link to the parent category
                    parentCategoryNode.Children.Add(zipLinkNode);

                    // Save the updated category
                    var rootNode = GetRootCategoryNode(parentCategoryNode);
                    await _categoryService!.SaveCategoryAsync(rootNode);

                    StatusText.Text = $"Created '{zipFileName}' and linked to '{categoryPath}'";
                }
            }

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The folder has been successfully zipped to:\n\n{zipFilePath}\n\nSize: {FormatFileSize((ulong)new FileInfo(zipFilePath).Length)}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error creating zip file: {ex.Message}";

            var errorDialog = new ContentDialog
            {
                Title = "Error Creating Zip File",
                Content = $"An error occurred while creating the zip file:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Shows the zip folder dialog.
    /// </summary>
    public async Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory)
    {
        // Create input fields
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

        var linkToCategoryCheckBox = new CheckBox
        {
            Content = "Link zip file to parent category",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var infoText = new TextBlock
        {
            Text = "This will create a zip archive of the folder and optionally add it as a link in the parent category.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Create stack panel for dialog content
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(infoText);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Zip File Name: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(zipFileNameTextBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Target Directory: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(targetDirectoryTextBox);
        stackPanel.Children.Add(browseButton);
        stackPanel.Children.Add(linkToCategoryCheckBox);

        // Create and configure the dialog
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

        // Handle browse button click
        browseButton.Click += async (s, args) =>
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
                    dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                                     !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text);
                }
            }
            catch (Exception)
            {
                // Error handled silently
            }
        };

        // Validate form when text changes
        zipFileNameTextBox.TextChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                             !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text);
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string zipFileName = zipFileNameTextBox.Text.Trim();
            string targetDirectory = targetDirectoryTextBox.Text.Trim();
            bool linkToCategory = linkToCategoryCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(zipFileName) || string.IsNullOrWhiteSpace(targetDirectory))
            {
                return null; // Validation failed
            }

            // Validate that target directory exists
            if (!Directory.Exists(targetDirectory))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Invalid Directory",
                    Content = "The target directory does not exist. Please select a valid directory.",
                    CloseButtonText = "OK",
                    XamlRoot = _xamlRoot
                };
                await errorDialog.ShowAsync();
                return null;
            }

            return new ZipFolderResult
            {
                ZipFileName = zipFileName,
                TargetDirectory = targetDirectory,
                LinkToCategory = linkToCategory
            };
        }

        return null;
    }
}