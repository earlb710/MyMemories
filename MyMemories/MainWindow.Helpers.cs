using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Creates a visual element for a tree node with icon and optional badge.
    /// </summary>
    private FrameworkElement CreateNodeContent(object content)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // For LinkItem, show icon with potential badge
        if (content is LinkItem linkItem)
        {
            // Create icon container with badge overlay
            var iconGrid = new Grid
            {
                Width = 20,
                Height = 20
            };

            // Primary icon (emoji)
            var primaryIcon = new TextBlock
            {
                Text = linkItem.GetIcon(),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconGrid.Children.Add(primaryIcon);

            // Add link badge for LinkOnly folders
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry && linkItem.FolderType == FolderLinkType.LinkOnly)
            {
                var linkBadge = new FontIcon
                {
                    Glyph = "\uE71B", // Link icon
                    FontSize = 8,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2)
                };
                
                linkBadge.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                iconGrid.Children.Add(linkBadge);
            }

            // Check if folder has changed and add warning badge
            if (linkItem.IsDirectory && 
                !linkItem.IsCatalogEntry && 
                linkItem.LastCatalogUpdate.HasValue &&
                Directory.Exists(linkItem.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(linkItem.Url);
                    if (dirInfo.LastWriteTime > linkItem.LastCatalogUpdate.Value)
                    {
                        // Add warning badge icon
                        var badgeIcon = new FontIcon
                        {
                            Glyph = "\uE7BA", // Warning icon
                            FontSize = 11,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(0, 0, -1, -1)
                        };

                        // Set badge color to bright red
                        badgeIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);

                        // Add tooltip
                        ToolTipService.SetToolTip(badgeIcon, "Folder has changed since last catalog");

                        iconGrid.Children.Add(badgeIcon);
                    }
                }
                catch
                {
                    // Ignore errors accessing directory
                }
            }

            stackPanel.Children.Add(iconGrid);

            // Add text with file count if applicable
            var displayText = linkItem.Title;
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry && linkItem.CatalogFileCount > 0)
            {
                displayText = $"{linkItem.Title} ({linkItem.CatalogFileCount} file{(linkItem.CatalogFileCount != 1 ? "s" : "")})";
            }

            var textBlock = new TextBlock
            {
                Text = displayText,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }
        // For CategoryItem, just show icon and name
        else if (content is CategoryItem categoryItem)
        {
            var iconText = new TextBlock
            {
                Text = categoryItem.Icon,
                FontSize = 16
            };
            stackPanel.Children.Add(iconText);

            var textBlock = new TextBlock
            {
                Text = categoryItem.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }

        return stackPanel;
    }

    /// <summary>
    /// Refreshes the visual content of a tree node.
    /// </summary>
    public void RefreshNodeVisual(TreeViewNode node)
    {
        if (node.Content != null)
        {
            // Force update by recreating the visual
            var content = node.Content;
            node.Content = null;
            node.Content = content;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);

        if (parentObject == null)
            return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
    }

    /// <summary>
    /// Updates the ModifiedDate of all parent categories up to the root.
    /// Call this whenever a link is added, removed, edited, or catalog is refreshed.
    /// </summary>
    private void UpdateParentCategoriesModifiedDate(TreeViewNode node)
    {
        var now = DateTime.Now;
        var current = node.Parent;

        while (current != null)
        {
            if (current.Content is CategoryItem category)
            {
                category.ModifiedDate = now;
            }
            current = current.Parent;
        }
    }

    /// <summary>
    /// Updates the ModifiedDate of all parent categories and saves the root category.
    /// </summary>
    private async Task UpdateParentCategoriesAndSaveAsync(TreeViewNode node)
    {
        // Update all parent categories' ModifiedDate
        UpdateParentCategoriesModifiedDate(node);

        // Save the root category
        var rootNode = GetRootCategoryNode(node);
        await _categoryService!.SaveCategoryAsync(rootNode);
    }

    /// <summary>
    /// Checks if a zip file contains a manifest and extracts the root category name.
    /// </summary>
    private async Task<string?> GetManifestRootCategoryAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipFilePath);
                var manifestEntry = archive.GetEntry("_MANIFEST.txt");
                
                if (manifestEntry == null)
                    return null;

                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                // Parse the manifest to find "Root Category: [name]"
                var match = Regex.Match(content, @"Root Category:\s*(.+)", RegexOptions.Multiline);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }

                return null;
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Refreshes (re-creates) a zip archive from its manifest category.
    /// </summary>
    private async Task RefreshArchiveFromManifestAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        try
        {
            // Get the root category from the manifest
            var rootCategoryName = await GetManifestRootCategoryAsync(zipLinkItem.Url);
            
            if (string.IsNullOrEmpty(rootCategoryName))
            {
                await ShowErrorDialogAsync(
                    "No Manifest Found",
                    "This zip file does not contain a manifest (_MANIFEST.txt) or the manifest could not be parsed."
                );
                return;
            }

            // Find the root category node
            var rootCategoryNode = LinksTreeView.RootNodes
                .FirstOrDefault(n => n.Content is CategoryItem cat && cat.Name == rootCategoryName);

            if (rootCategoryNode == null)
            {
                await ShowErrorDialogAsync(
                    "Category Not Found",
                    $"The root category '{rootCategoryName}' specified in the manifest was not found in the tree."
                );
                return;
            }

            // Confirm with user
            var confirmDialog = new ContentDialog
            {
                Title = "Refresh Archive",
                Content = $"This will re-create the zip archive from the current state of the category:\n\n" +
                         $"📦 {rootCategoryName}\n\n" +
                         $"The existing zip file will be overwritten with a fresh archive containing all current folders in the category.\n\n" +
                         $"Do you want to continue?",
                PrimaryButtonText = "Refresh Archive",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            // Get the zip file info
            var zipFileInfo = new FileInfo(zipLinkItem.Url);
            var zipFileName = zipFileInfo.Name;
            var targetDirectory = zipFileInfo.DirectoryName ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Call the category zipping method
            StatusText.Text = $"Refreshing archive '{zipFileName}'...";

            // Re-zip the category (this will overwrite the existing zip)
            await ReZipCategoryAsync(rootCategoryNode, zipFileName, targetDirectory);

            // Re-catalog the updated zip
            _categoryService!.RemoveCatalogEntries(zipLinkNode);
            await CatalogZipFileAsync(zipLinkItem, zipLinkNode);

            // Update the zip link item
            zipLinkItem.LastCatalogUpdate = DateTime.Now;
            zipLinkItem.FileSize = (ulong)new FileInfo(zipLinkItem.Url).Length;
            _categoryService.UpdateCatalogFileCount(zipLinkNode);

            var refreshedNode = _treeViewService!.RefreshLinkNode(zipLinkNode, zipLinkItem);

            // Save the category
            var parentCategory = refreshedNode.Parent;
            if (parentCategory != null)
            {
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
            }

            StatusText.Text = $"Successfully refreshed archive '{zipFileName}'";

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Archive Refreshed",
                Content = $"The zip archive has been successfully refreshed from the current state of category '{rootCategoryName}'.\n\n" +
                         $"Location: {zipLinkItem.Url}\n" +
                         $"Size: {FormatFileSize(zipLinkItem.FileSize ?? 0)}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error refreshing archive: {ex.Message}";

            var errorDialog = new ContentDialog
            {
                Title = "Error Refreshing Archive",
                Content = $"An error occurred while refreshing the zip archive:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    /// <summary>
    /// Re-creates a zip file from a category (used by refresh archive).
    /// </summary>
    private async Task ReZipCategoryAsync(TreeViewNode categoryNode, string zipFileName, string targetDirectory)
    {
        if (categoryNode.Content is not CategoryItem category)
            return;

        // Collect all folder links from the category
        var folderInfoList = CollectFolderInfoFromCategory(categoryNode, category.Name);
        var folderPaths = folderInfoList.Select(f => f.FolderPath).ToArray();

        if (folderPaths.Length == 0)
        {
            throw new InvalidOperationException("No folders found in category to zip.");
        }

        // Build full zip file path
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        var zipFilePath = Path.Combine(targetDirectory, zipFileName);

        // Delete existing file
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        // Create new zip with manifest
        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
            
            // Create and add the manifest file
            var manifestContent = GenerateManifestContent(folderInfoList, category.Name);
            var manifestEntry = archive.CreateEntry("_MANIFEST.txt", CompressionLevel.Optimal);
            using (var writer = new System.IO.StreamWriter(manifestEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(manifestContent);
            }

            // Add all folder contents
            foreach (var folderPath in folderPaths)
            {
                if (!Directory.Exists(folderPath))
                    continue;

                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                var folderName = new DirectoryInfo(folderPath).Name;

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(folderPath, file);
                    var entryName = Path.Combine(folderName, relativePath);
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        });
    }
}