using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
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

            // Create refresh URL state callback for bookmark categories
            Func<Task>? refreshUrlState = category.IsBookmarkCategory
                ? async () => await RefreshUrlStateAsync(category, node)
                : null;

            await _detailsViewService!.ShowCategoryDetailsAsync(category, node, refreshBookmarks, refreshUrlState);

            var categoryPath = _treeViewService!.GetCategoryPath(node);
            _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon);

            ShowDetailsViewers();
            StatusText.Text = $"Viewing: {categoryPath} ({node.Children.Count} item(s))";
        }
        else
        {
            // Handle link selection through service with refresh callback
            await _treeViewEventService!.HandleSelectionChangedAsync(
                node,
                HideAllViewers,
                ShowDetailsViewers,
                ShowViewer,
                status => StatusText.Text = status,
                RefreshBookmarksAsync);
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

    private async Task RefreshUrlStateAsync(CategoryItem category, TreeViewNode categoryNode)
    {
        if (_urlStateCheckerService == null || _urlStateCheckerService.IsChecking)
        {
            StatusText.Text = "URL check already in progress...";
            return;
        }

        // Show progress dialog
        var progressDialog = new ContentDialog
        {
            Title = "Checking URL Accessibility",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Checking URLs in category...",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new ProgressBar
                    {
                        IsIndeterminate = false,
                        Value = 0,
                        Maximum = 100
                    },
                    new TextBlock
                    {
                        Name = "ProgressText",
                        Text = "0 / 0",
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var progressBar = (progressDialog.Content as StackPanel)?.Children[1] as ProgressBar;
        var progressText = (progressDialog.Content as StackPanel)?.Children[2] as TextBlock;

        // Start checking in background (remove Task.Run wrapper - method is already async)
        var checkTask = _urlStateCheckerService.CheckCategoryUrlsAsync(
            categoryNode,
            (current, total) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (progressBar != null)
                    {
                        progressBar.Maximum = total;
                        progressBar.Value = current;
                    }
                    if (progressText != null)
                    {
                        progressText.Text = $"{current} / {total}";
                    }
                });
            });

        // Show dialog (non-blocking)
        var dialogTask = progressDialog.ShowAsync().AsTask();

        // Wait for either completion or cancellation
        var completedTask = await Task.WhenAny(checkTask, dialogTask);

        if (completedTask == dialogTask)
        {
            // User cancelled - cancel the check and wait for it to finish
            _urlStateCheckerService.CancelCheck();
            try
            {
                await checkTask; // Wait for cancellation to complete
            }
            catch
            {
                // Ignore cancellation exceptions
            }
            StatusText.Text = "URL check cancelled";
            return;
        }

        // Close dialog
        progressDialog.Hide();

        // Get results
        UrlCheckStatistics stats;
        try
        {
            stats = await checkTask;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error checking URLs: {ex.Message}";
            return;
        }

        // Refresh tree view to show status badges by refreshing each link node
        RefreshTreeNodesRecursively(categoryNode);

        // Save the updated category to persist URL state changes
        try
        {
            var rootNode = GetRootCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefreshUrlStateAsync] Error saving category: {ex.Message}");
            StatusText.Text = $"Warning: URL check complete but save failed: {ex.Message}";
        }

        // Show results dialog
        var resultsDialog = new ContentDialog
        {
            Title = "URL Check Complete",
            Content = $"Checked {stats.TotalUrls} URLs:\n\n" +
                     $"? Accessible: {stats.AccessibleCount}\n" +
                     $"? Error: {stats.ErrorCount}\n" +
                     $"? Not Found: {stats.NotFoundCount}",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await resultsDialog.ShowAsync();

        StatusText.Text = $"URL check complete: {stats.AccessibleCount} accessible, {stats.ErrorCount} errors, {stats.NotFoundCount} not found";
    }

    private void RefreshTreeNodesRecursively(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Trigger visual update by refreshing the node
                RefreshNodeVisual(child);
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively refresh subcategories
                RefreshTreeNodesRecursively(child);
            }
        }
    }
}