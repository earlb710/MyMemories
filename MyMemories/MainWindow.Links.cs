using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var categories = LinksTreeView.RootNodes
            .Where(n => n.Content is CategoryItem)
            .Select(n => new CategoryNode
            {
                Name = ((CategoryItem)n.Content).Name,
                Node = n
            })
            .ToList();

        if (categories.Count == 0)
        {
            StatusText.Text = "Please create a category first";
            return;
        }

        var selectedCategory = _treeViewService!.GetParentCategoryNode(LinksTreeView.SelectedNode) ?? _lastUsedCategory;
        var selectedCategoryNode = selectedCategory != null
            ? new CategoryNode { Name = ((CategoryItem)selectedCategory.Content).Name, Node = selectedCategory }
            : null;

        var result = await _linkDialog!.ShowAddAsync(categories, selectedCategoryNode);

        if (result?.CategoryNode != null)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(result.CategoryNode);
            
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
            _lastUsedCategory = result.CategoryNode;

            // Update parent categories' ModifiedDate and save
            await UpdateParentCategoriesAndSaveAsync(result.CategoryNode);

            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";
        }
    }

    private async Task EditLinkAsync(LinkItem link, TreeViewNode node)
    {
        var editResult = await _linkDialog!.ShowEditAsync(link);
        
        if (editResult != null)
        {
            link.Title = editResult.Title;
            link.Url = editResult.Url;
            link.Description = editResult.Description;
            link.IsDirectory = editResult.IsDirectory;
            link.CategoryPath = _treeViewService!.GetCategoryPath(node.Parent);
            link.ModifiedDate = DateTime.Now;
            link.FolderType = editResult.FolderType;
            link.FileFilters = editResult.FileFilters;

            var newNode = _treeViewService!.RefreshLinkNode(node, link);

            if (_contextMenuNode == node)
            {
                _contextMenuNode = newNode;
            }

            var parentCategory = newNode.Parent;
            if (parentCategory != null)
            {
                // Update parent categories' ModifiedDate and save
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
            }

            StatusText.Text = $"Updated link: {editResult.Title}";

            if (LinksTreeView.SelectedNode == newNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    newNode,
                    async () => await _catalogService!.CreateCatalogAsync(link, newNode),
                    async () => await _catalogService!.RefreshCatalogAsync(link, newNode)
                );
            }
        }
    }

    private async Task MoveLinkAsync(LinkItem link, TreeViewNode node)
    {
        if (node.Parent == null || node.Parent.Content is not CategoryItem)
        {
            StatusText.Text = "Cannot move link: Invalid parent category";
            return;
        }

        var currentCategoryNode = node.Parent;
        var allCategories = GetAllCategoriesFlat();
        
        var result = await _linkDialog!.ShowMoveLinkAsync(allCategories, currentCategoryNode, link.Title);

        if (result?.TargetCategoryNode != null)
        {
            var targetCategoryNode = result.TargetCategoryNode;
            var targetCategoryPath = _treeViewService!.GetCategoryPath(targetCategoryNode);

            link.CategoryPath = targetCategoryPath;
            currentCategoryNode.Children.Remove(node);
            targetCategoryNode.Children.Add(node);
            targetCategoryNode.IsExpanded = true;

            // Update ModifiedDate for both source and target category hierarchies
            UpdateParentCategoriesModifiedDate(currentCategoryNode);
            UpdateParentCategoriesModifiedDate(targetCategoryNode);

            var sourceRootNode = GetRootCategoryNode(currentCategoryNode);
            var targetRootNode = GetRootCategoryNode(targetCategoryNode);

            await _categoryService!.SaveCategoryAsync(sourceRootNode);
            
            if (sourceRootNode != targetRootNode)
            {
                await _categoryService!.SaveCategoryAsync(targetRootNode);
            }

            StatusText.Text = $"Moved link '{link.Title}' to '{targetCategoryPath}'";

            if (LinksTreeView.SelectedNode == node)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    node,
                    async () => await _catalogService!.CreateCatalogAsync(link, node),
                    async () => await _catalogService!.RefreshCatalogAsync(link, node)
                );
            }
        }
    }

    private async Task DeleteLinkAsync(LinkItem link, TreeViewNode node)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Remove Link",
            Content = $"Remove link '{link.Title}'?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary && node.Parent != null)
        {
            var parentCategory = node.Parent;
            parentCategory.Children.Remove(node);

            // Update parent categories' ModifiedDate and save
            await UpdateParentCategoriesAndSaveAsync(parentCategory);

            if (LinksTreeView.SelectedNode == node)
            {
                ShowWelcome();
            }

            StatusText.Text = $"Removed link: {link.Title}";
        }
    }
}