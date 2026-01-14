using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private static int _selectionChangeCounter = 0;
    private static DateTime _lastSelectionTime = DateTime.MinValue;

    // Guard to prevent re-entry during node refresh operations
    private static bool _isRefreshingNode = false;

    /// <summary>
    /// Sets the node refreshing flag to prevent SelectionChanged from re-triggering during node refresh.
    /// Called by TreeViewService when restoring selection after node refresh.
    /// </summary>
    public void SetNodeRefreshingFlag(bool value)
    {
        _isRefreshingNode = value;
    }

    private async void LinksTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        // Skip if we're just refreshing a node (which triggers a selection change)
        if (_isRefreshingNode)
        {
            return;
        }
        
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not TreeViewNode node)
        {
            return;
        }

        // Handle category selection with bookmark refresh capability
        if (node.Content is CategoryItem category)
        {
            HideAllViewers();
            
            // Clear content from both tabs (including WebView)
            _detailsViewService!.ClearTabbedViewContent();

            // Create refresh callback for bookmark import categories
            Func<Task>? refreshBookmarks = category.IsBookmarkImport
                ? async () => await RefreshBookmarksAsync(category, node)
                : null;

            // Create refresh URL state callback for bookmark categories
            Func<Task>? refreshUrlState = category.IsBookmarkCategory
                ? async () => await RefreshUrlStateAsync(category, node)
                : null;

            // Create sync callback for bookmark import categories
            Func<Task>? syncBookmarks = category.IsBookmarkImport
                ? async () => await SyncBookmarksAsync(category, node)
                : null;

            await _detailsViewService!.ShowCategoryDetailsAsync(category, node, refreshBookmarks, refreshUrlState, syncBookmarks);

            var categoryPath = _treeViewService!.GetCategoryPath(node);
            _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon, category);

            // Show Content tab message for categories
            _detailsViewService.ShowContentMessage("Categories do not have content. Select a link to view content.");

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
                RefreshBookmarksAsync,
                RefreshUrlStateAsync,
                SyncBookmarksAsync);
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
            // Skip zip entry URLs - they contain :: which is invalid for Uri parsing
            if (ZipUtilities.IsZipEntryUrl(link.Url))
            {
                StatusText.Text = $"Zip entry: {link.Title}";
                return;
            }
            
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
                        UrlStatus.Error => "?? URL error",
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
            // Pass the actual node that was double-tapped, not LinksTreeView.SelectedNode
            // as they might be different (double-tap can occur before selection updates)
            await _doubleTapHandlerService!.HandleDoubleTapAsync(
                linkItem,
                node,  // Use the node from the double-tap event, not the selected node
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
            
            // Redirect count with blue icon (if any redirects detected)
            if (stats.RedirectCount > 0)
            {
                var redirectPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };
                redirectPanel.Children.Add(new FontIcon
                {
                    Glyph = "\uE72A", // Forward/Redirect icon
                    FontSize = 16,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                });
                redirectPanel.Children.Add(new TextBlock
                {
                    Text = $"Redirects Detected: {stats.RedirectCount}",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                });
                resultsPanel.Children.Add(redirectPanel);
            }
            
            var resultsDialog = new ContentDialog
            {
                Title = "URL Check Complete",
                Content = resultsPanel,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };

            await resultsDialog.ShowAsync();

            var statusMessage = $"URL check complete: {stats.AccessibleCount} accessible, {stats.ErrorCount} errors, {stats.NotFoundCount} not found";
            if (stats.RedirectCount > 0)
            {
                statusMessage += $", {stats.RedirectCount} redirects";
            }
            StatusText.Text = statusMessage;
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
    /// Handles keyboard input on the TreeView.
    /// Shortcuts:
    /// - Delete: Remove selected item
    /// - Insert: Add new link
    /// - F2: Edit selected item
    /// - Ctrl+C: Copy link
    /// - Ctrl+E: Edit selected item
    /// - Ctrl+M: Move link
    /// - Ctrl+N: Add new link (same as Insert)
    /// - Ctrl+Shift+N: Add new subcategory
    /// - Enter: Open/launch selected item
    /// - Space: Expand/collapse category
    /// - Ctrl+D: Show details
    /// - Ctrl+O: Open in Explorer (for folders/files)
    /// </summary>
    private async void LinksTreeView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var selectedNode = LinksTreeView.SelectedNode;
        if (selectedNode == null && e.Key != Windows.System.VirtualKey.Insert && e.Key != Windows.System.VirtualKey.N)
            return;

        // Check for modifier keys
        var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Delete:
                await HandleDeleteKeyAsync(selectedNode);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Insert:
                await HandleInsertKeyAsync(selectedNode);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.F2:
                // F2: Edit selected item
                await HandleEditKeyAsync(selectedNode);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.C:
                if (ctrlPressed)
                {
                    // Ctrl+C: Copy link
                    await HandleCopyKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.E:
                if (ctrlPressed)
                {
                    // Ctrl+E: Edit selected item
                    await HandleEditKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.M:
                if (ctrlPressed)
                {
                    // Ctrl+M: Move link
                    await HandleMoveKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.N:
                if (ctrlPressed && shiftPressed)
                {
                    // Ctrl+Shift+N: Add new subcategory
                    await HandleAddSubcategoryKeyAsync(selectedNode);
                    e.Handled = true;
                }
                else if (ctrlPressed)
                {
                    // Ctrl+N: Add new link
                    await HandleInsertKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Enter:
                // Enter: Open/launch selected item
                await HandleEnterKeyAsync(selectedNode);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Space:
                // Space: Expand/collapse category
                if (selectedNode?.Content is CategoryItem)
                {
                    selectedNode.IsExpanded = !selectedNode.IsExpanded;
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.D:
                if (ctrlPressed)
                {
                    // Ctrl+D: Show details dialog
                    await HandleDetailsKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.O:
                if (ctrlPressed)
                {
                    // Ctrl+O: Open in Explorer
                    await HandleOpenInExplorerKeyAsync(selectedNode);
                    e.Handled = true;
                }
                break;
        }
    }

    /// <summary>
    /// Handles Delete key - removes selected item.
    /// </summary>
    private async Task HandleDeleteKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode == null) return;

        if (selectedNode.Content is LinkItem link)
        {
            if (link.IsCatalogEntry)
            {
                StatusText.Text = "Cannot remove catalog entries. Use 'Refresh Catalog' to update them.";
                return;
            }
            await DeleteLinkAsync(link, selectedNode);
        }
        else if (selectedNode.Content is CategoryItem category)
        {
            await DeleteCategoryAsync(category, selectedNode);
        }
    }

    /// <summary>
    /// Handles Insert key - adds new link.
    /// </summary>
    private async Task HandleInsertKeyAsync(TreeViewNode? selectedNode)
    {
        // Get the parent category node for the new link
        TreeViewNode? targetCategoryNode = null;

        if (selectedNode != null)
        {
            if (selectedNode.Content is CategoryItem)
            {
                targetCategoryNode = selectedNode;
            }
            else if (selectedNode.Content is LinkItem)
            {
                targetCategoryNode = _treeViewService!.GetParentCategoryNode(selectedNode);
            }
        }

        targetCategoryNode ??= _lastUsedCategory;

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

        CategoryNode? selectedCategoryNode = null;
        if (targetCategoryNode != null && targetCategoryNode.Content is CategoryItem)
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

            await UpdateParentCategoriesAndSaveAsync(result.CategoryNode);

            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";
        }
    }

    /// <summary>
    /// Handles F2/Ctrl+E key - edits selected item.
    /// </summary>
    private async Task HandleEditKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode == null) return;

        if (selectedNode.Content is CategoryItem category)
        {
            await EditCategoryAsync(category, selectedNode);
        }
        else if (selectedNode.Content is LinkItem link)
        {
            if (link.IsCatalogEntry)
            {
                StatusText.Text = "Catalog entries cannot be edited.";
                return;
            }
            await EditLinkAsync(link, selectedNode);
        }
    }

    /// <summary>
    /// Handles Ctrl+C key - copies link (duplicates the link in the same category).
    /// </summary>
    private async Task HandleCopyKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode?.Content is not LinkItem link) return;

        if (link.IsCatalogEntry)
        {
            StatusText.Text = "Catalog entries cannot be copied.";
            return;
        }

        // Find the parent category node
        var parentNode = FindParentNode(selectedNode);
        if (parentNode == null)
        {
            StatusText.Text = "Could not find parent category";
            return;
        }

        // Generate a unique title with sequence number
        var baseTitle = link.Title;
        var newTitle = GenerateUniqueTitle(baseTitle, parentNode);

        // Create a copy of the link
        var copiedLink = new LinkItem
        {
            Title = newTitle,
            Url = link.Url,
            Description = link.Description,
            Keywords = link.Keywords,
            IsDirectory = link.IsDirectory,
            CategoryPath = link.CategoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            FolderType = link.FolderType,
            FileFilters = link.FileFilters,
            UrlStatus = link.UrlStatus,
            UrlLastChecked = link.UrlLastChecked,
            UrlStatusMessage = link.UrlStatusMessage
        };

        // Create the new node
        var copiedNode = new TreeViewNode { Content = copiedLink };

        // Add the copied link to the parent category (after the original)
        var insertIndex = parentNode.Children.IndexOf(selectedNode) + 1;
        if (insertIndex > 0 && insertIndex <= parentNode.Children.Count)
        {
            parentNode.Children.Insert(insertIndex, copiedNode);
        }
        else
        {
            parentNode.Children.Add(copiedNode);
        }

        // Save the category
        var rootNode = GetRootCategoryNode(parentNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        StatusText.Text = $"Copied link as '{newTitle}'";

        // Select the new node and open the edit dialog
        LinksTreeView.SelectedNode = copiedNode;
        _contextMenuNode = copiedNode;

        // Open the edit dialog for the copied link
        await EditLinkAsync(copiedLink, copiedNode);
    }

    /// <summary>
    /// Handles Ctrl+M key - moves link.
    /// </summary>
    private async Task HandleMoveKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode?.Content is not LinkItem link) return;

        if (link.IsCatalogEntry)
        {
            StatusText.Text = "Catalog entries cannot be moved.";
            return;
        }

        await MoveLinkAsync(link, selectedNode);
    }

    /// <summary>
    /// Handles Ctrl+Shift+N key - adds subcategory.
    /// </summary>
    private async Task HandleAddSubcategoryKeyAsync(TreeViewNode? selectedNode)
    {
        TreeViewNode? targetCategoryNode = null;

        if (selectedNode != null)
        {
            if (selectedNode.Content is CategoryItem)
            {
                targetCategoryNode = selectedNode;
            }
            else if (selectedNode.Content is LinkItem)
            {
                targetCategoryNode = _treeViewService!.GetParentCategoryNode(selectedNode);
            }
        }

        if (targetCategoryNode == null)
        {
            StatusText.Text = "Please select a category first";
            return;
        }

        await CreateSubCategoryAsync(targetCategoryNode);
    }

    /// <summary>
    /// Handles Enter key - opens/launches selected item.
    /// </summary>
    private async Task HandleEnterKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode == null) return;

        if (selectedNode.Content is CategoryItem)
        {
            // Toggle expand for categories
            selectedNode.IsExpanded = !selectedNode.IsExpanded;
        }
        else if (selectedNode.Content is LinkItem link)
        {
            // Open/launch the link
            await _doubleTapHandlerService!.HandleDoubleTapAsync(
                link,
                selectedNode,
                status => StatusText.Text = status);
        }
    }

    /// <summary>
    /// Handles Ctrl+D key - shows details dialog.
    /// </summary>
    private async Task HandleDetailsKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode?.Content is LinkItem link)
        {
            var detailsViewer = new Dialogs.LinkDetailsViewer(Content.XamlRoot);
            var wantsEdit = await detailsViewer.ShowAsync(link);

            if (wantsEdit && !link.IsCatalogEntry)
            {
                await EditLinkAsync(link, selectedNode);
            }
        }
        else if (selectedNode?.Content is CategoryItem category)
        {
            // Show category stats
            var statisticsService = new CategoryStatisticsService();
            var folderPaths = statisticsService.CollectFolderPathsFromCategory(selectedNode);
            var stats = await Task.Run(() => statisticsService.CalculateMultipleFoldersStatistics(folderPaths.ToArray()));

            var statsMessage = $"?? {category.Name} Statistics:\n\n" +
                              $"• Items: {selectedNode.Children.Count}\n" +
                              $"• Folders: {stats.FolderCount:N0}\n" +
                              $"• Files: {stats.FileCount:N0}\n" +
                              $"• Total Size: {FileUtilities.FormatFileSize(stats.TotalSize)}";

            var dialog = new ContentDialog
            {
                Title = "Category Details",
                Content = statsMessage,
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    /// <summary>
    /// Handles Ctrl+O key - opens in Explorer.
    /// </summary>
    private async Task HandleOpenInExplorerKeyAsync(TreeViewNode? selectedNode)
    {
        if (selectedNode?.Content is not LinkItem link) return;

        string targetPath = link.Url;

        // For zip files, open the parent directory
        if (link.IsDirectory && targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var zipParentDir = System.IO.Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(zipParentDir) && System.IO.Directory.Exists(zipParentDir))
            {
                targetPath = zipParentDir;
            }
        }

        try
        {
            // For files, open the containing folder
            if (System.IO.File.Exists(targetPath))
            {
                var folder = System.IO.Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(folder))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(folder));
                    StatusText.Text = $"Opened folder: {folder}";
                }
            }
            else if (System.IO.Directory.Exists(targetPath))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(targetPath));
                StatusText.Text = $"Opened folder: {targetPath}";
            }
            else if (Uri.TryCreate(targetPath, UriKind.Absolute, out _))
            {
                StatusText.Text = "Cannot open Explorer for URLs";
            }
            else
            {
                StatusText.Text = "Path does not exist";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening location: {ex.Message}";
        }
    }
}