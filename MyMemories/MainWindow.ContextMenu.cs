using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Context menu event handlers for categories and links.
/// Uses optimized helpers from MainWindow.ContextMenu.Helpers.cs and MainWindow.ContextMenu.Configuration.cs.
/// </summary>
public sealed partial class MainWindow
{
    // NOTE: LinksTreeView_RightTapped, ConfigureCategoryContextMenu, ConfigureLinkContextMenu,
    // FindMenuItemByName, FindSubMenuItemByName, PopulateAddTagSubmenu, and PopulateRatingsSubmenu
    // are now in MainWindow.ContextMenu.Configuration.cs with optimizations.
    
    // NOTE: Helper methods (FindParentNode, GenerateUniqueTitle, etc.) are now in
    // MainWindow.ContextMenu.Helpers.cs with optimizations.

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

        // Show the backup directory dialog
        var dialog = new Dialogs.BackupDirectoryDialog(Content.XamlRoot, _folderPickerService!);
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
            bool shouldOverwrite = await ShowConfirmDialogAsync(
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
                Title = "⏳ Busy creating...",
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
                         $"📊 Statistics:\n" +
                         $"   • Original Size: {FileViewerService.FormatFileSize(originalSize)}\n" +
                         $"   • Compressed Size: {FileViewerService.FormatFileSize(compressedSize)}\n" +
                         $"   • Compression: {compressionRatio:F1}% reduction\n" +
                         $"   • Time Taken: {duration.TotalSeconds:F1} seconds" +
                         (result.UsePassword ? "\n\n🔒 Zip file is password-protected" : ""),
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
                    Text = $"📁 {path}",
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

    // ========================================
    // LINK MENU EVENT HANDLERS
    // ========================================

    private async void LinkMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagName)
            return;

        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (!link.Tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase)))
        {
            link.Tags.Add(tagName);
            link.ModifiedDate = DateTime.Now;
            link.NotifyTagsChanged();

            var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
            _contextMenuNode = updatedNode;

            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            StatusText.Text = $"Added tag '{tagName}' to {itemType} '{link.Title}'";
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
                await ShowErrorDialogAsync(
                    "Cannot Edit Catalog Entry",
                    "Catalog entries are read-only and cannot be edited. Use 'Refresh Catalog' to update them.");
                return;
            }

            await EditLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.IsCatalogEntry)
        {
            await ShowErrorDialogAsync(
                "Cannot Copy Catalog Entry",
                "Catalog entries are read-only and cannot be copied.");
            return;
        }

        var parentNode = FindParentNode(_contextMenuNode);
        if (parentNode == null)
        {
            StatusText.Text = "Could not find parent category";
            return;
        }

        // OPTIMIZED: Uses HashSet-based unique title generation
        var newTitle = GenerateUniqueTitle(link.Title, parentNode);

        var copiedLink = new LinkItem
        {
            Title = newTitle,
            Url = link.Url,
            Description = link.Description,
            Keywords = link.Keywords,
            IsDirectory = link.IsDirectory,
            CategoryPath = link.CategoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            FolderType = link.FolderType,
            FileFilters = link.FileFilters,
            UrlStatus = link.UrlStatus,
            UrlLastChecked = link.UrlLastChecked,
            UrlStatusMessage = link.UrlStatusMessage
        };

        var copiedNode = new TreeViewNode { Content = copiedLink };

        var insertIndex = parentNode.Children.IndexOf(_contextMenuNode) + 1;
        if (insertIndex > 0 && insertIndex <= parentNode.Children.Count)
        {
            parentNode.Children.Insert(insertIndex, copiedNode);
        }
        else
        {
            parentNode.Children.Add(copiedNode);
        }

        var rootNode = GetRootCategoryNode(parentNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        StatusText.Text = $"Copied link as '{newTitle}'";

        LinksTreeView.SelectedNode = copiedNode;
        _contextMenuNode = copiedNode;

        await EditLinkAsync(copiedLink, copiedNode);
    }

    private async void LinkMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        
        if (!isZipFile)
        {
            await ShowErrorDialogAsync(
                "Not a Zip File",
                "Password protection can only be changed for zip archive files.");
            return;
        }

        await ShowChangeZipPasswordDialogAsync(link, _contextMenuNode);
    }

    private async void LinkMenu_BackupZip_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        
        if (!isZipFile)
        {
            StatusText.Text = "Backup is only available for zip archive files.";
            return;
        }

        var dialog = new Dialogs.BackupDirectoryDialog(Content.XamlRoot, _folderPickerService!);
        var result = await dialog.ShowAsync(link.Title, link.BackupDirectories);

        if (result != null)
        {
            link.BackupDirectories = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var count = result.Count;
            if (count > 0)
            {
                StatusText.Text = $"Configured {count} backup directory(s) for '{link.Title}'";
                
                if (File.Exists(link.Url))
                {
                    bool shouldBackup = await ShowConfirmDialogAsync(
                        "Backup Now?",
                        $"Do you want to backup '{link.Title}' to the configured directories now?",
                        "Backup Now",
                        "Later");

                    if (shouldBackup)
                    {
                        StatusText.Text = $"Backing up '{link.Title}'...";
                        await BackupZipFileAsync(link.Url, link.BackupDirectories, link.Title);
                    }
                }
            }
            else
            {
                StatusText.Text = $"Removed backup directories for '{link.Title}'";
            }
        }
    }

    private async void LinkMenu_ZipFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            bool isZipArchive = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            
            if (isZipArchive)
            {
                await ShowErrorDialogAsync(
                    "Already a Zip Archive",
                    "This is already a zip archive. You cannot zip a zip file.\n\nUse 'Explore Here' to open the zip file location if needed.");
                return;
            }

            if (!link.IsDirectory)
            {
                await ShowErrorDialogAsync(
                    "Cannot Zip File",
                    "Only folders can be zipped. This is a file link.");
                return;
            }

            if (link.IsCatalogEntry)
            {
                await ShowErrorDialogAsync(
                    "Cannot Zip Catalog Entry",
                    "Catalog entries cannot be zipped directly. Please zip the parent folder instead.");
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
                await ShowErrorDialogAsync(
                    "Cannot Remove Catalog Entry",
                    "Catalog entries cannot be removed individually. Use 'Refresh Catalog' to update them.");
                return;
            }

            await DeleteLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_AddSubLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem parentLink)
            return;

        if (parentLink.IsCatalogEntry || parentLink.IsDirectory)
        {
            await ShowErrorDialogAsync(
                "Cannot Add Sub-Link",
                parentLink.IsCatalogEntry 
                    ? "Catalog entries cannot have sub-links." 
                    : "Directory links cannot have sub-links. Use 'Create Catalog' instead.");
            return;
        }

        var result = await _linkDialog!.ShowEditAsync(new LinkItem
        {
            Title = string.Empty,
            Url = string.Empty,
            Description = string.Empty,
            Keywords = string.Empty,
            CategoryPath = parentLink.CategoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        });

        if (result != null)
        {
            var subLinkItem = new LinkItem
            {
                Title = result.Title,
                Url = result.Url,
                Description = result.Description,
                Keywords = result.Keywords,
                IsDirectory = result.IsDirectory,
                CategoryPath = parentLink.CategoryPath,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                FolderType = result.FolderType,
                FileFilters = result.FileFilters
            };

            var subLinkNode = new TreeViewNode { Content = subLinkItem };
            _contextMenuNode.Children.Add(subLinkNode);
            _contextMenuNode.IsExpanded = true;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkChangeAsync(
                    rootCategory.Name,
                    "added sub-link to",
                    parentLink.Title,
                    $"Sub-link: {result.Title}");
            }

            StatusText.Text = $"Added sub-link '{result.Title}' to '{parentLink.Title}'";
        }
    }

    private async void LinkMenu_ExploreHere_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            // Extract URL if it's a zip file link
            string targetPath = link.Url;
            if (link.IsDirectory && targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Get the parent directory of the zip file
                var zipParentDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(zipParentDir) && Directory.Exists(zipParentDir))
                {
                    targetPath = zipParentDir;
                }
            }

            try
            {
                var uri = new Uri(targetPath);
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error Opening Location", $"Could not open the location:\n\n{ex.Message}");
            }
        }
    }

    private async void LinkMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.Tags.Count == 0)
        {
            StatusText.Text = "This item has no tags to remove";
            return;
        }

        var tagService = TagManagementService.Instance;
        if (tagService == null)
            return;

        var tagsInfo = tagService.GetTagsInfo(link.Tags);
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

        var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
        var dialog = new ContentDialog
        {
            Title = "Remove Tag",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = $"Select tag to remove from {itemType} '{link.Title}':" },
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
            var tagToRemove = link.Tags.FirstOrDefault(t => 
                string.Equals(t, selectedTagName, StringComparison.OrdinalIgnoreCase));
            
            if (tagToRemove != null)
            {
                link.Tags.Remove(tagToRemove);
                link.ModifiedDate = DateTime.Now;
                link.NotifyTagsChanged();

                var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
                _contextMenuNode = updatedNode;

                var rootNode = GetRootCategoryNode(updatedNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                StatusText.Text = $"Removed tag '{selectedTagName}' from {itemType} '{link.Title}'";
            }
        }
    }

    private async void LinkMenu_Summarize_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Only works for URL links
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusText.Text = "Summarize is only available for HTTP/HTTPS URLs";
            return;
        }

        // Create cancellation token source
        var cts = new System.Threading.CancellationTokenSource();

        // Show busy dialog with cancel button
        var busyContent = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        busyContent.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 50,
            Height = 50
        });
        
        busyContent.Children.Add(new TextBlock
        {
            Text = $"Summarizing URL...\n{link.Url}",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            MaxWidth = 400
        });

        var busyDialog = new ContentDialog
        {
            Title = "Fetching Summary",
            Content = busyContent,
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        // Handle cancel
        busyDialog.CloseButtonClick += (s, args) =>
        {
            cts.Cancel();
        };

        StatusText.Text = $"Summarizing URL: {link.Url}...";

        // Start the summarize task
        var summaryTask = Task.Run(async () =>
        {
            var webSummaryService = new WebSummaryService();
            return await webSummaryService.SummarizeUrlAsync(link.Url, cts.Token);
        });

        // Show the busy dialog (it will be dismissed when we hide it)
        var dialogTask = busyDialog.ShowAsync();

        // Wait for the summary to complete
        WebPageSummary? summary = null;
        try
        {
            summary = await summaryTask;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Summarize cancelled";
            busyDialog.Hide();
            return;
        }
        catch (Exception ex)
        {
            busyDialog.Hide();
            await ShowErrorDialogAsync("Error", $"An error occurred while summarizing the URL:\n\n{ex.Message}");
            StatusText.Text = $"Error summarizing URL: {ex.Message}";
            return;
        }

        // Hide the busy dialog
        busyDialog.Hide();

        if (summary == null || summary.WasCancelled)
        {
            StatusText.Text = "Summarize cancelled";
            return;
        }

        if (!summary.Success)
        {
            await ShowErrorDialogAsync("Summarize Failed", $"Could not summarize the URL:\n\n{summary.ErrorMessage}");
            StatusText.Text = $"Failed to summarize: {summary.ErrorMessage}";
            return;
        }

        // Build the new description text
        var descriptionBuilder = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(summary.Description))
        {
            descriptionBuilder.AppendLine(summary.Description);
        }

        if (!string.IsNullOrWhiteSpace(summary.ContentSummary) && summary.ContentSummary != summary.Description)
        {
            if (descriptionBuilder.Length > 0)
                descriptionBuilder.AppendLine();
            descriptionBuilder.AppendLine(summary.ContentSummary);
        }

        if (!string.IsNullOrWhiteSpace(summary.Author) || !string.IsNullOrWhiteSpace(summary.PublishedDate))
        {
            if (descriptionBuilder.Length > 0)
                descriptionBuilder.AppendLine();
            if (!string.IsNullOrWhiteSpace(summary.Author))
                descriptionBuilder.AppendLine($"Author: {summary.Author}");
            if (!string.IsNullOrWhiteSpace(summary.PublishedDate))
                descriptionBuilder.AppendLine($"Published: {summary.PublishedDate}");
        }

        var newDescription = descriptionBuilder.ToString().Trim();
        var newKeywords = summary.Keywords.Count > 0 ? string.Join(", ", summary.Keywords) : string.Empty;

        // Build the comparison dialog with Edit Current vs Summary columns
        var mainGrid = new Grid
        {
            ColumnSpacing = 20,
            RowSpacing = 12,
            Width = 900
        };
        
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;

        // Store editable text boxes for saving
        TextBox? currentTitleBox = null;
        TextBox? currentDescriptionBox = null;
        TextBox? currentKeywordsBox = null;
        
        // Store summary text boxes for deferred text setting
        TextBox? summaryTitleBox = null;
        TextBox? summaryDescriptionBox = null;
        TextBox? summaryKeywordsBox = null;

        // Helper to add a comparison row
        void AddComparisonRow(string label, string currentValue, string newValue, ref TextBox? currentTextBoxRef, ref TextBox? summaryTextBoxRef, bool isDescription = false)
        {
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Label spanning both columns
            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, row > 0 ? 8 : 0, 0, 4)
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumnSpan(labelBlock, 2);
            mainGrid.Children.Add(labelBlock);
            row++;

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Set height based on whether this is a description field
            int fieldHeight = isDescription ? 250 : 80;

            // Edit Current value column
            var currentPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            currentPanel.Children.Add(new TextBlock
            {
                Text = "Edit Current:",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            var currentTextBox = new TextBox
            {
                PlaceholderText = "(empty)",
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = fieldHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ScrollViewer.SetVerticalScrollBarVisibility(currentTextBox, ScrollBarVisibility.Auto);
            currentPanel.Children.Add(currentTextBox);
            Grid.SetRow(currentPanel, row);
            Grid.SetColumn(currentPanel, 0);
            mainGrid.Children.Add(currentPanel);

            // Store reference to editable text box
            currentTextBoxRef = currentTextBox;

            // New/Summary value column (read-only for copying)
            var newPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            newPanel.Children.Add(new TextBlock
            {
                Text = "Summary:",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
            });
            var newTextBox = new TextBox
            {
                PlaceholderText = "(empty)",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = fieldHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Foreground = string.IsNullOrWhiteSpace(newValue) 
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) 
                    : null
            };
            ScrollViewer.SetVerticalScrollBarVisibility(newTextBox, ScrollBarVisibility.Auto);
            newPanel.Children.Add(newTextBox);
            Grid.SetRow(newPanel, row);
            Grid.SetColumn(newPanel, 1);
            mainGrid.Children.Add(newPanel);
            
            // Store reference to summary text box
            summaryTextBoxRef = newTextBox;
            
            row++;
        }

        // Add comparison rows (text will be set after controls are added to visual tree)
        AddComparisonRow("Title", link.Title, summary.Title, ref currentTitleBox, ref summaryTitleBox);
        AddComparisonRow("Description", link.Description, newDescription, ref currentDescriptionBox, ref summaryDescriptionBox, isDescription: true);
        AddComparisonRow("Keywords", link.Keywords, newKeywords, ref currentKeywordsBox, ref summaryKeywordsBox);

        // Set text AFTER controls are created to avoid WinUI TextBox truncation issue
        if (currentTitleBox != null) currentTitleBox.Text = link.Title ?? string.Empty;
        if (currentDescriptionBox != null) currentDescriptionBox.Text = link.Description ?? string.Empty;
        if (currentKeywordsBox != null) currentKeywordsBox.Text = link.Keywords ?? string.Empty;
        if (summaryTitleBox != null) summaryTitleBox.Text = summary.Title ?? string.Empty;
        if (summaryDescriptionBox != null) summaryDescriptionBox.Text = newDescription ?? string.Empty;
        if (summaryKeywordsBox != null) summaryKeywordsBox.Text = newKeywords ?? string.Empty;

        // Add metadata info if available
        if (!string.IsNullOrWhiteSpace(summary.SiteName) || !string.IsNullOrWhiteSpace(summary.ContentType))
        {
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var metadataText = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(summary.SiteName))
                metadataText.AppendLine($"📍 Site: {summary.SiteName}");
            if (!string.IsNullOrWhiteSpace(summary.ContentType))
                metadataText.AppendLine($"📄 Type: {summary.ContentType}");

            var metadataBlock = new TextBlock
            {
                Text = metadataText.ToString().Trim(),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 12, 0, 0)
            };
            Grid.SetRow(metadataBlock, row);
            Grid.SetColumnSpan(metadataBlock, 2);
            mainGrid.Children.Add(metadataBlock);
        }

        // Show the comparison dialog with wider content
        var compareDialog = new ContentDialog
        {
            Title = "URL Summary - Compare & Edit",
            Content = new ScrollViewer
            {
                Content = mainGrid,
                MaxHeight = 700,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            },
            PrimaryButtonText = "Save Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        // Override the default ContentDialog width constraint
        compareDialog.Resources["ContentDialogMaxWidth"] = 1000.0;

        var result = await compareDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Save the edited current values
            if (currentTitleBox != null)
            {
                link.Title = currentTitleBox.Text.Trim();
            }

            if (currentDescriptionBox != null)
            {
                link.Description = currentDescriptionBox.Text.Trim();
            }

            if (currentKeywordsBox != null)
            {
                link.Keywords = currentKeywordsBox.Text.Trim();
            }

            link.ModifiedDate = DateTime.Now;

            var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
            _contextMenuNode = updatedNode;

            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Saved changes to: {link.Title}";
        }
        else
        {
            StatusText.Text = "Summary cancelled";
        }
    }

    private async void LinkMenu_SortCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (!link.IsDirectory)
        {
            StatusText.Text = "Sort is only available for directories";
            return;
        }

        await ShowSortDialogAsync(_contextMenuNode, link.CatalogSortOrder, isCategory: false);
    }

    private async void LinkMenu_RatingTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string templateName)
            return;

        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        var originalTemplate = _ratingService.CurrentTemplateName;
        _ratingService.SwitchTemplate(templateName);

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(link.Title, link.Ratings);

        _ratingService.SwitchTemplate(originalTemplate);

        if (result != null)
        {
            link.Ratings = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for {itemType} '{link.Title}' using template '{displayName}'"
                : $"Removed all ratings from {itemType} '{link.Title}'";
        }
    }

    private async void LinkMenu_Ratings_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(link.Title, link.Ratings);

        if (result != null)
        {
            link.Ratings = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for {itemType} '{link.Title}'"
                : $"Removed all ratings from {itemType} '{link.Title}'";
        }
    }

    // ========================================
    // SHARED HELPER METHODS
    // ========================================

    private async Task ShowSortDialogAsync(TreeViewNode node, SortOption currentSort, bool isCategory)
    {
        var sortComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        sortComboBox.Items.Add(new ComboBoxItem { Content = "Name (A-Z)", Tag = SortOption.NameAscending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Name (Z-A)", Tag = SortOption.NameDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Date (Newest First)", Tag = SortOption.DateDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Date (Oldest First)", Tag = SortOption.DateAscending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Size (Largest First)", Tag = SortOption.SizeDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Size (Smallest First)", Tag = SortOption.SizeAscending });

        sortComboBox.SelectedIndex = (int)currentSort;

        var dialog = new ContentDialog
        {
            Title = isCategory ? "Sort Category" : "Sort Catalog",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Select sort order:" },
                    sortComboBox
                }
            },
            PrimaryButtonText = "Sort",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && sortComboBox.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is SortOption newSort)
        {
            if (isCategory && node.Content is CategoryItem category)
            {
                category.SortOrder = newSort;
                category.ModifiedDate = DateTime.Now;
                SortingService.SortCategoryChildren(node, newSort);
            }
            else if (!isCategory && node.Content is LinkItem link)
            {
                link.CatalogSortOrder = newSort;
                link.ModifiedDate = DateTime.Now;
                SortingService.SortCatalogEntries(node, newSort);
            }

            var rootNode = GetRootCategoryNode(node);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Sorted by {selectedItem.Content}";
        }
    }
}
