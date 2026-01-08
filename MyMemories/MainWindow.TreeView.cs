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

        // Show progress bar
        UrlCheckProgressBar.Visibility = Visibility.Visible;
        UrlCheckProgressText.Text = "Checking URL status...";
        UrlCheckProgressCount.Text = "0 / 0";
        StatusText.Text = "Checking URL accessibility...";

        UrlCheckStatistics? stats = null;
        string? errorMessage = null;

        try
        {
            // Start checking URLs
            stats = await _urlStateCheckerService.CheckCategoryUrlsAsync(
                categoryNode,
                (current, total, url, node) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        // Update progress display
                        UrlCheckProgressCount.Text = $"{current} / {total}";

                        // Truncate URL for display if too long
                        var displayUrl = url.Length > 60 ? url.Substring(0, 57) + "..." : url;
                        UrlCheckProgressText.Text = $"Checking: {displayUrl}";

                        // Refresh node visual to show updated badge
                        if (node != null)
                        {
                            RefreshNodeVisual(node);
                        }
                    });
                });
        }
        catch (InvalidOperationException ex)
        {
            // Node not found error - show immediately
            errorMessage = ex.Message;

            DispatcherQueue.TryEnqueue(() =>
            {
                UrlCheckProgressBar.Visibility = Visibility.Collapsed;
                StatusText.Text = $"Error: {ex.Message}";
            });

            var errorDialog = new ContentDialog
            {
                Title = "URL Check Failed",
                Content = $"The URL check was stopped because a tree node could not be found.\n\n{ex.Message}\n\nThis may happen if you modified the tree structure while checking URLs.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }
        catch (OperationCanceledException)
        {
            // User cancelled
            errorMessage = "URL check cancelled by user";
            StatusText.Text = errorMessage;
        }
        catch (Exception ex)
        {
            // Other error
            errorMessage = $"Error checking URLs: {ex.Message}";
            StatusText.Text = errorMessage;
        }
        finally
        {
            // Hide progress bar
            DispatcherQueue.TryEnqueue(() =>
            {
                UrlCheckProgressBar.Visibility = Visibility.Collapsed;
            });
        }

        // If cancelled or error, stop here
        if (errorMessage != null)
        {
            return;
        }

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
        if (stats != null)
        {
            // Build content with proper icon rendering
            var resultsPanel = new StackPanel { Spacing = 8 };
            
            resultsPanel.Children.Add(new TextBlock
            {
                Text = $"Checked {stats.TotalUrls} URLs:",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            
            // Accessible count with green icon
            var accessiblePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            accessiblePanel.Children.Add(new FontIcon
            {
                Glyph = "\uE73E", // CheckMark icon
                FontSize = 16,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
            });
            accessiblePanel.Children.Add(new TextBlock
            {
                Text = $"Accessible: {stats.AccessibleCount}",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            resultsPanel.Children.Add(accessiblePanel);
            
            // Error count with yellow icon
            var errorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            errorPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE7BA", // Warning icon
                FontSize = 16,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Yellow)
            });
            errorPanel.Children.Add(new TextBlock
            {
                Text = $"Error: {stats.ErrorCount}",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            resultsPanel.Children.Add(errorPanel);
            
            // Not found count with red icon
            var notFoundPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            notFoundPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE711", // Cancel/X icon
                FontSize = 16,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
            });
            notFoundPanel.Children.Add(new TextBlock
            {
                Text = $"Not Found: {stats.NotFoundCount}",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            resultsPanel.Children.Add(notFoundPanel);
            
            var resultsDialog = new ContentDialog
            {
                Title = "URL Check Complete",
                Content = resultsPanel,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };

            await resultsDialog.ShowAsync();

            StatusText.Text = $"URL check complete: {stats.AccessibleCount} accessible, {stats.ErrorCount} errors, {stats.NotFoundCount} not found";
        }
    }

    /// <summary>
    /// Handles the cancel button click for URL state checking.
    /// </summary>
    private void UrlCheckCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_urlStateCheckerService != null && _urlStateCheckerService.IsChecking)
        {
            _urlStateCheckerService.CancelCheck();
            UrlCheckProgressText.Text = "Cancelling...";
            UrlCheckCancelButton.IsEnabled = false;
        }
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