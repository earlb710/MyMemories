using Microsoft.UI.Xaml;
using MyMemories.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace MyMemories;

public sealed partial class MainWindow
{
    private class SearchResult
    {
        public string DisplayText { get; set; } = string.Empty;
        public TreeViewNode Node { get; set; } = null!;
        public string NodeType { get; set; } = string.Empty;
    }

    // Tag filter state
    private string? _activeTagFilterId = null;
    private string? _activeTagFilterName = null;

    private void HideAllViewers()
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        WebViewer.Visibility = Visibility.Collapsed;
        TextViewerScroll.Visibility = Visibility.Collapsed;
        DetailsViewerScroll.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Collapsed;
        HeaderViewerScroll.Visibility = Visibility.Collapsed;
        UrlBarPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowWelcome()
    {
        HideAllViewers();
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private void ShowViewer(FileViewerType viewerType)
    {
        switch (viewerType)
        {
            case FileViewerType.Image:
                ImageViewer.Visibility = Visibility.Visible;
                HeaderViewerScroll.Visibility = Visibility.Visible;
                DetailsViewerScroll.Visibility = Visibility.Collapsed;
                UrlBarPanel.Visibility = Visibility.Collapsed;
                break;
            case FileViewerType.Web:
                WebViewer.Visibility = Visibility.Visible;
                HeaderViewerScroll.Visibility = Visibility.Visible;
                DetailsViewerScroll.Visibility = Visibility.Collapsed;
                UrlBarPanel.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Document:
                WebViewer.Visibility = Visibility.Visible;
                HeaderViewerScroll.Visibility = Visibility.Visible;
                DetailsViewerScroll.Visibility = Visibility.Collapsed;
                UrlBarPanel.Visibility = Visibility.Collapsed;
                break;
            case FileViewerType.Text:
                TextViewerScroll.Visibility = Visibility.Visible;
                HeaderViewerScroll.Visibility = Visibility.Visible;
                DetailsViewerScroll.Visibility = Visibility.Collapsed;
                UrlBarPanel.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void ShowDetailsViewers()
    {
        DetailsViewerScroll.Visibility = Visibility.Visible;
        HeaderViewerScroll.Visibility = Visibility.Visible;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void SearchComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
    }

    private void SearchComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var searchText = SearchComboBox.Text?.Trim();
        
        // If no search text and no tag filter, clear results
        if (string.IsNullOrWhiteSpace(searchText) && string.IsNullOrEmpty(_activeTagFilterId))
        {
            SearchComboBox.ItemsSource = null;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            _lastSearchText = string.Empty;
            StatusText.Text = "Ready";
            return;
        }

        // Build a search key that includes the tag filter
        var effectiveSearchKey = $"{searchText ?? ""}|{_activeTagFilterId ?? ""}";
        bool isNewSearch = effectiveSearchKey != _lastSearchText;

        if (isNewSearch)
        {
            _searchResults = SearchNodes(searchText ?? string.Empty);
            _currentSearchIndex = -1;
            _lastSearchText = effectiveSearchKey;

            if (_searchResults.Count == 0)
            {
                SearchComboBox.ItemsSource = null;
                var filterInfo = !string.IsNullOrEmpty(_activeTagFilterName) 
                    ? $" with tag '{_activeTagFilterName}'" 
                    : "";
                var searchInfo = !string.IsNullOrWhiteSpace(searchText) 
                    ? $"'{searchText}'" 
                    : "items";
                StatusText.Text = $"No results found for {searchInfo}{filterInfo}";
                return;
            }

            SearchComboBox.ItemsSource = _searchResults.Select(r => r.DisplayText).ToList();
        }

        if (_searchResults.Count > 0)
        {
            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
            NavigateToSearchResult(_searchResults[_currentSearchIndex], false);
            
            var filterInfo = !string.IsNullOrEmpty(_activeTagFilterName) 
                ? $" (Tag: {_activeTagFilterName})" 
                : "";
            var searchInfo = !string.IsNullOrWhiteSpace(searchText) 
                ? $"for '{searchText}'" 
                : "";
            StatusText.Text = $"Result {_currentSearchIndex + 1} of {_searchResults.Count} {searchInfo}{filterInfo}";
        }
    }

    private List<SearchResult> SearchNodes(string searchText)
    {
        var results = new List<SearchResult>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            SearchNodeRecursive(rootNode, searchLower, results);
        }

        return results;
    }

    private void SearchNodeRecursive(TreeViewNode node, string searchLower, List<SearchResult> results)
    {
        if (node.Content is CategoryItem category)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(node);
            
            // Check tag filter first
            bool passesTagFilter = string.IsNullOrEmpty(_activeTagFilterId) || 
                                   category.TagIds.Contains(_activeTagFilterId);
            
            if (passesTagFilter)
            {
                // If no search text, include all items that pass the tag filter
                bool matchesSearch = string.IsNullOrWhiteSpace(searchLower) ||
                    category.Name.ToLowerInvariant().Contains(searchLower) ||
                    (!string.IsNullOrEmpty(category.Description) && category.Description.ToLowerInvariant().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(category.Keywords) && MatchesKeywords(category.Keywords, searchLower));

                if (matchesSearch)
                {
                    results.Add(new SearchResult
                    {
                        DisplayText = $"📁 {categoryPath}",
                        Node = node,
                        NodeType = "Category"
                    });
                }
            }

            // Always recurse into children (they might have the tag even if parent doesn't)
            foreach (var child in node.Children)
            {
                SearchNodeRecursive(child, searchLower, results);
            }
        }
        else if (node.Content is LinkItem link)
        {
            var categoryPath = link.CategoryPath;
            
            // Check tag filter first
            bool passesTagFilter = string.IsNullOrEmpty(_activeTagFilterId) || 
                                   link.TagIds.Contains(_activeTagFilterId);
            
            if (passesTagFilter)
            {
                // If no search text, include all items that pass the tag filter
                bool matchesSearch = string.IsNullOrWhiteSpace(searchLower) ||
                    link.Title.ToLowerInvariant().Contains(searchLower) ||
                    (!string.IsNullOrEmpty(link.Url) && link.Url.ToLowerInvariant().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(link.Description) && link.Description.ToLowerInvariant().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(link.Keywords) && MatchesKeywords(link.Keywords, searchLower));

                if (matchesSearch)
                {
                    var icon = link.GetIcon();
                    var displayText = string.IsNullOrEmpty(categoryPath) 
                        ? $"{icon} {link.Title}" 
                        : $"{icon} {link.Title} [{categoryPath}]";

                    results.Add(new SearchResult
                    {
                        DisplayText = displayText,
                        Node = node,
                        NodeType = "Link"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Checks if the search term matches any of the keywords (comma or semicolon separated).
    /// </summary>
    private bool MatchesKeywords(string keywords, string searchLower)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return false;

        var keywordList = keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var keyword in keywordList)
        {
            var trimmedKeyword = keyword.Trim().ToLowerInvariant();
            if (trimmedKeyword.Contains(searchLower) || searchLower.Contains(trimmedKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private void NavigateToSearchResult(SearchResult result, bool clearSearch = true)
    {
        ExpandParentNodes(result.Node);
        LinksTreeView.SelectedNode = result.Node;

        ScrollToSelectedNode(result.Node);

        if (clearSearch)
        {
            SearchComboBox.Text = string.Empty;
            SearchComboBox.ItemsSource = null;
            SearchComboBox.IsDropDownOpen = false;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            _lastSearchText = string.Empty;
            StatusText.Text = $"Navigated to: {result.DisplayText}";
        }
    }

    /// <summary>
    /// Scrolls the TreeView to bring the specified node into view.
    /// </summary>
    private void ScrollToSelectedNode(TreeViewNode node)
    {
        System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] Called for node");
        
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(200);
            
            LinksTreeView.UpdateLayout();
            
            var listControl = FindChildByName(LinksTreeView, "ListControl") as ListViewBase;
            
            if (listControl != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] Found ListControl: {listControl.GetType().Name}");
                try
                {
                    listControl.ScrollIntoView(node);
                    System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] ScrollIntoView called on node");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] ScrollIntoView failed: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] ListControl not found, trying container approach");
                
                var container = LinksTreeView.ContainerFromNode(node);
                System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] Container: {container?.GetType().Name ?? "null"}");
                
                if (container is UIElement uiElement)
                {
                    uiElement.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = true,
                        VerticalAlignmentRatio = 0.5
                    });
                    System.Diagnostics.Debug.WriteLine($"[ScrollToSelectedNode] StartBringIntoView called");
                }
            }
        });
    }

    /// <summary>
    /// Finds a child element by name using VisualTreeHelper.
    /// </summary>
    private static DependencyObject? FindChildByName(DependencyObject parent, string controlName)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == controlName)
                return child;

            var findResult = FindChildByName(child, controlName);
            if (findResult != null)
                return findResult;
        }
        return null;
    }

    private void ExpandParentNodes(TreeViewNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
    }

    #region Tag Filter

    /// <summary>
    /// Handles the flyout opening event to populate tags.
    /// </summary>
    private void TagFilterFlyout_Opening(object? sender, object e)
    {
        PopulateTagFilterFlyout();
    }

    /// <summary>
    /// Populates the tag filter flyout with available tags.
    /// Called when the flyout is about to open.
    /// </summary>
    private void PopulateTagFilterFlyout()
    {
        TagFilterFlyout.Items.Clear();

        // Add "Clear Filter" option if a filter is active
        if (!string.IsNullOrEmpty(_activeTagFilterId))
        {
            var clearItem = new MenuFlyoutItem
            {
                Text = "✕ Clear Filter",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            clearItem.Click += TagFilterClear_Click;
            TagFilterFlyout.Items.Add(clearItem);
            TagFilterFlyout.Items.Add(new MenuFlyoutSeparator());
        }

        // Add all available tags
        if (_tagService == null || _tagService.TagCount == 0)
        {
            var noTagsItem = new MenuFlyoutItem
            {
                Text = "No tags available",
                IsEnabled = false
            };
            TagFilterFlyout.Items.Add(noTagsItem);
            return;
        }

        foreach (var tag in _tagService.Tags)
        {
            var tagItem = new MenuFlyoutItem
            {
                Text = $"🏷️ {tag.Name}",
                Tag = tag.Id
            };

            // Mark the active tag with a checkmark
            if (tag.Id == _activeTagFilterId)
            {
                tagItem.Text = $"✓ 🏷️ {tag.Name}";
                tagItem.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            tagItem.Click += TagFilterItem_Click;
            TagFilterFlyout.Items.Add(tagItem);
        }
    }

    /// <summary>
    /// Handles clicking on a tag filter item.
    /// </summary>
    private void TagFilterItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tagId)
        {
            var tag = _tagService?.GetTag(tagId);
            if (tag != null)
            {
                _activeTagFilterId = tagId;
                _activeTagFilterName = tag.Name;
                
                // Show the active filter indicator
                TagFilterActiveIndicator.Visibility = Visibility.Visible;
                
                // Reset search to apply the new filter
                _lastSearchText = string.Empty;
                PerformSearch();
                
                StatusText.Text = $"Filtering by tag: {tag.Name}";
            }
        }
    }

    /// <summary>
    /// Clears the active tag filter.
    /// </summary>
    private void TagFilterClear_Click(object sender, RoutedEventArgs e)
    {
        ClearTagFilter();
    }

    /// <summary>
    /// Clears the active tag filter and updates the UI.
    /// </summary>
    private void ClearTagFilter()
    {
        _activeTagFilterId = null;
        _activeTagFilterName = null;
        
        // Hide the active filter indicator
        TagFilterActiveIndicator.Visibility = Visibility.Collapsed;
        
        // Clear search results
        SearchComboBox.ItemsSource = null;
        _searchResults.Clear();
        _currentSearchIndex = -1;
        _lastSearchText = string.Empty;
        
        StatusText.Text = "Tag filter cleared";
    }

    #endregion
}