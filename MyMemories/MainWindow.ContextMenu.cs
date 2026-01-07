using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private void LinksTreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        _contextMenuNode = node;

        var menu = node.Content switch
        {
            CategoryItem => LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout,
            LinkItem => LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout,
            _ => null
        };

        menu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
        e.Handled = true;
    }

    private async void CategoryMenu_AddLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode == null) return;

        var categoriesWithSubs = _treeViewService!.GetCategoryWithSubcategories(_contextMenuNode);

        var selectedCategoryNode = new CategoryNode
        {
            Name = _treeViewService.GetCategoryPath(_contextMenuNode),
            Node = _contextMenuNode
        };

        var result = await _linkDialog!.ShowAddAsync(categoriesWithSubs, selectedCategoryNode);

        if (result?.CategoryNode != null)
        {
            var categoryPath = _treeViewService.GetCategoryPath(result.CategoryNode);

            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = result.Title,
                    Url = result.Url,
                    Description = result.Description,
                    IsDirectory = result.IsDirectory,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    FolderType = result.FolderType,
                    FileFilters = result.FileFilters
                }
            };

            result.CategoryNode.Children.Add(linkNode);
            result.CategoryNode.IsExpanded = true;

            var rootNode = GetRootCategoryNode(result.CategoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";

            if (LinksTreeView.SelectedNode == result.CategoryNode)
            {
                _detailsViewService!.ShowCategoryDetails((CategoryItem)result.CategoryNode.Content, result.CategoryNode);
            }
        }
    }

    private async void CategoryMenu_AddSubCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem)
        {
            await CreateSubCategoryAsync(_contextMenuNode);
        }
    }

    private async void CategoryMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await EditCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void CategoryMenu_ZipCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Collect all folder links (not catalog entries) from the category recursively
        var folderPaths = CollectFolderPathsFromCategory(_contextMenuNode);

        if (folderPaths.Count == 0)
        {
            var errorDialog = new ContentDialog
            {
                Title = "No Folders to Zip",
                Content = "This category does not contain any folder links to zip.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Get default target directory (parent of first folder, or user's documents)
        var defaultTargetDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (folderPaths.Count > 0 && Directory.Exists(folderPaths[0]))
        {
            var firstFolder = new DirectoryInfo(folderPaths[0]);
            defaultTargetDirectory = firstFolder.Parent?.FullName ?? defaultTargetDirectory;
        }

        // Show zip dialog with multiple source folders
        var result = await _linkDialog!.ShowZipFolderDialogAsync(
            category.Name,
            defaultTargetDirectory,
            folderPaths.ToArray()
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

        // Create zip file from all folders
        try
        {
            StatusText.Text = $"Creating zip file '{zipFileName}' from {folderPaths.Count} folder(s)...";

            await Task.Run(() =>
            {
                using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    foreach (var folderPath in folderPaths)
                    {
                        if (!Directory.Exists(folderPath))
                            continue;

                        // Add all files from this folder recursively
                        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                        var folderName = new DirectoryInfo(folderPath).Name;

                        foreach (var file in files)
                        {
                            var relativePath = Path.GetRelativePath(folderPath, file);
                            var entryName = Path.Combine(folderName, relativePath);
                            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                        }
                    }
                }
            });

            StatusText.Text = $"Successfully created '{zipFileName}'";

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The category folders have been successfully zipped to:\n\n{zipFilePath}\n\nSize: {FormatFileSize((ulong)new FileInfo(zipFilePath).Length)}",
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

    private async void CategoryMenu_Stats_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Collect all folder links (not catalog entries) from the category recursively
        var folderPaths = CollectFolderPathsFromCategory(_contextMenuNode);

        // Calculate statistics
        StatusText.Text = "Calculating category statistics...";

        var stats = await Task.Run(() => CalculateMultipleFoldersStatistics(folderPaths.ToArray()));

        // Build statistics UI
        var stackPanel = new StackPanel { Spacing = 16 };

        // Category header
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"{category.Icon} {category.Name}",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Description if available
        if (!string.IsNullOrWhiteSpace(category.Description))
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = category.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 0, 0, 16)
            });
        }

        // Source folders list
        if (folderPaths.Count > 0)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Folders in Category:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var foldersPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 16)
            };

            foreach (var path in folderPaths)
            {
                foldersPanel.Children.Add(new TextBlock
                {
                    Text = $"📁 {path}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(16, 0, 0, 4)
                });
            }

            stackPanel.Children.Add(foldersPanel);
        }

        // Statistics
        var statsTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 16)
        };

        if (stats.FolderCount == 0)
        {
            statsTextBlock.Text = "📊 No folder links in this category";
        }
        else
        {
            statsTextBlock.Text = $"📊 Category Statistics:\n" +
                                 $"   • Folders: {stats.FolderCount:N0}\n" +
                                 $"   • Subdirectories: {stats.SubdirectoryCount:N0}\n" +
                                 $"   • Files: {stats.FileCount:N0}\n" +
                                 $"   • Total Size: {FileUtilities.FormatFileSize(stats.TotalSize)}";
        }

        stackPanel.Children.Add(statsTextBlock);

        // Show dialog
        var dialog = new ContentDialog
        {
            Title = "Category Statistics",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();

        StatusText.Text = "Ready";
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
            LogUtilities.LogWarning("MainWindow.CalculateFolderStatistics", $"Access denied to some folders in: {folderPath}");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.CalculateFolderStatistics", "Error during statistics calculation", ex);
        }

        return (subdirectoryCount, fileCount, totalSize);
    }

    /// <summary>
    /// Recursively collects all folder paths from a category node (excluding catalog entries).
    /// </summary>
    private List<string> CollectFolderPathsFromCategory(TreeViewNode categoryNode)
    {
        var folderPaths = new List<string>();

        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Only include directory links that are not catalog entries
                if (link.IsDirectory && !link.IsCatalogEntry && Directory.Exists(link.Url))
                {
                    folderPaths.Add(link.Url);
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively collect from subcategories
                folderPaths.AddRange(CollectFolderPathsFromCategory(child));
            }
        }

        return folderPaths;
    }

    private async void CategoryMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await DeleteCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void LinkMenu_Move_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            await MoveLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            if (link.IsCatalogEntry)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Edit Catalog Entry",
                    Content = "Catalog entries are read-only and cannot be edited. Use 'Refresh Catalog' to update them.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await EditLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_ZipFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            if (!link.IsDirectory)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Zip File",
                    Content = "Only folders can be zipped. This is a file link.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            if (link.IsCatalogEntry)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Zip Catalog Entry",
                    Content = "Catalog entries cannot be zipped directly. Please zip the parent folder instead.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await ZipFolderAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            if (link.IsCatalogEntry)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Remove Catalog Entry",
                    Content = "Catalog entries cannot be removed individually. Use 'Refresh Catalog' to update them.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await DeleteLinkAsync(link, _contextMenuNode);
        }
    }
}
