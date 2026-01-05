using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
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

        var menu = node.Content switch
        {
            CategoryItem => LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout,
            LinkItem => LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout,
            _ => null
        };

        menu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
        e.Handled = true;
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
            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";

            if (LinksTreeView.SelectedNode == result.CategoryNode)
            {
                _detailsViewService!.ShowCategoryDetails((CategoryItem)result.CategoryNode.Content, result.CategoryNode);
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

    private async void CategoryMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await DeleteCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void LinkMenu_AddSubCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Parent != null)
        {
            var parentCategoryNode = _treeViewService!.GetParentCategoryNode(_contextMenuNode);
            if (parentCategoryNode != null)
            {
                await CreateSubCategoryAsync(parentCategoryNode);
            }
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
}