using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async void LinksTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not TreeViewNode node)
            return;

        // Handle category selection with bookmark refresh capability
        if (node.Content is CategoryItem category)
        {
            HideAllViewers();

            // Create refresh callback for bookmark import categories
            Func<Task>? refreshBookmarks = category.IsBookmarkImport
                ? async () => await RefreshBookmarksAsync(category, node)
                : null;

            await _detailsViewService!.ShowCategoryDetailsAsync(category, node, refreshBookmarks);

            var categoryPath = _treeViewService!.GetCategoryPath(node);
            _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon);

            ShowDetailsViewers();
            StatusText.Text = $"Viewing: {categoryPath} ({node.Children.Count} item(s))";
        }
        else
        {
            // Handle link selection through service
            await _treeViewEventService!.HandleSelectionChangedAsync(
                node,
                HideAllViewers,
                ShowDetailsViewers,
                ShowViewer,
                status => StatusText.Text = status);
        }
    }

    private async void LinksTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        if (node.Content is CategoryItem category)
        {
            await EditCategoryAsync(category, node);
            e.Handled = true;
        }
        else if (node.Content is LinkItem linkItem)
        {
            await _doubleTapHandlerService!.HandleDoubleTapAsync(
                linkItem,
                LinksTreeView.SelectedNode,
                status => StatusText.Text = status);
            e.Handled = true;
        }
    }
}