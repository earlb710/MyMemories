using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Dialogs;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Saved Searches functionality - saved complex searches that can be re-executed.
/// </summary>
public sealed partial class MainWindow
{
    private TreeViewNode? _searchesNode;
    private SavedSearchService? _savedSearchService;
    
    // Maps search result nodes to their original nodes for navigation
    private readonly Dictionary<TreeViewNode, TreeViewNode> _searchResultToOriginalNode = new();
    
    /// <summary>
    /// Gets or creates the Searches node in the tree.
    /// </summary>
    private TreeViewNode GetOrCreateSearchesNode()
    {
        if (_searchesNode != null)
            return _searchesNode;
        
        // Find existing searches node
        foreach (var node in LinksTreeView.RootNodes)
        {
            if (node.Content is CategoryItem cat && cat.IsSearchesNode)
            {
                _searchesNode = node;
                return _searchesNode;
            }
        }
        
        // Should not happen as Searches is created in LoadAllCategoriesAsync
        return LinksTreeView.RootNodes[^2]; // Return second-to-last node
    }
    
    /// <summary>
    /// Updates the Searches node display name to show search count.
    /// </summary>
    private void UpdateSearchesNodeName()
    {
        try
        {
            var searchesNode = GetOrCreateSearchesNode();
            if (searchesNode?.Content is CategoryItem category)
            {
                int count = _savedSearchService?.Searches.Count ?? 0;
                var newName = $"Searches ({count})";
                
                if (category.Name != newName)
                {
                    category.Name = newName;
                    
                    var updatedCategory = new CategoryItem
                    {
                        Name = newName,
                        Description = category.Description,
                        Icon = category.Icon,
                        IsSearchesNode = true
                    };
                    
                    searchesNode.Content = updatedCategory;
                    _searchesNode = searchesNode;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Searches] Error updating searches name: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Refreshes the saved searches node children.
    /// </summary>
    private void RefreshSearchesNodeChildren()
    {
        if (_searchesNode == null || _savedSearchService == null)
            return;
        
        _searchesNode.Children.Clear();
        
        foreach (var search in _savedSearchService.Searches)
        {
            var searchItem = new LinkItem
            {
                Title = $"{search.Icon} {search.Name}",
                Description = search.Description,
                Url = $"search://{search.Id}", // Special URL scheme for saved searches
                IsSavedSearch = true,
                SavedSearchId = search.Id
            };
            
            var searchNode = new TreeViewNode { Content = searchItem };
            _searchesNode.Children.Add(searchNode);
        }
        
        UpdateSearchesNodeName();
    }
    
    /// <summary>
    /// Gets a list of all available category names for search filtering.
    /// </summary>
    private List<string> GetAvailableCategoryNames()
    {
        var categories = new List<string>();
        
        foreach (var node in LinksTreeView.RootNodes)
        {
            if (node.Content is CategoryItem category)
            {
                // Skip system nodes (dividers, archive, searches)
                if (category.IsArchiveNode || category.IsSearchesNode || 
                    string.IsNullOrEmpty(category.Name) || category.Name.StartsWith("———"))
                    continue;
                
                categories.Add(category.Name);
            }
        }
        
        return categories;
    }
    
    /// <summary>
    /// Shows the Add Search dialog and creates a new saved search.
    /// </summary>
    private async Task AddSavedSearchAsync()
    {
        var availableCategories = GetAvailableCategoryNames();
        var dialog = new SavedSearchDialog(Content.XamlRoot, availableCategories);
        var search = await dialog.ShowCreateDialogAsync();
        
        if (search != null)
        {
            await _savedSearchService!.AddSearchAsync(search);
            RefreshSearchesNodeChildren();
            StatusText.Text = $"Created saved search: {search.Name}";
        }
    }
    
    /// <summary>
    /// Shows the Edit Search dialog for an existing saved search.
    /// </summary>
    private async Task EditSavedSearchAsync(string searchId)
    {
        var search = _savedSearchService?.GetSearch(searchId);
        if (search == null)
            return;
        
        var availableCategories = GetAvailableCategoryNames();
        var dialog = new SavedSearchDialog(Content.XamlRoot, availableCategories);
        var updatedSearch = await dialog.ShowEditDialogAsync(search);
        
        if (updatedSearch != null)
        {
            await _savedSearchService!.UpdateSearchAsync(updatedSearch);
            RefreshSearchesNodeChildren();
            StatusText.Text = $"Updated saved search: {updatedSearch.Name}";
        }
    }
    
    /// <summary>
    /// Deletes a saved search after confirmation.
    /// </summary>
    private async Task DeleteSavedSearchAsync(string searchId)
    {
        var search = _savedSearchService?.GetSearch(searchId);
        if (search == null)
            return;
        
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Saved Search",
            Content = $"Are you sure you want to delete the saved search '{search.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        
        var result = await confirmDialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            await _savedSearchService!.DeleteSearchAsync(searchId);
            RefreshSearchesNodeChildren();
            StatusText.Text = $"Deleted saved search: {search.Name}";
        }
    }
    
    /// <summary>
    /// Executes a saved search and displays results.
    /// </summary>
    private async Task ExecuteSavedSearchAsync(string searchId)
    {
        var search = _savedSearchService?.GetSearch(searchId);
        if (search == null)
            return;
        
        StatusText.Text = $"Executing search: {search.Name}...";
        
        try
        {
            var result = await _savedSearchService!.ExecuteSearchAsync(search, LinksTreeView);
            
            // Find the search node and add results as children
            var searchNode = FindSearchNode(searchId);
            if (searchNode != null)
            {
                AddSearchResultsToNode(searchNode, result);
                searchNode.IsExpanded = true;
            }
            
            // Show results in details panel
            await ShowSearchResultsAsync(result);
            
            StatusText.Text = $"Found {result.Results.Count} item(s) in {result.ExecutionTime.TotalMilliseconds:F0}ms";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Search error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Finds the tree node for a saved search by ID.
    /// </summary>
    private TreeViewNode? FindSearchNode(string searchId)
    {
        if (_searchesNode == null)
            return null;
        
        foreach (var child in _searchesNode.Children)
        {
            if (child.Content is LinkItem linkItem && linkItem.SavedSearchId == searchId)
            {
                return child;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Adds search results as child nodes under the search node.
    /// </summary>
    private void AddSearchResultsToNode(TreeViewNode searchNode, SearchExecutionResult result)
    {
        // Clear existing result children and their mappings
        foreach (var child in searchNode.Children)
        {
            _searchResultToOriginalNode.Remove(child);
        }
        searchNode.Children.Clear();
        
        foreach (var item in result.Results)
        {
            var resultItem = new LinkItem
            {
                Title = item.Name,
                Description = $"{item.ItemType} • {item.CategoryPath}",
                Url = item.Link?.Url ?? string.Empty,
                IsDirectory = item.Link?.IsDirectory ?? false,
                IsCatalogEntry = item.Link?.IsCatalogEntry ?? false
            };
            
            // Copy additional properties if available
            if (item.Link != null)
            {
                resultItem.TagIds = item.Link.TagIds;
                resultItem.Ratings = item.Link.Ratings;
                resultItem.CreatedDate = item.Link.CreatedDate;
                resultItem.ModifiedDate = item.Link.ModifiedDate;
            }
            
            var resultNode = new TreeViewNode { Content = resultItem };
            searchNode.Children.Add(resultNode);
            
            
            // Store mapping to original node for navigation on double-click
            if (item.Node != null)
            {
                _searchResultToOriginalNode[resultNode] = item.Node;
            }
        }
    }
    
    /// <summary>
    /// Checks if a node is a search result node and navigates to its original if double-clicked.
    /// Returns true if the node was a search result and navigation was handled.
    /// </summary>
    private bool TryNavigateToOriginalFromSearchResult(TreeViewNode node)
    {
        if (_searchResultToOriginalNode.TryGetValue(node, out var originalNode))
        {
            // Use DispatcherQueue to navigate after the double-tap event completes
            // This prevents the tree from jumping back to the clicked node
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                NavigateToNode(originalNode);
            });
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Navigates to a node in the tree - expands parents and selects it.
    /// </summary>
    private void NavigateToNode(TreeViewNode targetNode)
    {
        // First expand all parent nodes
        var parent = targetNode.Parent;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
        
        // Small delay to let the tree expand before selecting
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            // Select the target node
            LinksTreeView.SelectedNode = targetNode;
            
            // Force focus to the tree view to ensure selection sticks
            LinksTreeView.Focus(FocusState.Programmatic);
        });
    }
    
    /// <summary>
    /// Shows search results in the details panel.
    /// </summary>
    private async Task ShowSearchResultsAsync(SearchExecutionResult result)
    {
        await _detailsViewService!.ShowSearchResultsAsync(
            result,
            async (item) =>
            {
                // Navigate to the item when clicked
                if (item.Node != null)
                {
                    NavigateToNode(item.Node);
                }
                await Task.CompletedTask;
            });
    }
    
    /// <summary>
    /// Shows details for a saved search with Run, Edit, and Delete buttons.
    /// </summary>
    private async Task ShowSavedSearchDetailsAsync(SavedSearch search, TreeViewNode node)
    {
        await _detailsViewService!.ShowSavedSearchDetailsAsync(
            search,
            async () => await ExecuteSavedSearchAsync(search.Id),
            async () => await EditSavedSearchAsync(search.Id),
            async () => await DeleteSavedSearchAsync(search.Id));
        
        _detailsViewService.ShowCategoryHeader(
            search.Name, 
            search.Description, 
            search.Icon, 
            null);
        
        _detailsViewService.ShowContentMessage("Double-click or click 'Run Search' to execute this search.");
        
        StatusText.Text = $"Saved search: {search.Name}";
    }
}
