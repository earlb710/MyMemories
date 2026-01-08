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
            currentPasswordHash: null);

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
                    IsBookmarkLookup = result.IsBookmarkLookup
                }
            };

            _treeViewService!.InsertCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(categoryNode);

            StatusText.Text = $"Created category: {result.Name}";
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

        // Check if node is a root category by checking if it's in RootNodes collection
        bool isRootCategory = LinksTreeView.RootNodes.Contains(node);

        System.Diagnostics.Debug.WriteLine($"EditCategoryAsync: category='{category.Name}', node.Parent={node.Parent}, node in RootNodes={isRootCategory}");

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
            currentIsBookmarkLookup: category.IsBookmarkLookup);

        if (result != null)
        {
            if (isRootCategory && oldCategoryName != result.Name)
            {
                await _categoryService!.DeleteCategoryAsync(oldCategoryName);
            }

            // Cache passwords before saving
            if (result.PasswordProtection == PasswordProtectionType.OwnPassword && result.OwnPassword != null)
            {
                _categoryService!.CacheCategoryPassword(result.Name, result.OwnPassword);
            }
            else if (result.PasswordProtection == PasswordProtectionType.GlobalPassword)
            {
                // Check if global password is already cached
                // We need to verify by attempting to use it, or prompt for it
                if (_configService != null && _configService.HasGlobalPassword())
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
                            if (enteredPasswordHash == _configService.GlobalPasswordHash)
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
                IsBookmarkLookup = result.IsBookmarkLookup
            };

            var newNode = _treeViewService!.RefreshCategoryNode(node, updatedCategory);

            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = newNode;
            }

            var rootNode = GetRootCategoryNode(newNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
            StatusText.Text = $"Updated category: {result.Name}";

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
}