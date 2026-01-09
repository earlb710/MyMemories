using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Dialogs; // Add this line
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
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
        }
        
        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: "Create New Category",
            currentName: null,
            currentDescription: null,
            currentIcon: null,
            isRootCategory: true,
            currentPasswordProtection: PasswordProtectionType.None,
            currentPasswordHash: null,
            currentIsBookmarkCategory: false,
            currentIsBookmarkLookup: false,
            currentIsAuditLoggingEnabled: false);

        if (result != null)
        {
            // Cache passwords before saving
            if (result.PasswordProtection == PasswordProtectionType.OwnPassword && result.OwnPassword != null)
            {
                _categoryService!.CacheCategoryPassword(result.Name, result.OwnPassword);
            }
            else if (result.PasswordProtection == PasswordProtectionType.GlobalPassword)
            {
                // Global password should already be cached from startup or Security Setup
                // If not cached yet, prompt for it now
                if (_configService != null && _configService.HasGlobalPassword())
                {
                    // Try to use it - if it fails, it will throw an error with helpful message
                    // The error will be caught and displayed to the user
                }
            }
            
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
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

            // Log the category creation to SYSTEM log (persists even if category is later deleted)
            if (_configService?.IsLoggingEnabled() == true)
            {
                // Try AuditLogService first
                if (_configService.AuditLogService != null)
                {
                    await _configService.AuditLogService.LogCategoryAddedToSystemLogAsync(result.Name, result.Description);
                }
                // Fallback to ErrorLogService if AuditLogService is not available
                else if (_configService.ErrorLogService != null)
                {
                    await _configService.ErrorLogService.LogWarningAsync(
                        $"Category created: {result.Name}" + (!string.IsNullOrEmpty(result.Description) ? $" - {result.Description}" : ""),
                        "MainWindow.CreateCategoryButton_Click");
                }
            }

            StatusText.Text = $"Created category: {result.Name}";
            
            // Update bookmark lookup categories in case new category has lookup enabled
            UpdateBookmarkLookupCategories();
        }
    }

    private async Task CreateSubCategoryAsync(TreeViewNode parentNode)
    {
        var parentCategoryPath = _treeViewService!.GetCategoryPath(parentNode);
        var parentCategory = parentNode.Content as CategoryItem;
        
        // Check if parent is a bookmark category - if so, inherit the flag
        bool parentIsBookmarkCategory = parentCategory?.IsBookmarkCategory ?? false;
        
        // Ensure _linkDialog has the latest _configService reference
        if (_linkDialog != null && _configService != null)
        {
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
        }
        
        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: $"Create Sub Category under '{parentCategoryPath}'",
            currentName: null,
            currentDescription: null,
            currentIcon: null,
            isRootCategory: false,
            currentPasswordProtection: PasswordProtectionType.None,
            currentPasswordHash: null,
            currentIsBookmarkCategory: parentIsBookmarkCategory);

        if (result != null)
        {
            var subCategoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
                    Icon = result.Icon,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsBookmarkCategory = result.IsBookmarkCategory, // Inherit from parent
                    IsBookmarkLookup = result.IsBookmarkLookup
                }
            };

            _treeViewService!.InsertSubCategoryNode(parentNode, subCategoryNode);
            await _categoryService!.SaveCategoryAsync(GetRootCategoryNode(parentNode));

            var fullPath = _treeViewService.GetCategoryPath(subCategoryNode);
            StatusText.Text = $"Created sub category: {fullPath}";
            
            // Update bookmark lookup categories in case new subcategory has lookup enabled
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

        // Check if node is a root category by checking if it's in RootNodes collection
        bool isRootCategory = LinksTreeView.RootNodes.Contains(node);

        // Check if category has non-URL children (files, folders, directories)
        bool hasNonUrlChildren = HasNonUrlChildrenRecursive(node);

        System.Diagnostics.Debug.WriteLine($"EditCategoryAsync: category='{category.Name}', node.Parent={node.Parent}, node in RootNodes={isRootCategory}, hasNonUrlChildren={hasNonUrlChildren}");

        // Ensure _linkDialog has the latest _configService reference
        if (_linkDialog != null && _configService != null)
        {
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
        }

        var result = await _linkDialog!.ShowCategoryDialogAsync(
            title: "Edit Category",
            currentName: category.Name,
            currentDescription: category.Description,
            currentIcon: category.Icon,
            isRootCategory: isRootCategory,
            currentPasswordProtection: category.PasswordProtection,
            currentPasswordHash: category.OwnPasswordHash,
            currentIsBookmarkCategory: category.IsBookmarkCategory,
            currentIsBookmarkLookup: category.IsBookmarkLookup,
            currentIsAuditLoggingEnabled: category.IsAuditLoggingEnabled,
            hasNonUrlChildren: hasNonUrlChildren);

        if (result != null)
        {
            bool categoryRenamed = isRootCategory && oldCategoryName != result.Name;
            bool passwordChanged = isRootCategory && oldPasswordProtection != result.PasswordProtection;
            
            if (categoryRenamed)
            {
                // Delete the old category file
                await _categoryService!.DeleteCategoryAsync(oldCategoryName);
                
                // Rename the audit log file if logging is enabled
                if (_configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
                {
                    await _configService.AuditLogService.RenameLogAsync(oldCategoryName, result.Name);
                    // Log the rename to SYSTEM log (for tracking category changes)
                    await _configService.AuditLogService.LogCategoryRenamedToSystemLogAsync(oldCategoryName, result.Name);
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
                                new PasswordBox
                                {
                                    Name = "GlobalPasswordInput",
                                    PlaceholderText = "Enter global password"
                                }
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
                        var passwordBox = (globalPasswordDialog.Content as StackPanel)
                            ?.Children.OfType<PasswordBox>()
                            .FirstOrDefault();

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
                                await DialogHelpers.ShowErrorAsync(Content.XamlRoot,
                                    "Incorrect Password",
                                    "The global password you entered is incorrect.");
                                return;
                            }
                        }
                        else
                        {
                            await DialogHelpers.ShowErrorAsync(Content.XamlRoot,
                                "Password Required",
                                "You must enter the global password to save this category.");
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

            // Log password change to SYSTEM log if it occurred (and not already logged as part of rename)
            if (passwordChanged && !categoryRenamed && _configService?.IsLoggingEnabled() == true && _configService.AuditLogService != null)
            {
                await _configService.AuditLogService.LogCategoryPasswordChangedToSystemLogAsync(result.Name, oldPasswordProtection, result.PasswordProtection);
            }

            StatusText.Text = $"Updated category: {result.Name}";
            
            // Update bookmark lookup categories in case IsBookmarkLookup changed
            UpdateBookmarkLookupCategories();

            if (LinksTreeView.SelectedNode == newNode)
            {
                await _detailsViewService!.ShowCategoryDetailsAsync(updatedCategory, newNode);
                _detailsViewService.ShowCategoryHeader(_treeViewService!.GetCategoryPath(newNode), updatedCategory.Description, updatedCategory.Icon);
                HeaderViewerScroll.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task DeleteCategoryAsync(CategoryItem category, TreeViewNode node)
    {
        int totalLinks = CountAllLinks(node);
        int totalSubcategories = CountAllSubcategories(node);
        
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Category",
            Content = $"Are you sure you want to delete '{category.Icon} {category.Name}' and all its {totalLinks} link(s) and subcategories?",
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

            // Log the removal to SYSTEM log BEFORE actually deleting (so it persists)
            if (isRootCategory)
            {
                // Debug: Check logging state
                System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] Logging enabled: {_configService?.IsLoggingEnabled()}");
                System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] AuditLogService: {_configService?.AuditLogService != null}");
                System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] ErrorLogService: {_configService?.ErrorLogService != null}");
                System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] Log directory: {_configService?.LogDirectory}");
                
                if (_configService?.IsLoggingEnabled() == true)
                {
                    // Try AuditLogService first (writes to system.log)
                    if (_configService.AuditLogService != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] Calling LogCategoryRemovedToSystemLogAsync for '{category.Name}'");
                        await _configService.AuditLogService.LogCategoryRemovedToSystemLogAsync(category.Name, totalLinks, totalSubcategories);
                        System.Diagnostics.Debug.WriteLine($"[DeleteCategoryAsync] LogCategoryRemovedToSystemLogAsync completed");
                    }
                    
                    // Also log to error.log as a backup (more visible)
                    if (_configService.ErrorLogService != null)
                    {
                        await _configService.ErrorLogService.LogWarningAsync(
                            $"Category removed: {category.Name} (Links: {totalLinks}, Subcategories: {totalSubcategories})",
                            "MainWindow.DeleteCategoryAsync");
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
            StatusText.Text = $"Deleted category: {category.Name}";
        }
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
}