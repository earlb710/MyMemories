using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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
        
        // Attach pointer events to show URL status in status bar
        AttachPointerEventsToTreeViewItems();
    }
    
    /// <summary>
    /// Attaches pointer events to TreeView items to show URL status information in status bar.
    /// </summary>
    private void AttachPointerEventsToTreeViewItems()
    {
        // Find all TreeViewItem controls
        var items = FindVisualChildren<TreeViewItem>(LinksTreeView);
        
        foreach (var item in items)
        {
            // Remove old handlers to avoid duplicates
            item.PointerEntered -= TreeViewItem_PointerEntered;
            item.PointerExited -= TreeViewItem_PointerExited;
            
            // Add new handlers
            item.PointerEntered += TreeViewItem_PointerEntered;
            item.PointerExited += TreeViewItem_PointerExited;
        }
    }
    
    /// <summary>
    /// Handles pointer entering a TreeViewItem to show URL status in status bar.
    /// </summary>
    private void TreeViewItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Content is TreeViewNode node && node.Content is LinkItem link)
        {
            // Check if this is a web URL with status information
            if (!link.IsDirectory && 
                Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && 
                !uri.IsFile)
            {
                if (link.UrlStatus != UrlStatus.Unknown)
                {
                    // Build status message
                    var statusMessage = link.UrlStatus switch
                    {
                        UrlStatus.Accessible => "? URL is accessible",
                        UrlStatus.Error => "? URL error",
                        UrlStatus.NotFound => "? URL not found",
                        _ => "URL status unknown"
                    };
                    
                    if (!string.IsNullOrWhiteSpace(link.UrlStatusMessage))
                    {
                        statusMessage += $" | Status: {link.UrlStatusMessage}";
                    }
                    
                    if (link.UrlLastChecked.HasValue)
                    {
                        statusMessage += $" | Last checked: {link.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}";
                    }
                    
                    StatusText.Text = statusMessage;
                }
                else
                {
                    StatusText.Text = "URL status not checked | Click 'Refresh URL State' on category to check";
                }
            }
        }
    }
    
    /// <summary>
    /// Handles pointer exiting a TreeViewItem to restore default status.
    /// </summary>
    private void TreeViewItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        StatusText.Text = "Ready";
    }
    
    /// <summary>
    /// Handles pointer entering URL status badge to show detailed information.
    /// </summary>
    private void UrlStatusBadge_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // The TreeViewItem event handler will already show the status
        // This just ensures the badge itself also triggers the status update
    }
    
    /// <summary>
    /// Handles pointer exiting URL status badge.
    /// </summary>
    private void UrlStatusBadge_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        StatusText.Text = "Ready";
    }
    
    /// <summary>
    /// Finds all visual children of a specific type in the visual tree.
    /// </summary>
    private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
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

    /// <summary>
    /// Handles keyboard input on the TreeView. Delete key removes the selected item, Insert key adds a link.
    /// </summary>
    private async void LinksTreeView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete)
        {
            var selectedNode = LinksTreeView.SelectedNode;
            if (selectedNode == null)
                return;

            if (selectedNode.Content is LinkItem link)
            {
                // Don't allow deleting catalog entries
                if (link.IsCatalogEntry)
                {
                    StatusText.Text = "Cannot remove catalog entries. Use 'Refresh Catalog' to update them.";
                    e.Handled = true;
                    return;
                }

                await DeleteLinkAsync(link, selectedNode);
                e.Handled = true;
            }
            else if (selectedNode.Content is CategoryItem category)
            {
                await DeleteCategoryAsync(category, selectedNode);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Insert)
        {
            // Add link - same as clicking the Add Link button or context menu
            var selectedNode = LinksTreeView.SelectedNode;
            
            // Get the parent category node for the new link
            TreeViewNode? targetCategoryNode = null;
            
            if (selectedNode != null)
            {
                if (selectedNode.Content is CategoryItem)
                {
                    // If a category is selected, add link to that category
                    targetCategoryNode = selectedNode;
                }
                else if (selectedNode.Content is LinkItem)
                {
                    // If a link is selected, add to its parent category
                    targetCategoryNode = _treeViewService!.GetParentCategoryNode(selectedNode);
                }
            }
            
            // Fall back to last used category if nothing is selected
            targetCategoryNode ??= _lastUsedCategory;
            
            // Get all root categories for the dialog
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
                e.Handled = true;
                return;
            }

            // Prepare selected category for the dialog
            CategoryNode? selectedCategoryNode = null;
            if (targetCategoryNode != null && targetCategoryNode.Content is CategoryItem targetCategory)
            {
                selectedCategoryNode = new CategoryNode 
                { 
                    Name = _treeViewService!.GetCategoryPath(targetCategoryNode), 
                    Node = targetCategoryNode 
                };
            }

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

            e.Handled = true;
        }
    }
}