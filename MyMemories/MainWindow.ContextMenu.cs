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

    private async void LinkMenu_ExploreHere_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Check if this is a directory or zip file
        bool isZipFile = link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(link.Url);
        bool isDirectory = link.IsDirectory && Directory.Exists(link.Url);

        if (isZipFile)
        {
            // For zip files, open the parent directory and select the zip file
            try
            {
                var zipFileInfo = new FileInfo(link.Url);
                var parentDirectory = zipFileInfo.DirectoryName;

                if (!string.IsNullOrEmpty(parentDirectory) && Directory.Exists(parentDirectory))
                {
                    // Use Windows Explorer to open the folder and select the file
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{link.Url}\"");
                    StatusText.Text = $"Opened location of '{link.Title}'";
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Directory Not Found",
                        Content = "The parent directory of this zip file does not exist.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error Opening Location",
                    Content = $"Could not open the file location:\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
        else if (isDirectory)
        {
            // For directories, open in File Explorer
            try
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(link.Url);
                await Windows.System.Launcher.LaunchFolderAsync(folder);
                StatusText.Text = $"Opened '{link.Title}' in File Explorer";
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error Opening Folder",
                    Content = $"Could not open the folder:\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
        else
        {
            // Not a directory or zip file
            var errorDialog = new ContentDialog
            {
                Title = "Not a Folder or Zip File",
                Content = "The 'Explore Here' option is only available for folders and zip files.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async void LinkMenu_Summarize_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Verify this is a URL
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Not a Web URL",
                Content = "The summarize feature only works with web URLs (http:// or https://).",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Create cancellation token source
        using var cts = new System.Threading.CancellationTokenSource();

        // Show loading dialog with cancel button
        var loadingDialog = new ContentDialog
        {
            Title = "Summarizing URL",
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new ProgressRing { IsActive = true, Width = 48, Height = 48 },
                    new TextBlock
                    {
                        Text = $"Fetching and analyzing:\n{link.Url}",
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "This may take a few seconds...",
                        FontSize = 11,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        // Track if cancelled via dialog
        bool wasCancelledByUser = false;

        // Start the summarization task
        var summaryService = new WebSummaryService();
        var summaryTask = summaryService.SummarizeUrlAsync(link.Url, cts.Token);

        // Show dialog and handle cancellation
        var dialogTask = loadingDialog.ShowAsync();

        // When dialog is closed (Cancel clicked), cancel the operation
        _ = dialogTask.AsTask().ContinueWith(_ =>
        {
            if (!summaryTask.IsCompleted)
            {
                wasCancelledByUser = true;
                cts.Cancel();
            }
        });

        try
        {
            StatusText.Text = $"Summarizing '{link.Title}'...";

            // Wait for the summary task to complete
            var summary = await summaryTask;

            // Close loading dialog
            loadingDialog.Hide();

            // Check if cancelled
            if (summary.WasCancelled || wasCancelledByUser)
            {
                StatusText.Text = "Summarization cancelled";
                return;
            }

            if (!summary.Success)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error Fetching URL",
                    Content = $"Failed to fetch or summarize the URL:\n\n{summary.ErrorMessage}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                StatusText.Text = $"Failed to summarize '{link.Title}'";
                return;
            }

            // Handle binary content (images, PDFs, etc.)
            if (summary.IsBinaryContent)
            {
                await ShowBinarySummaryDialog(link, summary);
                return;
            }

            // Build summary UI with editable fields for HTML content
            var stackPanel = new StackPanel { Spacing = 12, Width = 650 };

            // Title from website
            stackPanel.Children.Add(new TextBlock
            {
                Text = summary.Title,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });

            // URL
            stackPanel.Children.Add(new TextBlock
            {
                Text = summary.Url,
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -8, 0, 0),
                IsTextSelectionEnabled = true
            });

            // Metadata row (status, site name, author, date)
            var metadataPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Margin = new Thickness(0, 4, 0, 0)
            };

            if (summary.StatusCode > 0)
            {
                metadataPanel.Children.Add(new TextBlock
                {
                    Text = $"HTTP {summary.StatusCode}",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                });
            }

            if (!string.IsNullOrEmpty(summary.SiteName))
            {
                metadataPanel.Children.Add(new TextBlock
                {
                    Text = $"📰 {summary.SiteName}",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                });
            }

            if (!string.IsNullOrEmpty(summary.Author))
            {
                metadataPanel.Children.Add(new TextBlock
                {
                    Text = $"✍️ {summary.Author}",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange)
                });
            }

            if (!string.IsNullOrEmpty(summary.PublishedDate))
            {
                metadataPanel.Children.Add(new TextBlock
                {
                    Text = $"📅 {summary.PublishedDate}",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }

            if (metadataPanel.Children.Count > 0)
            {
                stackPanel.Children.Add(metadataPanel);
            }

            // Content type and locale
            if (!string.IsNullOrEmpty(summary.ContentType) || !string.IsNullOrEmpty(summary.Locale))
            {
                var typeLocalePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16
                };

                if (!string.IsNullOrEmpty(summary.ContentType))
                {
                    typeLocalePanel.Children.Add(new TextBlock
                    {
                        Text = $"Type: {summary.ContentType}",
                        FontSize = 10,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                    });
                }

                if (!string.IsNullOrEmpty(summary.Locale))
                {
                    typeLocalePanel.Children.Add(new TextBlock
                    {
                        Text = $"Language: {summary.Locale}",
                        FontSize = 10,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                    });
                }

                stackPanel.Children.Add(typeLocalePanel);
            }

            // Separator
            stackPanel.Children.Add(new Border
            {
                Height = 1,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Opacity = 0.3,
                Margin = new Thickness(0, 8, 0, 8)
            });

            // Current Description Section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Current Description:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(link.Description) ? "(No description set)" : link.Description,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = string.IsNullOrWhiteSpace(link.Description) ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    string.IsNullOrWhiteSpace(link.Description) ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.LightGray),
                Margin = new Thickness(0, 0, 0, 8),
                IsTextSelectionEnabled = true
            });

            // Fetched Description from Website
            if (!string.IsNullOrWhiteSpace(summary.Description))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "Description from Website:",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                
                stackPanel.Children.Add(new TextBlock
                {
                    Text = summary.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                    Margin = new Thickness(0, 0, 0, 8),
                    IsTextSelectionEnabled = true
                });
            }

            // Editable Description
            stackPanel.Children.Add(new TextBlock
            {
                Text = "New Description (editable):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            });

            var descriptionTextBox = new TextBox
            {
                Text = !string.IsNullOrWhiteSpace(summary.Description) ? summary.Description : link.Description,
                PlaceholderText = "Enter description...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 80,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(descriptionTextBox);

            // Current Keywords Section
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Current Keywords:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(link.Keywords) ? "(No keywords set)" : link.Keywords,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = string.IsNullOrWhiteSpace(link.Keywords) ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    string.IsNullOrWhiteSpace(link.Keywords) ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.LightGray),
                Margin = new Thickness(0, 0, 0, 8),
                IsTextSelectionEnabled = true
            });

            // Keywords from Website (displayed as badges - gray for existing, blue for new)
            if (summary.Keywords.Count > 0)
            {
                // Parse existing keywords for comparison
                var existingKeywordsSet = string.IsNullOrWhiteSpace(link.Keywords) 
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(
                        link.Keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(k => k.Trim()),
                        StringComparer.OrdinalIgnoreCase);

                stackPanel.Children.Add(new TextBlock
                {
                    Text = "Keywords from Website:",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                // Use ItemsRepeater with UniformGridLayout for wrapping, or fall back to a simple wrapping approach
                // Using a vertical StackPanel with multiple horizontal rows for wrapping
                var keywordsContainer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6
                };

                // Create rows of keywords that wrap
                var currentRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6
                };

                int keywordsPerRow = 4; // Approximate keywords per row
                int keywordCount = 0;

                foreach (var keyword in summary.Keywords.Take(15)) // Limit to 15 for display
                {
                    // Check if this keyword already exists
                    bool isExistingKeyword = existingKeywordsSet.Contains(keyword.Trim());

                    var keywordBorder = new Border
                    {
                        // Gray background for existing keywords, Blue for new keywords
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            isExistingKeyword ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.DodgerBlue),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4)
                    };

                    keywordBorder.Child = new TextBlock
                    {
                        Text = keyword,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                        FontSize = 11,
                        MaxWidth = 150,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    // Add tooltip to indicate status and show full text if truncated
                    ToolTipService.SetToolTip(keywordBorder, 
                        $"{keyword}\n({(isExistingKeyword ? "Already in your keywords" : "New keyword from website")})");

                    currentRow.Children.Add(keywordBorder);
                    keywordCount++;

                    // Start a new row after keywordsPerRow items
                    if (keywordCount % keywordsPerRow == 0)
                    {
                        keywordsContainer.Children.Add(currentRow);
                        currentRow = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6
                        };
                    }
                }

                // Add the last row if it has items
                if (currentRow.Children.Count > 0)
                {
                    keywordsContainer.Children.Add(currentRow);
                }

                stackPanel.Children.Add(keywordsContainer);
            }

            // Merge existing and new keywords (no duplicates)
            var existingKeywords = string.IsNullOrWhiteSpace(link.Keywords) 
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    link.Keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim()),
                    StringComparer.OrdinalIgnoreCase);

            var newKeywords = new HashSet<string>(existingKeywords, StringComparer.OrdinalIgnoreCase);
            foreach (var keyword in summary.Keywords)
            {
                newKeywords.Add(keyword.Trim());
            }

            var mergedKeywordsText = string.Join(", ", newKeywords.OrderBy(k => k));

            // Editable Keywords
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Merged Keywords (editable, comma or semicolon separated):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            });

            var keywordsTextBox = new TextBox
            {
                Text = mergedKeywordsText,
                PlaceholderText = "Enter keywords separated by comma or semicolon...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 60,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(keywordsTextBox);

            // Content Summary (read-only but selectable)
            if (!string.IsNullOrWhiteSpace(summary.ContentSummary))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "Content Summary:",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 4)
                });

                stackPanel.Children.Add(new TextBlock
                {
                    Text = summary.ContentSummary,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    FontSize = 12,
                    IsTextSelectionEnabled = true
                });
            }

            // Show summary dialog
            var summaryDialog = new ContentDialog
            {
                Title = "URL Summary - Update Description & Keywords",
                Content = new ScrollViewer
                {
                    Content = stackPanel,
                    MaxHeight = 550,
                    Width = 650
                },
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Save Changes",
                XamlRoot = Content.XamlRoot
            };

            var result = await summaryDialog.ShowAsync();

            // If user clicked "Save Changes", update the link's description and keywords
            if (result == ContentDialogResult.Primary)
            {
                var newDescription = descriptionTextBox.Text.Trim();
                var newKeywordsText = keywordsTextBox.Text.Trim();

                bool hasChanges = false;

                if (link.Description != newDescription)
                {
                    link.Description = newDescription;
                    hasChanges = true;
                }

                if (link.Keywords != newKeywordsText)
                {
                    link.Keywords = newKeywordsText;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    link.ModifiedDate = DateTime.Now;

                    // Save the category
                    var rootNode = GetRootCategoryNode(_contextMenuNode);
                    await _categoryService!.SaveCategoryAsync(rootNode);

                    StatusText.Text = $"Updated description and keywords for '{link.Title}'";

                    // Refresh the details view if this link is currently selected
                    if (LinksTreeView.SelectedNode == _contextMenuNode)
                    {
                        await _detailsViewService!.ShowLinkDetailsAsync(link, _contextMenuNode,
                            async () => await _catalogService!.CreateCatalogAsync(link, _contextMenuNode),
                            async () => await _catalogService!.RefreshCatalogAsync(link, _contextMenuNode),
                            null);
                    }
                }
                else
                {
                    StatusText.Text = "No changes made";
                }
            }
            else
            {
                StatusText.Text = "Ready";
            }
        }
        catch (Exception ex)
        {
            loadingDialog.Hide();
            
            var errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = $"An unexpected error occurred:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            
            StatusText.Text = "Ready";
        }
    }

    /// <summary>
    /// Shows a summary dialog for binary content (images, PDFs, etc.)
    /// </summary>
    private async Task ShowBinarySummaryDialog(LinkItem link, WebPageSummary summary)
    {
        var stackPanel = new StackPanel { Spacing = 12, Width = 500 };

        // Icon based on content type
        var icon = summary.MediaType switch
        {
            string s when s.StartsWith("image/") => "🖼️",
            string s when s.StartsWith("audio/") => "🎵",
            string s when s.StartsWith("video/") => "🎬",
            "application/pdf" => "📋",
            string s when s.Contains("zip") || s.Contains("compressed") => "📦",
            _ => "📄"
        };

        // Title
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"{icon} {summary.Title}",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // URL
        stackPanel.Children.Add(new TextBlock
        {
            Text = summary.Url,
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        });

        // Content type info
        var infoPanel = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(40, 255, 193, 7)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gold),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 8)
        };

        var infoContent = new StackPanel { Spacing = 4 };
        infoContent.Children.Add(new TextBlock
        {
            Text = "⚠️ Binary Content Detected",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
        });
        infoContent.Children.Add(new TextBlock
        {
            Text = summary.ContentSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            FontSize = 12
        });
        if (!string.IsNullOrEmpty(summary.MediaType))
        {
            infoContent.Children.Add(new TextBlock
            {
                Text = $"Content-Type: {summary.MediaType}",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                FontStyle = Windows.UI.Text.FontStyle.Italic
            });
        }

        infoPanel.Child = infoContent;
        stackPanel.Children.Add(infoPanel);

        // Separator
        stackPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Opacity = 0.3
        });

        // Current Description
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Current Description:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(link.Description) ? "(No description set)" : link.Description,
            TextWrapping = TextWrapping.Wrap,
            FontStyle = string.IsNullOrWhiteSpace(link.Description) ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                string.IsNullOrWhiteSpace(link.Description) ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.LightGray),
            Margin = new Thickness(0, 0, 0, 8),
            IsTextSelectionEnabled = true
        });

        // Editable Description
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Description (editable):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var descriptionTextBox = new TextBox
        {
            Text = !string.IsNullOrWhiteSpace(summary.Description) ? summary.Description : link.Description,
            PlaceholderText = "Enter description...",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        stackPanel.Children.Add(descriptionTextBox);

        // Show dialog
        var dialog = new ContentDialog
        {
            Title = "Binary Content Summary",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 450
            },
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Save Description",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var newDescription = descriptionTextBox.Text.Trim();

            if (link.Description != newDescription)
            {
                link.Description = newDescription;
                link.ModifiedDate = DateTime.Now;

                var rootNode = GetRootCategoryNode(_contextMenuNode!);
                await _categoryService!.SaveCategoryAsync(rootNode);

                StatusText.Text = $"Updated description for '{link.Title}'";

                if (LinksTreeView.SelectedNode == _contextMenuNode)
                {
                    await _detailsViewService!.ShowLinkDetailsAsync(link, _contextMenuNode!,
                        async () => await _catalogService!.CreateCatalogAsync(link, _contextMenuNode!),
                        async () => await _catalogService!.RefreshCatalogAsync(link, _contextMenuNode!),
                        null);
                }
            }
            else
            {
                StatusText.Text = "No changes made";
            }
        }
    }

    private async void CategoryMenu_SortBy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        // Build sort options UI
        var stackPanel = new StackPanel { Spacing = 8 };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select sort order for items in this category:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var sortComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var sortOptions = new[]
        {
            SortOption.NameAscending,
            SortOption.NameDescending,
            SortOption.DateAscending,
            SortOption.DateDescending,
            SortOption.SizeAscending,
            SortOption.SizeDescending
        };

        foreach (var option in sortOptions)
        {
            sortComboBox.Items.Add(SortingService.GetSortOptionDisplayName(option));
        }

        // Set current sort option as selected
        sortComboBox.SelectedIndex = Array.IndexOf(sortOptions, category.SortOrder);
        if (sortComboBox.SelectedIndex < 0) sortComboBox.SelectedIndex = 0;

        stackPanel.Children.Add(sortComboBox);

        var dialog = new ContentDialog
        {
            Title = $"Sort - {category.Name}",
            Content = stackPanel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var selectedOption = sortOptions[sortComboBox.SelectedIndex];

            // Apply sorting
            SortingService.SortCategoryChildren(_contextMenuNode, selectedOption);

            // Save the category
            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Sorted '{category.Name}' by {SortingService.GetSortOptionDisplayName(selectedOption)}";
        }
    }

    private async void LinkMenu_SortCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link || !link.IsDirectory)
            return;

        // Build sort options UI
        var stackPanel = new StackPanel { Spacing = 8 };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select sort order for catalog entries:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var sortComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var sortOptions = new[]
        {
            SortOption.NameAscending,
            SortOption.NameDescending,
            SortOption.DateAscending,
            SortOption.DateDescending,
            SortOption.SizeAscending,
            SortOption.SizeDescending
        };

        foreach (var option in sortOptions)
        {
            sortComboBox.Items.Add(SortingService.GetSortOptionDisplayName(option));
        }

        // Set current sort option as selected
        sortComboBox.SelectedIndex = Array.IndexOf(sortOptions, link.CatalogSortOrder);
        if (sortComboBox.SelectedIndex < 0) sortComboBox.SelectedIndex = 0;

        stackPanel.Children.Add(sortComboBox);

        // Option to apply to subdirectories
        var applyToSubdirsCheckBox = new CheckBox
        {
            Content = "Apply to all subdirectories",
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stackPanel.Children.Add(applyToSubdirsCheckBox);

        var dialog = new ContentDialog
        {
            Title = $"Sort Catalog - {link.Title}",
            Content = stackPanel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var selectedOption = sortOptions[sortComboBox.SelectedIndex];
            var applyToSubdirs = applyToSubdirsCheckBox.IsChecked ?? false;

            // Apply sorting
            if (applyToSubdirs)
            {
                SortingService.SortCatalogEntriesRecursive(_contextMenuNode, selectedOption);
            }
            else
            {
                SortingService.SortCatalogEntries(_contextMenuNode, selectedOption);
            }

            // Save the category
            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Sorted catalog for '{link.Title}' by {SortingService.GetSortOptionDisplayName(selectedOption)}";
        }
    }

    #region Tag Menu Handlers

    private async void CategoryMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagId)
            return;

        var tag = _tagService?.GetTag(tagId);
        if (tag == null)
            return;

        // Add tag to category
        if (!category.TagIds.Contains(tagId))
        {
            category.TagIds.Add(tagId);
            category.ModifiedDate = DateTime.Now;

            // Save the category
            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Refresh the tree node visual to show updated tag indicator
            _treeViewService!.RefreshCategoryNode(_contextMenuNode, category);

            // Refresh the detail view if this category is currently selected
            if (LinksTreeView.SelectedNode == _contextMenuNode)
            {
                _detailsViewService!.ShowCategoryHeader(category.Name, category.Description, category.Icon, category);
                await _detailsViewService.ShowCategoryDetailsAsync(category, _contextMenuNode);
            }

            StatusText.Text = $"Added tag '{tag.Name}' to category '{category.Name}'";
        }
    }

    private async void CategoryMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not CategoryItem category)
            return;

        if (category.TagIds.Count == 0)
        {
            StatusText.Text = "No tags to remove";
            return;
        }

        // Remove the last tag added
        var lastTagId = category.TagIds[^1];
        var tag = _tagService?.GetTag(lastTagId);
        
        category.TagIds.RemoveAt(category.TagIds.Count - 1);
        category.ModifiedDate = DateTime.Now;

        // Save the category
        var rootNode = GetRootCategoryNode(_contextMenuNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        // Refresh the tree node visual to show updated tag indicator
        _treeViewService!.RefreshCategoryNode(_contextMenuNode, category);

        // Refresh the detail view if this category is currently selected
        if (LinksTreeView.SelectedNode == _contextMenuNode)
        {
            _detailsViewService!.ShowCategoryHeader(category.Name, category.Description, category.Icon, category);
            await _detailsViewService.ShowCategoryDetailsAsync(category, _contextMenuNode);
        }

        var tagName = tag?.Name ?? "Unknown";
        StatusText.Text = $"Removed tag '{tagName}' from category '{category.Name}'";
    }

    private async void LinkMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagId)
            return;

        var tag = _tagService?.GetTag(tagId);
        if (tag == null)
            return;

        // Add tag to link
        if (!link.TagIds.Contains(tagId))
        {
            link.TagIds.Add(tagId);
            link.ModifiedDate = DateTime.Now;

            // Save the category
            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Refresh the tree node visual to show updated tag indicator
            _treeViewService!.RefreshLinkNode(_contextMenuNode, link);

            // Refresh the detail view if this link is currently selected
            if (LinksTreeView.SelectedNode == _contextMenuNode)
            {
                bool showLinkBadge = link.IsDirectory && link.FolderType == FolderLinkType.LinkOnly;
                _detailsViewService!.ShowLinkHeader(link.Title, link.Description, link.GetIcon(), showLinkBadge, 
                    link.FileSize, link.CreatedDate, link.ModifiedDate, link);
            }

            StatusText.Text = $"Added tag '{tag.Name}' to link '{link.Title}'";
        }
    }

    private async void LinkMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.TagIds.Count == 0)
        {
            StatusText.Text = "No tags to remove";
            return;
        }

        // Remove the last tag added
        var lastTagId = link.TagIds[^1];
        var tag = _tagService?.GetTag(lastTagId);
        
        link.TagIds.RemoveAt(link.TagIds.Count - 1);
        link.ModifiedDate = DateTime.Now;

        // Save the category
        var rootNode = GetRootCategoryNode(_contextMenuNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        // Refresh the tree node visual to show updated tag indicator
        _treeViewService!.RefreshLinkNode(_contextMenuNode, link);

        // Refresh the detail view if this link is currently selected
        if (LinksTreeView.SelectedNode == _contextMenuNode)
        {
            bool showLinkBadge = link.IsDirectory && link.FolderType == FolderLinkType.LinkOnly;
            _detailsViewService!.ShowLinkHeader(link.Title, link.Description, link.GetIcon(), showLinkBadge, 
                link.FileSize, link.CreatedDate, link.ModifiedDate, link);
        }

        var tagName = tag?.Name ?? "Unknown";
        StatusText.Text = $"Removed tag '{tagName}' from link '{link.Title}'";
    }

    #endregion
}
