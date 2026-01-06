using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
                        : null
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
            isRootCategory: false);

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
                    ModifiedDate = DateTime.Now
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
            isRootCategory: node.Parent == null,
            currentPasswordProtection: category.PasswordProtection,
            currentPasswordHash: category.OwnPasswordHash);

        if (result != null)
        {
            if (node.Parent == null && oldCategoryName != result.Name)
            {
                await _categoryService!.DeleteCategoryAsync(oldCategoryName);
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
                    : category.OwnPasswordHash // Keep existing password if not changed
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
                _detailsViewService!.ShowCategoryDetails(updatedCategory, newNode);
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
            if (node.Parent == null)
            {
                await _categoryService!.DeleteCategoryAsync(category.Name);
                LinksTreeView.RootNodes.Remove(node);
            }
            else
            {
                node.Parent.Children.Remove(node);
                var rootNode = GetRootCategoryNode(node.Parent);
                await _categoryService!.SaveCategoryAsync(rootNode);
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