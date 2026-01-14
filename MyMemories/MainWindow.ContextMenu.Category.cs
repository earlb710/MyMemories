using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Category context menu event handlers.
/// </summary>
public sealed partial class MainWindow
{
    // ========================================
    // CATEGORY MENU EVENT HANDLERS
    // ========================================

    private async void CategoryMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagName)
            return;

        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Add the tag name to the category (check for duplicates case-insensitively)
        if (!category.Tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase)))
        {
            category.Tags.Add(tagName);
            category.ModifiedDate = DateTime.Now;
            category.NotifyTagsChanged();

            // Refresh the node to update display
            var updatedNode = _treeViewService!.RefreshCategoryNode(_contextMenuNode, category);
            _contextMenuNode = updatedNode;

            // Save
            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Added tag '{tagName}' to category '{category.Name}'";
        }
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
                    Keywords = result.Keywords,
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
            
            // Audit log the link addition
            if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkChangeAsync(
                    rootCategory.Name,
                    "added",
                    result.Title,
                    result.Url);
            }
            
            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";

            if (LinksTreeView.SelectedNode == result.CategoryNode)
            {
                await _detailsViewService!.ShowCategoryDetailsAsync((CategoryItem)result.CategoryNode.Content, result.CategoryNode);
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

    private async void CategoryMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Check if this node is a root category
        bool isRootCategory = LinksTreeView.RootNodes.Contains(_contextMenuNode);
        
        if (!isRootCategory)
        {
            await ShowErrorDialogAsync(
                "Not a Root Category",
                "Password protection can only be changed for root categories.\n\nSubcategories inherit their parent's password protection.");
            return;
        }

        await ShowChangePasswordDialogAsync(category, _contextMenuNode);
    }

    private async void CategoryMenu_BackupDirectories_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Check if this node is a root category
        bool isRootCategory = LinksTreeView.RootNodes.Contains(_contextMenuNode);
        
        if (!isRootCategory)
        {
            await ShowErrorDialogAsync(
                "Not a Root Category",
                "Backup directories can only be configured for root categories.\n\nSubcategories are saved with their parent category.");
            return;
        }

        // Show the backup directory dialog with category file path for manual backup
        var dialog = new Dialogs.BackupDirectoryDialog(Content.XamlRoot, _folderPickerService!);
        
        // Set the category file path for backup operations
        var sanitizedName = Utilities.FileUtilities.SanitizeFileName(category.Name);
        var dataFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyMemories", "Data");
        var categoryFilePath = System.IO.Path.Combine(dataFolder, sanitizedName + ".json");
        if (!System.IO.File.Exists(categoryFilePath))
        {
            // Try encrypted version
            categoryFilePath = System.IO.Path.Combine(dataFolder, sanitizedName + ".zip.json");
        }
        dialog.SetCategoryFilePath(categoryFilePath);
        
        var result = await dialog.ShowAsync(category.Name, category.BackupDirectories);

        if (result != null)
        {
            category.BackupDirectories = result;
            category.ModifiedDate = DateTime.Now;
            await _categoryService!.SaveCategoryAsync(_contextMenuNode);

            var count = result.Count;
            StatusText.Text = count > 0 
                ? $"Configured {count} backup directory(s) for '{category.Name}'"
                : $"Removed backup directories for '{category.Name}'";
        }
    }

    private async void CategoryMenu_ZipCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // OPTIMIZED: Use cached service instance
        var folderPaths = _statisticsService.CollectFolderPathsFromCategory(_contextMenuNode);

        if (folderPaths.Count == 0)
        {
            await ShowErrorDialogAsync(
                "No Folders to Zip",
                "This category does not contain any folder links to zip.");
            return;
        }

        // Get default target directory
        var defaultTargetDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (folderPaths.Count > 0 && Directory.Exists(folderPaths[0]))
        {
            var firstFolder = new DirectoryInfo(folderPaths[0]);
            defaultTargetDirectory = firstFolder.Parent?.FullName ?? defaultTargetDirectory;
        }

        // Get the ROOT category for password protection
        var rootCategoryNode = GetRootCategoryNode(_contextMenuNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;
        
        bool categoryHasPassword = rootCategory?.PasswordProtection != PasswordProtectionType.None;
        string? categoryPassword = null;

        if (categoryHasPassword && rootCategory != null)
        {
            categoryPassword = await GetCategoryPasswordAsync(rootCategory);
            if (categoryPassword == null)
                return;
        }

        // Show zip dialog
        var result = await _linkDialog!.ShowZipFolderDialogAsync(
            category.Name,
            defaultTargetDirectory,
            folderPaths.ToArray(),
            categoryHasPassword,
            categoryPassword,
            (zipTitle) => {
                if (LinkExistsInCategory(_contextMenuNode, zipTitle))
                {
                    var categoryPath = _treeViewService!.GetCategoryPath(_contextMenuNode);
                    return (false, $"A link named '{zipTitle}' already exists in '{categoryPath}'. Please choose a different name.");
                }
                return (true, null);
            },
            _contextMenuNode
        );

        if (result == null)
            return;

        // Build full zip file path
        var zipFileName = result.ZipFileName;
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            zipFileName += ".zip";

        var zipFilePath = Path.Combine(result.TargetDirectory, zipFileName);

        // Check if zip file already exists
        if (File.Exists(zipFilePath))
        {
            bool shouldOverwrite = await ShowConfirmAsync(
                "File Already Exists",
                $"The file '{zipFileName}' already exists in the target directory. Do you want to overwrite it?",
                "Overwrite",
                "Cancel");

            if (!shouldOverwrite)
                return;

            // Delete existing file
            try
            {
                File.Delete(zipFilePath);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error Deleting File", $"Could not delete existing file: {ex.Message}");
                return;
            }
        }

        // Create temporary zip node with "busy creating" indicator
        TreeViewNode? zipLinkNode = null;
        TreeViewNode? busyNode = null;

        if (result.LinkToCategory)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(_contextMenuNode);

            var zipLinkItem = new LinkItem
            {
                Title = Path.GetFileNameWithoutExtension(zipFileName),
                Url = zipFilePath,
                Description = "Creating zip archive...",
                IsDirectory = true,
                CategoryPath = categoryPath,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                FolderType = FolderLinkType.CatalogueFiles
            };

            zipLinkNode = new TreeViewNode { Content = zipLinkItem };

            var busyLinkItem = new LinkItem
            {
                Title = "? Busy creating...",
                Url = string.Empty,
                Description = "Zip archive is being created",
                IsDirectory = false,
                CategoryPath = categoryPath,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };

            busyNode = new TreeViewNode { Content = busyLinkItem };
            zipLinkNode.Children.Add(busyNode);
            zipLinkNode.IsExpanded = true;

            _contextMenuNode.Children.Add(zipLinkNode);
            _contextMenuNode.IsExpanded = true;
            LinksTreeView.SelectedNode = zipLinkNode;
        }

        // Create zip file
        try
        {
            StatusText.Text = $"Creating{(result.UsePassword ? " password-protected" : "")} zip file '{zipFileName}' from {folderPaths.Count} folder(s)...";

            var startTime = DateTime.Now;

            // Collect folder info on UI thread
            var folderInfoList = _archiveRefreshService!.CollectFolderInfoFromCategory(_contextMenuNode, category.Name);
            var manifestContent = _archiveRefreshService.GenerateManifestContent(folderInfoList, category.Name);

            // Calculate original size
            ulong originalSize = await Task.Run(() =>
            {
                ulong totalSize = 0;
                foreach (var folderPath in folderPaths)
                {
                    if (!Directory.Exists(folderPath))
                        continue;

                    var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            totalSize += (ulong)new FileInfo(file).Length;
                        }
                        catch { }
                    }
                }
                return totalSize;
            });

            if (result.UsePassword && !string.IsNullOrEmpty(result.Password))
            {
                // Password-protected zip creation
                await Task.Run(() =>
                {
                    using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                    using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                    zipStream.SetLevel(6);
                    zipStream.Password = result.Password;
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;

                    // Add manifest
                    var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestContent);
                    var manifestEntry = new ICSharpCode.SharpZipLib.Zip.ZipEntry("_MANIFEST.txt")
                    {
                        DateTime = DateTime.Now,
                        Size = manifestBytes.Length,
                        AESKeySize = 256
                    };

                    zipStream.PutNextEntry(manifestEntry);
                    zipStream.Write(manifestBytes, 0, manifestBytes.Length);
                    zipStream.CloseEntry();

                    // Add folder contents
                    foreach (var folderPath in folderPaths)
                    {
                        if (!Directory.Exists(folderPath))
                            continue;

                        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                        var folderName = new DirectoryInfo(folderPath).Name;

                        foreach (var filePath in files)
                        {
                            try
                            {
                                var relativePath = Path.GetRelativePath(folderPath, filePath);
                                var entryName = Path.Combine(folderName, relativePath).Replace(Path.DirectorySeparatorChar, '/');

                                var fileInfo = new FileInfo(filePath);
                                var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(entryName)
                                {
                                    DateTime = fileInfo.LastWriteTime,
                                    Size = fileInfo.Length,
                                    AESKeySize = 256
                                };

                                zipStream.PutNextEntry(entry);

                                using var fileStream = File.OpenRead(filePath);
                                fileStream.CopyTo(zipStream);

                                zipStream.CloseEntry();
                            }
                            catch (Exception ex)
                            {
                                LogUtilities.LogError("MainWindow.CategoryMenu_ZipCategory_Click", $"Error adding file {filePath}", ex);
                            }
                        }
                    }

                    zipStream.Finish();
                });
            }
            else
            {
                // Standard zip creation
                await Task.Run(() =>
                {
                    using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
                    
                    // Add manifest
                    var manifestEntry = archive.CreateEntry("_MANIFEST.txt", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(manifestEntry.Open(), System.Text.Encoding.UTF8))
                    {
                        writer.Write(manifestContent);
                    }

                    // Add folder contents
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

            StatusText.Text = $"Successfully created '{zipFileName}'";

            // Calculate statistics
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            var compressedSize = (ulong)new FileInfo(zipFilePath).Length;
            var compressionRatio = originalSize > 0 ? (1.0 - ((double)compressedSize / originalSize)) * 100.0 : 0.0;

            // Update zip node
            if (result.LinkToCategory && zipLinkNode != null)
            {
                if (busyNode != null)
                    zipLinkNode.Children.Remove(busyNode);

                var finalZipLinkItem = zipLinkNode.Content as LinkItem;
                if (finalZipLinkItem != null)
                {
                    finalZipLinkItem.Description = $"Zip archive of folders from '{category.Name}'"
                        + (result.UsePassword ? " (password-protected)" : "");
                    finalZipLinkItem.FileSize = compressedSize;
                    finalZipLinkItem.LastCatalogUpdate = DateTime.Now;
                    finalZipLinkItem.IsZipPasswordProtected = result.UsePassword;
                }

                await _catalogService!.CreateCatalogAsync(finalZipLinkItem!, zipLinkNode);

                var rootNode = GetRootCategoryNode(_contextMenuNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                var categoryPath = _treeViewService!.GetCategoryPath(_contextMenuNode);
                StatusText.Text = $"Created '{zipFileName}' and linked to '{categoryPath}'";
            }

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The category folders have been successfully zipped to:\n\n{zipFilePath}\n\n" +
                         $"?? Statistics:\n" +
                         $"   • Original Size: {FileViewerService.FormatFileSize(originalSize)}\n" +
                         $"   • Compressed Size: {FileViewerService.FormatFileSize(compressedSize)}\n" +
                         $"   • Compression: {compressionRatio:F1}% reduction\n" +
                         $"   • Time Taken: {duration.TotalSeconds:F1} seconds" +
                         (result.UsePassword ? "\n\n?? Zip file is password-protected" : ""),
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error creating zip file: {ex.Message}";

            if (zipLinkNode != null)
                _contextMenuNode.Children.Remove(zipLinkNode);

            await ShowErrorDialogAsync("Error Creating Zip File", $"An error occurred while creating the zip file:\n\n{ex.Message}");
        }
    }

    private async void CategoryMenu_Stats_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // OPTIMIZED: Use cached service instance
        var folderPaths = _statisticsService.CollectFolderPathsFromCategory(_contextMenuNode);

        StatusText.Text = "Calculating category statistics...";

        var stats = await Task.Run(() => _statisticsService.CalculateMultipleFoldersStatistics(folderPaths.ToArray()));

        // Build statistics UI
        var stackPanel = new StackPanel { Spacing = 16 };

        stackPanel.Children.Add(new TextBlock
        {
            Text = $"{category.Icon} {category.Name}",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

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

        if (folderPaths.Count > 0)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Folders in Category:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var foldersPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            foreach (var path in folderPaths)
            {
                foldersPanel.Children.Add(new TextBlock
                {
                    Text = $"?? {path}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(16, 0, 0, 4)
                });
            }

            stackPanel.Children.Add(foldersPanel);
        }

        var statsTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 16)
        };

        if (stats.FolderCount == 0)
        {
            statsTextBlock.Text = "?? No folder links in this category";
        }
        else
        {
            statsTextBlock.Text = $"?? Category Statistics:\n" +
                                 $"   • Folders: {stats.FolderCount:N0}\n" +
                                 $"   • Subdirectories: {stats.SubdirectoryCount:N0}\n" +
                                 $"   • Files: {stats.FileCount:N0}\n" +
                                 $"   • Total Size: {FileUtilities.FormatFileSize(stats.TotalSize)}";
        }

        stackPanel.Children.Add(statsTextBlock);

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

    private async void CategoryMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await DeleteCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void CategoryMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (category.Tags.Count == 0)
        {
            StatusText.Text = "This category has no tags to remove";
            return;
        }

        var tagService = TagManagementService.Instance;
        if (tagService == null)
            return;

        var tagsInfo = tagService.GetTagsInfo(category.Tags);
        if (tagsInfo.Count == 0)
        {
            StatusText.Text = "No valid tags found";
            return;
        }

        var tagComboBox = new ComboBox
        {
            PlaceholderText = "Select tag to remove",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        foreach (var (name, _) in tagsInfo)
        {
            tagComboBox.Items.Add(name);
        }
        tagComboBox.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            Title = "Remove Tag",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = $"Select tag to remove from '{category.Name}':" },
                    tagComboBox
                }
            },
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && tagComboBox.SelectedItem is string selectedTagName)
        {
            var tagToRemove = category.Tags.FirstOrDefault(t => 
                string.Equals(t, selectedTagName, StringComparison.OrdinalIgnoreCase));
            
            if (tagToRemove != null)
            {
                category.Tags.Remove(tagToRemove);
                category.ModifiedDate = DateTime.Now;
                category.NotifyTagsChanged();

                var updatedNode = _treeViewService!.RefreshCategoryNode(_contextMenuNode, category);
                _contextMenuNode = updatedNode;

                var rootNode = GetRootCategoryNode(updatedNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                StatusText.Text = $"Removed tag '{selectedTagName}' from category '{category.Name}'";
            }
        }
    }

    private async void CategoryMenu_SortBy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        await ShowSortDialogAsync(_contextMenuNode, category.SortOrder, isCategory: true);
    }

    private async void CategoryMenu_RatingTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string templateName)
            return;

        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        var originalTemplate = _ratingService.CurrentTemplateName;
        _ratingService.SwitchTemplate(templateName);

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(category.Name, category.Ratings);

        _ratingService.SwitchTemplate(originalTemplate);

        if (result != null)
        {
            category.Ratings = result;
            category.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for '{category.Name}' using template '{displayName}'"
                : $"Removed all ratings from '{category.Name}'";
        }
    }

    private async void CategoryMenu_Ratings_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(category.Name, category.Ratings);

        if (result != null)
        {
            category.Ratings = result;
            category.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for '{category.Name}'"
                : $"Removed all ratings from '{category.Name}'";
        }
    }
}
