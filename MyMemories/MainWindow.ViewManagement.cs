using Microsoft.UI.Xaml;
using MyMemories.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
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
                UrlBarPanel.Visibility = Visibility.Collapsed;
                break;
            case FileViewerType.Web:
                WebViewer.Visibility = Visibility.Visible;
                UrlBarPanel.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Text:
                TextViewerScroll.Visibility = Visibility.Visible;
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
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            SearchComboBox.ItemsSource = null;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            _lastSearchText = string.Empty;
            StatusText.Text = "Ready";
            return;
        }

        bool isNewSearch = searchText != _lastSearchText;

        if (isNewSearch)
        {
            _searchResults = SearchNodes(searchText);
            _currentSearchIndex = -1;
            _lastSearchText = searchText;

            if (_searchResults.Count == 0)
            {
                SearchComboBox.ItemsSource = null;
                StatusText.Text = $"No results found for '{searchText}'";
                return;
            }

            SearchComboBox.ItemsSource = _searchResults.Select(r => r.DisplayText).ToList();
        }

        if (_searchResults.Count > 0)
        {
            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
            NavigateToSearchResult(_searchResults[_currentSearchIndex], false);
            
            StatusText.Text = $"Result {_currentSearchIndex + 1} of {_searchResults.Count} for '{searchText}'";
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
            
            // Search in name, description, and keywords
            if (category.Name.ToLowerInvariant().Contains(searchLower) ||
                (!string.IsNullOrEmpty(category.Description) && category.Description.ToLowerInvariant().Contains(searchLower)) ||
                (!string.IsNullOrEmpty(category.Keywords) && MatchesKeywords(category.Keywords, searchLower)))
            {
                results.Add(new SearchResult
                {
                    DisplayText = $"📁 {categoryPath}",
                    Node = node,
                    NodeType = "Category"
                });
            }

            foreach (var child in node.Children)
            {
                SearchNodeRecursive(child, searchLower, results);
            }
        }
        else if (node.Content is LinkItem link)
        {
            var categoryPath = link.CategoryPath;
            
            // Search in title, URL, description, and keywords
            if (link.Title.ToLowerInvariant().Contains(searchLower) ||
                (!string.IsNullOrEmpty(link.Url) && link.Url.ToLowerInvariant().Contains(searchLower)) ||
                (!string.IsNullOrEmpty(link.Description) && link.Description.ToLowerInvariant().Contains(searchLower)) ||
                (!string.IsNullOrEmpty(link.Keywords) && MatchesKeywords(link.Keywords, searchLower)))
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

    /// <summary>
    /// Checks if the search term matches any of the keywords (comma or semicolon separated).
    /// </summary>
    private bool MatchesKeywords(string keywords, string searchLower)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return false;

        // Split keywords by comma or semicolon, trim each, and check if any contains the search term
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

    private void ExpandParentNodes(TreeViewNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
    }
}