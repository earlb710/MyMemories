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

        MenuFlyout? menu = null;
        
        if (node.Content is CategoryItem category)
        {
            menu = LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout;
            if (menu != null)
            {
                ConfigureCategoryContextMenu(menu, category, node);
            }
        }
        else if (node.Content is LinkItem linkItem)
        {
            menu = LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout;
            if (menu != null)
            {
                ConfigureLinkContextMenu(menu, linkItem, node);
            }
        }

        menu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
        e.Handled = true;
    }

    /// <summary>
    /// Configures the category context menu based on the node's properties.
    /// </summary>
    private void ConfigureCategoryContextMenu(MenuFlyout menu, CategoryItem category, TreeViewNode node)
    {
        // Get menu items by name
        var changePasswordItem = FindMenuItemByName(menu, "CategoryMenu_ChangePassword");
        var zipCategoryItem = FindMenuItemByName(menu, "CategoryMenu_ZipCategory");
        var addTagSubItem = FindSubMenuItemByName(menu, "CategoryMenu_AddTag");
        var removeTagItem = FindMenuItemByName(menu, "CategoryMenu_RemoveTag");

        // Change Password: only enabled for root categories
        bool isRootCategory = LinksTreeView.RootNodes.Contains(node);
        if (changePasswordItem != null)
        {
            changePasswordItem.IsEnabled = isRootCategory;
        }

        // Zip Category: check if category has any folders
        if (zipCategoryItem != null)
        {
            var statisticsService = new CategoryStatisticsService();
            var folderPaths = statisticsService.CollectFolderPathsFromCategory(node);
            zipCategoryItem.IsEnabled = folderPaths.Count > 0;
        }

        // Configure Add Tag submenu
        if (addTagSubItem != null)
        {
            PopulateAddTagSubmenu(addTagSubItem, category.TagIds, isCategory: true);
        }

        // Remove Tag: only enabled if category has tags
        if (removeTagItem != null)
        {
            removeTagItem.IsEnabled = category.TagIds.Count > 0;
        }
    }

    /// <summary>
    /// Configures the link context menu based on the link's properties.
    /// </summary>
    private void ConfigureLinkContextMenu(MenuFlyout menu, LinkItem link, TreeViewNode node)
    {
        // Get menu items by name
        var addSubLinkItem = FindMenuItemByName(menu, "LinkMenu_AddSubLink");
        var editItem = FindMenuItemByName(menu, "LinkMenu_Edit");
        var copyItem = FindMenuItemByName(menu, "LinkMenu_Copy");
        var moveItem = FindMenuItemByName(menu, "LinkMenu_Move");
        var removeItem = FindMenuItemByName(menu, "LinkMenu_Remove");
        var changePasswordItem = FindMenuItemByName(menu, "LinkMenu_ChangePassword");
        var zipFolderItem = FindMenuItemByName(menu, "LinkMenu_ZipFolder");
        var exploreHereItem = FindMenuItemByName(menu, "LinkMenu_ExploreHere");
        var sortCatalogItem = FindMenuItemByName(menu, "LinkMenu_SortCatalog");
        var summarizeItem = FindMenuItemByName(menu, "LinkMenu_Summarize");
        var addTagSubItem = FindSubMenuItemByName(menu, "LinkMenu_AddTag");
        var removeTagItem = FindMenuItemByName(menu, "LinkMenu_RemoveTag");

        // Check conditions
        bool isCatalogEntry = link.IsCatalogEntry;
        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        bool isDirectory = link.IsDirectory;
        bool isZipOrDirectory = isZipFile || (isDirectory && Directory.Exists(link.Url));
        bool hasCatalog = isDirectory && node.Children.Count > 0;
        bool isUrl = Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && 
                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        // Add Sub-Link: only enabled for non-catalog, non-directory links (URL links)
        if (addSubLinkItem != null)
        {
            addSubLinkItem.IsEnabled = !isCatalogEntry && !isDirectory;
        }

        // Edit, Copy, Move, Remove: disabled for catalog entries
        if (editItem != null)
        {
            editItem.IsEnabled = !isCatalogEntry;
        }
        if (copyItem != null)
        {
            copyItem.IsEnabled = !isCatalogEntry;
        }
        if (moveItem != null)
        {
            moveItem.IsEnabled = !isCatalogEntry;
        }
        if (removeItem != null)
        {
            removeItem.IsEnabled = !isCatalogEntry;
        }

        // Change Password: only for zip files
        if (changePasswordItem != null)
        {
            changePasswordItem.IsEnabled = isZipFile;
        }

        // Summarize: only for URLs (http/https)
        if (summarizeItem != null)
        {
            summarizeItem.IsEnabled = isUrl;
        }

        // Zip Folder: only for non-zip, non-catalog directories
        if (zipFolderItem != null)
        {
            zipFolderItem.IsEnabled = isDirectory && !isZipFile && !isCatalogEntry;
        }

        // Explore Here: only for directories and zip files (that exist)
        if (exploreHereItem != null)
        {
            exploreHereItem.IsEnabled = isZipOrDirectory;
        }

        // Sort Catalog: only for directories with catalog entries
        if (sortCatalogItem != null)
        {
            sortCatalogItem.IsEnabled = isDirectory && hasCatalog;
        }

        // Configure Add Tag submenu: disabled for catalog entries
        if (addTagSubItem != null)
        {
            addTagSubItem.IsEnabled = !isCatalogEntry;
            if (!isCatalogEntry)
            {
                PopulateAddTagSubmenu(addTagSubItem, link.TagIds, isCategory: false);
            }
        }

        // Remove Tag: only enabled if link has tags and is not a catalog entry
        if (removeTagItem != null)
        {
            removeTagItem.IsEnabled = !isCatalogEntry && link.TagIds.Count > 0;
        }
    }

    /// <summary>
    /// Finds a menu flyout item by its x:Name property.
    /// </summary>
    private MenuFlyoutItem? FindMenuItemByName(MenuFlyout menu, string name)
    {
        foreach (var item in menu.Items)
        {
            if (item is MenuFlyoutItem menuItem && menuItem.Name == name)
            {
                return menuItem;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a menu flyout sub item by its x:Name property.
    /// </summary>
    private MenuFlyoutSubItem? FindSubMenuItemByName(MenuFlyout menu, string name)
    {
        foreach (var item in menu.Items)
        {
            if (item is MenuFlyoutSubItem subItem && subItem.Name == name)
            {
                return subItem;
            }
        }
        return null;
    }

    /// <summary>
    /// Populates the Add Tag submenu with available tags.
    /// </summary>
    private void PopulateAddTagSubmenu(MenuFlyoutSubItem subMenu, List<string> existingTagIds, bool isCategory)
    {
        subMenu.Items.Clear();

        if (_tagService == null || _tagService.TagCount == 0)
        {
            var noTagsItem = new MenuFlyoutItem
            {
                Text = "No tags available",
                IsEnabled = false
            };
            subMenu.Items.Add(noTagsItem);
            return;
        }

        // Add available tags (excluding already assigned ones)
        var availableTags = _tagService.Tags.Where(t => !existingTagIds.Contains(t.Id)).ToList();

        if (availableTags.Count == 0)
        {
            var allAssignedItem = new MenuFlyoutItem
            {
                Text = "All tags already assigned",
                IsEnabled = false
            };
            subMenu.Items.Add(allAssignedItem);
            return;
        }

        foreach (var tag in availableTags)
        {
            var tagItem = new MenuFlyoutItem
            {
                Text = $"🏷️ {tag.Name}",
                Tag = tag.Id
            };

            if (isCategory)
            {
                tagItem.Click += CategoryMenu_AddTagItem_Click;
            }
            else
            {
                tagItem.Click += LinkMenu_AddTagItem_Click;
            }

            subMenu.Items.Add(tagItem);
        }
    }

    private async void CategoryMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagId)
            return;

        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Add the tag to the category
        if (!category.TagIds.Contains(tagId))
        {
            category.TagIds.Add(tagId);
            category.ModifiedDate = DateTime.Now;
            category.NotifyTagsChanged();

            // Refresh the node to update display
            var updatedNode = _treeViewService!.RefreshCategoryNode(_contextMenuNode, category);
            _contextMenuNode = updatedNode;

            // Save
            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var tag = TagManagementService.Instance?.GetTag(tagId);
            StatusText.Text = $"Added tag '{tag?.Name}' to category '{category.Name}'";
        }
    }

    private async void LinkMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagId)
            return;

        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Add the tag to the link
        if (!link.TagIds.Contains(tagId))
        {
            link.TagIds.Add(tagId);
            link.ModifiedDate = DateTime.Now;
            link.NotifyTagsChanged();

            // Refresh the node to update display
            var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
            _contextMenuNode = updatedNode;

            // Save
            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var tag = TagManagementService.Instance?.GetTag(tagId);
            StatusText.Text = $"Added tag '{tag?.Name}' to link '{link.Title}'";
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

        // Check if this node is a root category by verifying it's in the RootNodes collection
        bool isRootCategory = LinksTreeView.RootNodes.Contains(_contextMenuNode);
        
        if (!isRootCategory)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Not a Root Category",
                Content = "Password protection can only be changed for root categories.\n\nSubcategories inherit their parent's password protection.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        await ShowChangePasswordDialogAsync(category, _contextMenuNode);
    }

    private async void CategoryMenu_ZipCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Use CategoryStatisticsService to collect folder paths
        var statisticsService = new CategoryStatisticsService();
        var folderPaths = statisticsService.CollectFolderPathsFromCategory(_contextMenuNode);

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

        // CRITICAL FIX: Get the ROOT category to check for password protection
        var rootCategoryNode = GetRootCategoryNode(_contextMenuNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;
        
        // DEBUG OUTPUT
        System.Diagnostics.Debug.WriteLine($"[CategoryMenu_ZipCategory_Click] Current category: {category.Name}, PasswordProtection: {category.PasswordProtection}");
        System.Diagnostics.Debug.WriteLine($"[CategoryMenu_ZipCategory_Click] Root category: {rootCategory?.Name}, PasswordProtection: {rootCategory?.PasswordProtection}");
        
        // Check if ROOT category has password protection (not the current subcategory)
        bool categoryHasPassword = rootCategory?.PasswordProtection != PasswordProtectionType.None;
        string? categoryPassword = null;

        System.Diagnostics.Debug.WriteLine($"[CategoryMenu_ZipCategory_Click] categoryHasPassword: {categoryHasPassword}");

        if (categoryHasPassword && rootCategory != null)
        {
            System.Diagnostics.Debug.WriteLine($"[CategoryMenu_ZipCategory_Click] Attempting to get password for root category: {rootCategory.Name}");
            // Get the password from the ROOT category
            categoryPassword = await GetCategoryPasswordAsync(rootCategory);
            System.Diagnostics.Debug.WriteLine($"[CategoryMenu_ZipCategory_Click] Password retrieved: {(categoryPassword != null ? "Yes" : "No")}");
            if (categoryPassword == null)
            {
                // User cancelled password entry or password retrieval failed
                return;
            }
        }

        // Show zip dialog with multiple source folders and password info
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

        // Create temporary zip node with "busy creating" indicator if linking to category
        TreeViewNode? zipLinkNode = null;
        TreeViewNode? busyNode = null;

        if (result.LinkToCategory)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(_contextMenuNode);

            // Create the zip link item (but with temporary status)
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

            // Create busy indicator child node
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

            // Add the zip node to the category
            _contextMenuNode.Children.Add(zipLinkNode);
            _contextMenuNode.IsExpanded = true;

            // Navigate to the newly created zip node
            LinksTreeView.SelectedNode = zipLinkNode;
        }

        // Create zip file from all folders
        try
        {
            StatusText.Text = $"Creating{(result.UsePassword ? " password-protected" : "")} zip file '{zipFileName}' from {folderPaths.Count} folder(s)...";

            // Start timing
            var startTime = DateTime.Now;

            // CRITICAL: Collect folder info on UI thread BEFORE entering background thread
            var folderInfoList = CollectFolderInfoFromCategory(_contextMenuNode, category.Name);
            var manifestContent = GenerateManifestContent(folderInfoList, category.Name);

            // Calculate original size for compression ratio
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
                // Use password-protected zip creation WITH MANIFEST (pass pre-collected data)
                await Task.Run(() =>
                {
                    using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                    using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                    zipStream.SetLevel(6); // Compression level 0-9
                    zipStream.Password = result.Password;
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;

                    // Create and add the manifest file FIRST
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

                    // Then add all folder contents
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
                // Standard zip creation WITH MANIFEST (use pre-collected data)
                await Task.Run(() =>
                {
                    using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        // Create and add the manifest file FIRST
                        var manifestEntry = archive.CreateEntry("_MANIFEST.txt", CompressionLevel.Optimal);
                        using (var writer = new StreamWriter(manifestEntry.Open(), System.Text.Encoding.UTF8))
                        {
                            writer.Write(manifestContent);
                        }

                        // Then add all folder contents
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
            }

            StatusText.Text = $"Successfully created '{zipFileName}'";

            // Calculate compression statistics
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            var compressedSize = (ulong)new FileInfo(zipFilePath).Length;
            var compressionRatio = originalSize > 0 ? (1.0 - ((double)compressedSize / originalSize)) * 100.0 : 0.0;

            // Remove busy node and update the zip node with catalog contents
            if (result.LinkToCategory && zipLinkNode != null)
            {
                // Remove the busy indicator
                if (busyNode != null)
                {
                    zipLinkNode.Children.Remove(busyNode);
                }

                // Update the zip link item with final information
                var finalZipLinkItem = zipLinkNode.Content as LinkItem;
                if (finalZipLinkItem != null)
                {
                    finalZipLinkItem.Description = $"Zip archive of folders from '{category.Name}'"
                        + (result.UsePassword ? " (password-protected)" : "");
                    finalZipLinkItem.FileSize = (ulong)new FileInfo(zipFilePath).Length;
                    finalZipLinkItem.LastCatalogUpdate = DateTime.Now;
                    finalZipLinkItem.IsZipPasswordProtected = result.UsePassword;
                }

                // Catalog the zip file contents
                await _catalogService!.CreateCatalogAsync(finalZipLinkItem!, zipLinkNode);

                // Save the updated category
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

            // Remove the temporary zip node if it was created
            if (zipLinkNode != null)
            {
                _contextMenuNode.Children.Remove(zipLinkNode);
            }

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

        // Use CategoryStatisticsService to collect folder paths
        var statisticsService = new CategoryStatisticsService();
        var folderPaths = statisticsService.CollectFolderPathsFromCategory(_contextMenuNode);

        // Calculate statistics
        StatusText.Text = "Calculating category statistics...";

        var stats = await Task.Run(() => statisticsService.CalculateMultipleFoldersStatistics(folderPaths.ToArray()));

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
    /// Recursively collects all folder paths from a category node (excluding catalog entries).
    /// </summary>
    private List<string> CollectFolderPathsFromCategory(TreeViewNode categoryNode)
    {
        var statisticsService = new CategoryStatisticsService();
        return statisticsService.CollectFolderPathsFromCategory(categoryNode);
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

    private async void LinkMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.IsCatalogEntry)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Cannot Copy Catalog Entry",
                Content = "Catalog entries are read-only and cannot be copied.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Find the parent category node
        var parentNode = FindParentNode(_contextMenuNode);
        if (parentNode == null)
        {
            StatusText.Text = "Could not find parent category";
            return;
        }

        // Generate a unique title with sequence number
        var baseTitle = link.Title;
        var newTitle = GenerateUniqueTitle(baseTitle, parentNode);

        // Create a copy of the link
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

        // Create the new node
        var copiedNode = new TreeViewNode { Content = copiedLink };

        // Add the copied link to the parent category (after the original)
        var insertIndex = parentNode.Children.IndexOf(_contextMenuNode) + 1;
        if (insertIndex > 0 && insertIndex <= parentNode.Children.Count)
        {
            parentNode.Children.Insert(insertIndex, copiedNode);
        }
        else
        {
            parentNode.Children.Add(copiedNode);
        }

        // Save the category
        var rootNode = GetRootCategoryNode(parentNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        StatusText.Text = $"Copied link as '{newTitle}'";

        // Select the new node and open the edit dialog
        LinksTreeView.SelectedNode = copiedNode;
        _contextMenuNode = copiedNode;

        // Open the edit dialog for the copied link
        await EditLinkAsync(copiedLink, copiedNode);
    }

    /// <summary>
    /// Generates a unique title by appending a sequence number if needed.
    /// </summary>
    private string GenerateUniqueTitle(string baseTitle, TreeViewNode parentNode)
    {
        // Check if base title already ends with a number in parentheses like " (2)"
        var match = System.Text.RegularExpressions.Regex.Match(baseTitle, @"^(.+?)\s*\((\d+)\)$");
        string coreName;
        int startSequence;

        if (match.Success)
        {
            coreName = match.Groups[1].Value.Trim();
            startSequence = int.Parse(match.Groups[2].Value) + 1;
        }
        else
        {
            coreName = baseTitle;
            startSequence = 2;
        }

        // Collect existing titles in the parent
        var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in parentNode.Children)
        {
            if (child.Content is LinkItem childLink)
            {
                existingTitles.Add(childLink.Title);
            }
        }

        // Find a unique title
        var newTitle = $"{coreName} ({startSequence})";
        while (existingTitles.Contains(newTitle))
        {
            startSequence++;
            newTitle = $"{coreName} ({startSequence})";
        }

        return newTitle;
    }

    /// <summary>
    /// Finds the parent node of a given tree view node.
    /// </summary>
    private TreeViewNode? FindParentNode(TreeViewNode childNode)
    {
        // Check root nodes first
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            var parent = FindParentNodeRecursive(rootNode, childNode);
            if (parent != null)
                return parent;
        }
        return null;
    }

    /// <summary>
    /// Recursively searches for the parent of a given node.
    /// </summary>
    private TreeViewNode? FindParentNodeRecursive(TreeViewNode currentNode, TreeViewNode targetChild)
    {
        foreach (var child in currentNode.Children)
        {
            if (child == targetChild)
                return currentNode;

            var found = FindParentNodeRecursive(child, targetChild);
            if (found != null)
                return found;
        }
        return null;
    }

    private async void LinkMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Only allow for zip files
        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        
        if (!isZipFile)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Not a Zip File",
                Content = "Password protection can only be changed for zip archive files.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        await ShowChangeZipPasswordDialogAsync(link, _contextMenuNode);
    }

    private async void LinkMenu_ZipFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            // Check if this is already a zip archive
            bool isZipArchive = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            
            if (isZipArchive)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Already a Zip Archive",
                    Content = "This is already a zip archive. You cannot zip a zip file.\n\nUse 'Explore Here' to open the zip file location if needed.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

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

    private async void LinkMenu_AddSubLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem parentLink)
            return;

        // Don't allow sub-links for catalog entries or directories
        if (parentLink.IsCatalogEntry || parentLink.IsDirectory)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Cannot Add Sub-Link",
                Content = parentLink.IsCatalogEntry 
                    ? "Catalog entries cannot have sub-links." 
                    : "Directory links cannot have sub-links. Use 'Create Catalog' instead.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Show add link dialog (without category selection since we're adding to the current link)
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

            // Save the category
            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Audit log the sub-link addition
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
                var errorDialog = new ContentDialog
                {
                    Title = "Error Opening Location",
                    Content = $"Could not open the location:\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    private async void LinkMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.TagIds.Count == 0)
        {
            StatusText.Text = "This link has no tags to remove";
            return;
        }

        // Show dialog to select which tag to remove
        var tagService = TagManagementService.Instance;
        if (tagService == null)
            return;

        var tagsInfo = tagService.GetTagsInfo(link.TagIds);
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
                    new TextBlock { Text = $"Select tag to remove from '{link.Title}':" },
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
            var tagToRemove = tagService.GetTagByName(selectedTagName);
            if (tagToRemove != null && link.TagIds.Contains(tagToRemove.Id))
            {
                link.TagIds.Remove(tagToRemove.Id);
                link.ModifiedDate = DateTime.Now;
                link.NotifyTagsChanged();

                var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
                _contextMenuNode = updatedNode;

                var rootNode = GetRootCategoryNode(updatedNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                StatusText.Text = $"Removed tag '{selectedTagName}' from link '{link.Title}'";
            }
        }
    }

    private async void CategoryMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (category.TagIds.Count == 0)
        {
            StatusText.Text = "This category has no tags to remove";
            return;
        }

        var tagService = TagManagementService.Instance;
        if (tagService == null)
            return;

        var tagsInfo = tagService.GetTagsInfo(category.TagIds);
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
            var tagToRemove = tagService.GetTagByName(selectedTagName);
            if (tagToRemove != null && category.TagIds.Contains(tagToRemove.Id))
            {
                category.TagIds.Remove(tagToRemove.Id);
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
            var errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = $"An error occurred while summarizing the URL:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
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
            var errorDialog = new ContentDialog
            {
                Title = "Summarize Failed",
                Content = $"Could not summarize the URL:\n\n{summary.ErrorMessage}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
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

    private async void CategoryMenu_SortBy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        await ShowSortDialogAsync(_contextMenuNode, category.SortOrder, isCategory: true);
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
