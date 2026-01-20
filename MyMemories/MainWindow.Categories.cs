using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Dialogs;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async void CreateCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        // Ensure _linkDialog has the latest _configService reference
        if (_linkDialog != null && _configService != null)
        {
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
            _linkDialog.SetGitConfigCallback(ShowGitSetupDialogAsync);
        }
        
        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: "Create New Category",
            options: new CategoryDialogOptions
            {
                IsRootCategory = true,
                CurrentIsAuditLoggingEnabled = false
            });

        if (result != null)
        {
            // Cache passwords before saving
            if (result.PasswordProtection == PasswordProtectionType.OwnPassword && result.OwnPassword != null)
            {
                _categoryService!.CacheCategoryPassword(result.Name, result.OwnPassword);
            }
            
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
                    Keywords = result.Keywords,
                    Icon = result.Icon,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    PasswordProtection = result.PasswordProtection,
                    OwnPasswordHash = result.OwnPassword != null 
                        ? PasswordUtilities.HashPassword(result.OwnPassword) 
                        : null,
                    IsBookmarkCategory = result.IsBookmarkCategory,
                    IsBookmarkLookup = result.IsBookmarkLookup,
                    IsAuditLoggingEnabled = result.IsAuditLoggingEnabled
                }
            };

            _treeViewService!.InsertCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(categoryNode);

            // Log the category creation to the category's own log
            if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
            {
                await _configService.AuditLogService.LogCategoryAddedAsync(result.Name, result.Description);
            }

            StatusText.Text = $"Created category: {result.Name}";
            UpdateBookmarkLookupCategories();
        }
    }

    private async Task CreateSubCategoryAsync(TreeViewNode parentNode)
    {
        var parentCategoryPath = _treeViewService!.GetCategoryPath(parentNode);
        var parentCategory = parentNode.Content as CategoryItem;
        bool parentIsBookmarkCategory = parentCategory?.IsBookmarkCategory ?? false;
        
        // Ensure _linkDialog has the latest _configService reference
        if (_linkDialog != null && _configService != null)
        {
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
            _linkDialog.SetGitConfigCallback(ShowGitSetupDialogAsync);
        }
        
        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: $"Create Sub Category under '{parentCategoryPath}'",
            options: new CategoryDialogOptions
            {
                IsRootCategory = false,
                CurrentIsBookmarkCategory = parentIsBookmarkCategory
            });

        if (result != null)
        {
            var subCategoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
                    Keywords = result.Keywords,
                    Icon = result.Icon,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsBookmarkCategory = result.IsBookmarkCategory,
                    IsBookmarkLookup = result.IsBookmarkLookup
                }
            };

            _treeViewService!.InsertSubCategoryNode(parentNode, subCategoryNode);
            await _categoryService!.SaveCategoryAsync(GetRootCategoryNode(parentNode));

            var fullPath = _treeViewService.GetCategoryPath(subCategoryNode);
            
            // Log subcategory creation to the root category's log
            if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
            {
                var rootNode = GetRootCategoryNode(parentNode);
                if (rootNode?.Content is CategoryItem rootCategory)
                {
                    await _configService.AuditLogService.LogAsync(
                        rootCategory.Name,
                        Services.AuditLogType.Add,
                        $"Subcategory {result.Name} created",
                        $"Path: {fullPath}");
                }
            }
            
            StatusText.Text = $"Created sub category: {fullPath}";
            UpdateBookmarkLookupCategories();
        }
    }

    private List<CategoryNode> GetAllCategoriesFlat()
    {
        var allCategories = new List<CategoryNode>();
        
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem)
            {
                var categoriesWithSubs = _treeViewService!.GetCategoryWithSubcategories(rootNode);
                allCategories.AddRange(categoriesWithSubs);
            }
        }
        
        return allCategories;
    }

    private async Task EditCategoryAsync(CategoryItem category, TreeViewNode node)
    {
        string oldCategoryName = category.Name;
        var oldPasswordProtection = category.PasswordProtection;
        bool isRootCategory = LinksTreeView.RootNodes.Contains(node);
        bool hasNonUrlChildren = HasNonUrlChildrenRecursive(node);

        // Ensure _linkDialog has the latest _configService reference
        if (_linkDialog != null && _configService != null)
        {
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
            _linkDialog.SetGitConfigCallback(ShowGitSetupDialogAsync);
        }

        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: "Edit Category",
            options: new CategoryDialogOptions
            {
                CurrentName = category.Name,
                CurrentDescription = category.Description,
                CurrentIcon = category.Icon,
                CurrentKeywords = category.Keywords,
                IsRootCategory = isRootCategory,
                CurrentPasswordProtection = category.PasswordProtection,
                CurrentPasswordHash = category.OwnPasswordHash,
                CurrentIsBookmarkCategory = category.IsBookmarkCategory,
                CurrentIsBookmarkLookup = category.IsBookmarkLookup,
                CurrentIsAuditLoggingEnabled = category.IsAuditLoggingEnabled,
                HasNonUrlChildren = hasNonUrlChildren
            });

        if (result != null)
        {
            bool categoryRenamed = oldCategoryName != result.Name;
            bool passwordChanged = isRootCategory && oldPasswordProtection != result.PasswordProtection;
            
            if (categoryRenamed && isRootCategory)
            {
                // Delete the old category file
                await _categoryService!.DeleteCategoryAsync(oldCategoryName);
                
                // Rename the audit log file if logging is enabled
                if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
                {
                    await _configService.AuditLogService.RenameLogAsync(oldCategoryName, result.Name);
                    // Log the rename to the category's own log
                    await _configService.AuditLogService.LogCategoryRenamedAsync(result.Name, oldCategoryName, result.Name);
                }
            }
            else if (categoryRenamed && !isRootCategory)
            {
                // Subcategory renamed - log to root category's log
                if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
                {
                    var root = GetRootCategoryNode(node);
                    if (root?.Content is CategoryItem rootCat)
                    {
                        var newPath = _treeViewService!.GetCategoryPath(node).Replace(oldCategoryName, result.Name);
                        await _configService.AuditLogService.LogAsync(
                            rootCat.Name,
                            Services.AuditLogType.Change,
                            $"Subcategory renamed from '{oldCategoryName}' to '{result.Name}'",
                            $"Path: {newPath}");
                    }
                }
            }

            // Cache passwords before saving
            if (result.PasswordProtection == PasswordProtectionType.OwnPassword && result.OwnPassword != null)
            {
                _categoryService!.CacheCategoryPassword(result.Name, result.OwnPassword);
            }
            else if (result.PasswordProtection == PasswordProtectionType.GlobalPassword)
            {
                // Check if global password is already cached - only prompt if not cached
                var cachedGlobalPassword = _categoryService?.GetCachedGlobalPassword();
                if (string.IsNullOrEmpty(cachedGlobalPassword))
                {
                    // Prompt user for global password to cache it
                    var globalPasswordDialog = new ContentDialog
                    {
                        Title = "Global Password Required",
                        Content = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "This category uses the global password. Please enter it to continue:",
                                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                                },
                                new PasswordBox { PlaceholderText = "Enter global password" }
                            }
                        },
                        PrimaryButtonText = "OK",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = Content.XamlRoot
                    };

                    var dialogResult = await globalPasswordDialog.ShowAsync();

                    if (dialogResult == ContentDialogResult.Primary)
                    {
                        var passwordBox = (globalPasswordDialog.Content as StackPanel)?.Children.OfType<PasswordBox>().FirstOrDefault();

                        if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                        {
                            // Verify the password is correct
                            var enteredPasswordHash = PasswordUtilities.HashPassword(passwordBox.Password);
                            if (enteredPasswordHash == _configService!.GlobalPasswordHash)
                            {
                                // Cache the global password
                                _categoryService!.CacheGlobalPassword(passwordBox.Password);
                            }
                            else
                            {
                                await DialogHelpers.ShowErrorAsync(Content.XamlRoot, "Incorrect Password", "The global password you entered is incorrect.");
                                return;
                            }
                        }
                        else
                        {
                            await DialogHelpers.ShowErrorAsync(Content.XamlRoot, "Password Required", "You must enter the global password to save this category.");
                            return;
                        }
                    }
                    else
                    {
                        // User cancelled
                        return;
                    }
                }
                // If global password is already cached, no need to prompt again
            }

            var updatedCategory = new CategoryItem
            {
                Name = result.Name,
                Description = result.Description,
                Keywords = result.Keywords,
                Icon = result.Icon,
                CreatedDate = category.CreatedDate,
                ModifiedDate = DateTime.Now,
                PasswordProtection = result.PasswordProtection,
                OwnPasswordHash = result.OwnPassword != null
                    ? PasswordUtilities.HashPassword(result.OwnPassword)
                    : category.OwnPasswordHash,
                IsBookmarkCategory = result.IsBookmarkCategory,
                IsBookmarkLookup = result.IsBookmarkLookup,
                IsAuditLoggingEnabled = result.IsAuditLoggingEnabled
            };

            var newNode = _treeViewService!.RefreshCategoryNode(node, updatedCategory);

            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = newNode;
            }

            var rootNode = GetRootCategoryNode(newNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Log password change to category's log if it occurred (and not already logged as part of rename)
            if (passwordChanged && !categoryRenamed && _configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
            {
                await _configService.AuditLogService.LogCategoryPasswordChangedAsync(result.Name, oldPasswordProtection, result.PasswordProtection);
            }

            StatusText.Text = $"Updated category: {result.Name}";
            UpdateBookmarkLookupCategories();

            if (LinksTreeView.SelectedNode == newNode)
            {
                await _detailsViewService!.ShowCategoryDetailsAsync(updatedCategory, newNode);
                _detailsViewService.ShowCategoryHeader(_treeViewService!.GetCategoryPath(newNode), updatedCategory.Description, updatedCategory.Icon, updatedCategory);
                HeaderViewerScroll.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task DeleteCategoryAsync(CategoryItem category, TreeViewNode node)
    {
        int totalLinks = CountAllLinks(node);
        int totalSubcategories = CountAllSubcategories(node);
        
        // Build a detailed message showing what will be deleted
        string deleteMessage = $"Are you sure you want to delete '{category.Icon} {category.Name}'?";
        
        if (totalSubcategories > 0 || totalLinks > 0)
        {
            deleteMessage += "\n\nThis will delete:";
            if (totalLinks > 0)
            {
                deleteMessage += $"\n  • {totalLinks} link{(totalLinks == 1 ? "" : "s")}";
            }
            if (totalSubcategories > 0)
            {
                deleteMessage += $"\n  • {totalSubcategories} subcategor{(totalSubcategories == 1 ? "y" : "ies")}";
                // Count links in subcategories
                int subcategoryLinks = 0;
                foreach (var child in node.Children)
                {
                    if (child.Content is CategoryItem)
                    {
                        subcategoryLinks += CountAllLinks(child);
                    }
                }
                if (subcategoryLinks > 0)
                {
                    deleteMessage += $"\n  • {subcategoryLinks} link{(subcategoryLinks == 1 ? "" : "s")} within subcategories";
                }
            }
        }
        
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Category",
            Content = new TextBlock
            {
                Text = deleteMessage,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Check if this is a root category by checking the RootNodes collection
            bool isRootCategory = LinksTreeView.RootNodes.Contains(node);
            
            // Log the removal - for root categories log to their own log, for subcategories log to root's log
            if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
            {
                if (isRootCategory)
                {
                    // Root category - log to its own log before deletion
                    
                    // Build detailed deletion summary
                    string deletionDetails = $"Links: {totalLinks}, Subcategories: {totalSubcategories}";
                    if (totalSubcategories > 0)
                    {
                        // Log each subcategory being deleted
                        var subcategories = GetAllSubcategoriesRecursive(node);
                        if (subcategories.Any())
                        {
                            deletionDetails += $" (Subcategories: {string.Join(", ", subcategories.Select(s => s.Name))})";
                        }
                    }
                    
                    await _configService.AuditLogService.LogCategoryRemovedAsync(category.Name, totalLinks, totalSubcategories);
                }
                else
                {
                    // Subcategory - log to the root category's log
                    var rootNode = GetRootCategoryNode(node);
                    if (rootNode?.Content is CategoryItem rootCategory)
                    {
                        var categoryPath = _treeViewService!.GetCategoryPath(node);
                        
                        string deletionDetails = $"Path: {categoryPath}";
                        if (totalLinks > 0 || totalSubcategories > 0)
                        {
                            deletionDetails += $", Links: {totalLinks}, Subcategories: {totalSubcategories}";
                        }
                        
                        await _configService.AuditLogService.LogAsync(
                            rootCategory.Name,
                            Services.AuditLogType.Remove,
                            $"Subcategory {category.Name} deleted",
                            deletionDetails);
                    }
                }
            }

            if (isRootCategory)
            {
                // Remove from RootNodes and delete the category file
                await _categoryService!.DeleteCategoryAsync(category.Name);
                LinksTreeView.RootNodes.Remove(node);
            }
            else
            {
                // It's a subcategory - GET ROOT BEFORE REMOVING
                if (node.Parent != null)
                {
                    TreeViewNode? rootNode = null;
                    
                    try
                    {
                        // CRITICAL: Get root node BEFORE removing the child
                        rootNode = GetRootCategoryNode(node.Parent);
                    }
                    catch (Exception ex)
                    {
                        LogUtilities.LogError("MainWindow.DeleteCategoryAsync", 
                            $"Error getting root node for subcategory '{category.Name}'", ex);
                        await ShowErrorDialogAsync("Delete Error", 
                            $"Cannot find root category: {ex.Message}");
                        return;
                    }
                    
                    // Now remove the child from parent
                    node.Parent.Children.Remove(node);
                    
                    // Save using the root node we got earlier
                    if (rootNode != null)
                    {
                        await _categoryService!.SaveCategoryAsync(rootNode);
                    }
                }
                else
                {
                    // Safety fallback - shouldn't happen but handle it
                    LogUtilities.LogError("MainWindow.DeleteCategoryAsync", 
                        $"Subcategory '{category.Name}' has no parent - cannot delete safely");
                    await ShowErrorDialogAsync("Delete Error", 
                        "Cannot delete category: Invalid category structure.");
                    return;
                }
            }

            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = null;
            }

            ShowWelcome();
            
            // Update status message with deletion details
            string statusMessage = $"Deleted category: {category.Name}";
            if (totalLinks > 0 || totalSubcategories > 0)
            {
                statusMessage += " (";
                if (totalLinks > 0)
                {
                    statusMessage += $"{totalLinks} link{(totalLinks == 1 ? "" : "s")}";
                }
                if (totalSubcategories > 0)
                {
                    if (totalLinks > 0) statusMessage += ", ";
                    statusMessage += $"{totalSubcategories} subcategor{(totalSubcategories == 1 ? "y" : "ies")}";
                }
                statusMessage += ")";
            }
            StatusText.Text = statusMessage;
        }
    }

    /// <summary>
    /// Gets all subcategories recursively from a node as a flat list.
    /// </summary>
    private List<CategoryItem> GetAllSubcategoriesRecursive(TreeViewNode node)
    {
        var subcategories = new List<CategoryItem>();
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem subCategory)
            {
                subcategories.Add(subCategory);
                subcategories.AddRange(GetAllSubcategoriesRecursive(child));
            }
        }
        return subcategories;
    }

    /// <summary>
    /// Counts all subcategories recursively in a node.
    /// </summary>
    private int CountAllSubcategories(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem)
            {
                count++;
                count += CountAllSubcategories(child);
            }
        }
        return count;
    }

    private int CountAllLinks(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem)
            {
                count++;
            }
            else if (child.Content is CategoryItem)
            {
                count += CountAllLinks(child);
            }
        }
        return count;
    }

    /// <summary>
    /// Checks if a category node has any non-URL children (files, folders, directories).
    /// This is used to determine if the "URL Bookmarks Only" option should be hidden.
    /// </summary>
    private bool HasNonUrlChildrenRecursive(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Skip catalog entries - they don't count as regular children
                if (link.IsCatalogEntry)
                    continue;

                // Check if this is a non-URL link (file or directory)
                if (link.IsDirectory)
                    return true;

                // Check if URL is a file path (not a web URL)
                if (!string.IsNullOrEmpty(link.Url))
                {
                    // If URL is a local file path, it's not a web URL
                    if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                    {
                        if (uri.IsFile || uri.Scheme == "file")
                            return true;
                    }
                    else
                    {
                        // If it's not a valid URI at all, check if it looks like a file path
                        if (link.Url.Contains(":\\") || link.Url.StartsWith("\\\\") || link.Url.StartsWith("/"))
                            return true;
                    }
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively check subcategories
                if (HasNonUrlChildrenRecursive(child))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Opens a file picker to select and load a category JSON file.
    /// </summary>
    private async void OpenCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create file picker
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            
            // Get the window handle for the picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            // Show picker
            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                StatusText.Text = "Category selection cancelled";
                return;
            }
            
            StatusText.Text = $"Loading category: {file.Name}";
            
            // Read and parse the JSON file with proper enum handling
            var json = await Windows.Storage.FileIO.ReadTextAsync(file);
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var categoryData = System.Text.Json.JsonSerializer.Deserialize<CategoryData>(json, jsonOptions);
            
            if (categoryData == null || string.IsNullOrEmpty(categoryData.Name))
            {
                await DialogUtilities.ShowErrorAsync(
                    Content.XamlRoot,
                    "Invalid Category File",
                    "The selected file is not a valid category JSON file.");
                StatusText.Text = "Ready";
                return;
            }
            
            var categoryName = categoryData.Name;
            
            // Check if category already exists
            var existingNode = LinksTreeView.RootNodes
                .FirstOrDefault(n => n.Content is CategoryItem existing && existing.Name == categoryName);
            
            if (existingNode != null)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Category Already Loaded",
                    Content = $"The category '{categoryName}' is already loaded. Do you want to reload it?",
                    PrimaryButtonText = "Reload",
                    CloseButtonText = "Cancel",
                    XamlRoot = Content.XamlRoot
                };
                
                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    StatusText.Text = "Ready";
                    return;
                }
                
                // Remove existing node (file will be overwritten)
                LinksTreeView.RootNodes.Remove(existingNode);
            }
            
            // Get working directory
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMemories",
                "Categories"
            );
            Directory.CreateDirectory(appDataFolder);
            
            // Determine the target filename - use the category name for consistency
            var sanitizedName = FileUtilities.SanitizeFileName(categoryName);
            var targetFileName = sanitizedName + ".json";
            var targetPath = Path.Combine(appDataFolder, targetFileName);
            
            // Copy/rename file to working directory with the category name as filename
            if (Path.GetFullPath(file.Path) != Path.GetFullPath(targetPath))
            {
                // If target exists and is different from source, delete it first
                if (File.Exists(targetPath) && Path.GetFileName(file.Path) != targetFileName)
                {
                    File.Delete(targetPath);
                }
                
                File.Copy(file.Path, targetPath, overwrite: true);
                
                // Delete the original file if it's in the Categories folder but has a different name
                if (Path.GetDirectoryName(file.Path) == appDataFolder && 
                    Path.GetFileName(file.Path) != targetFileName)
                {
                    try
                    {
                        File.Delete(file.Path);
                    }
                    catch
                    {
                        // Ignore errors - file might be in use or read-only
                    }
                }
            }
            
            // Create the category node manually from the data
            var categoryItem = new CategoryItem
            {
                Name = categoryData.Name,
                Description = categoryData.Description ?? string.Empty,
                Icon = categoryData.Icon ?? "??",
                Keywords = categoryData.Keywords ?? string.Empty,
                TagIds = categoryData.TagIds ?? new List<string>(),
                Ratings = categoryData.Ratings?.Select(r => new RatingValue
                {
                    Rating = r.Rating,
                    Score = r.Score,
                    Reason = r.Reason,
                    CreatedDate = r.CreatedDate,
                    ModifiedDate = r.ModifiedDate
                }).ToList() ?? new List<RatingValue>(),
                CreatedDate = categoryData.CreatedDate ?? DateTime.Now,
                ModifiedDate = categoryData.ModifiedDate ?? DateTime.Now,
                PasswordProtection = categoryData.PasswordProtection,
                OwnPasswordHash = categoryData.OwnPasswordHash,
                SortOrder = categoryData.SortOrder,
                IsBookmarkImport = categoryData.IsBookmarkImport,
                SourceBrowserType = categoryData.SourceBrowserType,
                SourceBrowserName = categoryData.SourceBrowserName,
                IsBookmarkCategory = categoryData.IsBookmarkCategory,
                IsBookmarkLookup = categoryData.IsBookmarkLookup,
                IsAuditLoggingEnabled = categoryData.IsAuditLoggingEnabled
                // Note: SourceFileName is no longer needed - file is renamed to match category name
            };
            
            var categoryNode = new TreeViewNode { Content = categoryItem };
            
            // Load links
            if (categoryData.Links != null)
            {
                foreach (var linkData in categoryData.Links)
                {
                    var linkItem = new LinkItem
                    {
                        Title = linkData.Title,
                        Url = linkData.Url,
                        Description = linkData.Description ?? string.Empty,
                        Keywords = linkData.Keywords ?? string.Empty,
                        TagIds = linkData.TagIds ?? new List<string>(),
                        Ratings = linkData.Ratings?.Select(r => new RatingValue
                        {
                            Rating = r.Rating,
                            Score = r.Score,
                            Reason = r.Reason,
                            CreatedDate = r.CreatedDate,
                            ModifiedDate = r.ModifiedDate
                        }).ToList() ?? new List<RatingValue>(),
                        IsDirectory = linkData.IsDirectory ?? false,
                        CategoryPath = categoryName,
                        CreatedDate = linkData.CreatedDate ?? DateTime.Now,
                        ModifiedDate = linkData.ModifiedDate ?? DateTime.Now
                    };
                    
                    categoryNode.Children.Add(new TreeViewNode { Content = linkItem });
                }
            }
            
            // Load subcategories recursively
            if (categoryData.SubCategories != null)
            {
                foreach (var subCategoryData in categoryData.SubCategories)
                {
                    LoadSubCategoryRecursive(categoryNode, subCategoryData, categoryName);
                }
            }
            
            // Add to tree view
            LinksTreeView.RootNodes.Insert(0, categoryNode);
            categoryNode.IsExpanded = true;
            LinksTreeView.SelectedNode = categoryNode;
            
            // Count total items including subcategories
            int totalItems = CountTotalItems(categoryNode);
            StatusText.Text = $"Loaded category: {categoryName} ({totalItems} items)";
            UpdateBookmarkLookupCategories();
        }
        catch (Exception ex)
        {
            await DialogUtilities.ShowErrorAsync(
                Content.XamlRoot,
                "Failed to Open Category",
                $"An error occurred while loading the category:\n{ex.Message}");
            StatusText.Text = "Ready";
        }
    }

    /// <summary>
    /// Opens the categories folder in Windows Explorer.
    /// </summary>
    private void CategoryMenu_OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the app data folder for categories
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMemories",
                "Categories"
            );
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(appDataFolder);

            // Open the folder in Windows Explorer
            System.Diagnostics.Process.Start("explorer.exe", appDataFolder);
            StatusText.Text = $"Opened categories folder";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Recursively loads subcategories and their links from CategoryData.
    /// </summary>
    private void LoadSubCategoryRecursive(TreeViewNode parentNode, CategoryData subCategoryData, string rootCategoryName)
    {
        // Create subcategory item
        var subCategoryItem = new CategoryItem
        {
            Name = subCategoryData.Name,
            Description = subCategoryData.Description ?? string.Empty,
            Icon = subCategoryData.Icon ?? "??",
            Keywords = subCategoryData.Keywords ?? string.Empty,
            TagIds = subCategoryData.TagIds ?? new List<string>(),
            Ratings = subCategoryData.Ratings?.Select(r => new RatingValue
            {
                Rating = r.Rating,
                Score = r.Score,
                Reason = r.Reason,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate
            }).ToList() ?? new List<RatingValue>(),
            CreatedDate = subCategoryData.CreatedDate ?? DateTime.Now,
            ModifiedDate = subCategoryData.ModifiedDate ?? DateTime.Now,
            SortOrder = subCategoryData.SortOrder,
            IsBookmarkCategory = subCategoryData.IsBookmarkCategory,
            IsBookmarkLookup = subCategoryData.IsBookmarkLookup
        };

        var subCategoryNode = new TreeViewNode { Content = subCategoryItem };

        // Load links for this subcategory
        if (subCategoryData.Links != null)
        {
            foreach (var linkData in subCategoryData.Links)
            {
                var linkItem = new LinkItem
                {
                    Title = linkData.Title,
                    Url = linkData.Url,
                    Description = linkData.Description ?? string.Empty,
                    Keywords = linkData.Keywords ?? string.Empty,
                    TagIds = linkData.TagIds ?? new List<string>(),
                    Ratings = linkData.Ratings?.Select(r => new RatingValue
                    {
                        Rating = r.Rating,
                        Score = r.Score,
                        Reason = r.Reason,
                        CreatedDate = r.CreatedDate,
                        ModifiedDate = r.ModifiedDate
                    }).ToList() ?? new List<RatingValue>(),
                    IsDirectory = linkData.IsDirectory ?? false,
                    CategoryPath = _treeViewService!.GetCategoryPath(parentNode) + "/" + subCategoryData.Name,
                    CreatedDate = linkData.CreatedDate ?? DateTime.Now,
                    ModifiedDate = linkData.ModifiedDate ?? DateTime.Now
                };

                subCategoryNode.Children.Add(new TreeViewNode { Content = linkItem });
            }
        }

        // Recursively load nested subcategories
        if (subCategoryData.SubCategories != null)
        {
            foreach (var nestedSubCategoryData in subCategoryData.SubCategories)
            {
                LoadSubCategoryRecursive(subCategoryNode, nestedSubCategoryData, rootCategoryName);
            }
        }

        // Add subcategory to parent
        parentNode.Children.Add(subCategoryNode);
    }

    /// <summary>
    /// Counts total items (links and subcategories) recursively in a node.
    /// </summary>
    private int CountTotalItems(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            count++;
            if (child.Content is CategoryItem)
            {
                count += CountTotalItems(child);
            }
        }
        return count;
    }
}
