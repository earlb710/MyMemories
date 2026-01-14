using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace MyMemories.Services;

public class TreeViewEventService
{
    private readonly DetailsViewService _detailsViewService;
    private readonly TreeViewService _treeViewService;
    private readonly LinkSelectionService _linkSelectionService;

    public TreeViewEventService(
        DetailsViewService detailsViewService,
        TreeViewService treeViewService,
        LinkSelectionService linkSelectionService)
    {
        _detailsViewService = detailsViewService;
        _treeViewService = treeViewService;
        _linkSelectionService = linkSelectionService;
    }

    public async Task HandleSelectionChangedAsync(
        TreeViewNode node, 
        Action hideAllViewers, 
        Action showDetailsViewers, 
        Action<FileViewerType> showViewer, 
        Action<string> setStatus, 
        Func<CategoryItem, TreeViewNode, Task>? refreshBookmarksCallback = null,
        Func<CategoryItem, TreeViewNode, Task>? refreshUrlStateCallback = null,
        Func<CategoryItem, TreeViewNode, Task>? syncBookmarksCallback = null)
    {
        if (node.Content is CategoryItem category)
        {
            await HandleCategorySelectionAsync(category, node, hideAllViewers, showDetailsViewers, setStatus, 
                refreshBookmarksCallback, refreshUrlStateCallback, syncBookmarksCallback);
        }
        else if (node.Content is LinkItem linkItem)
        {
            await _linkSelectionService.HandleLinkSelectionAsync(linkItem, node, hideAllViewers, showDetailsViewers, showViewer, setStatus);
        }
    }

    private async Task HandleCategorySelectionAsync(
        CategoryItem category, 
        TreeViewNode node, 
        Action hideAllViewers, 
        Action showDetailsViewers, 
        Action<string> setStatus, 
        Func<CategoryItem, TreeViewNode, Task>? refreshBookmarksCallback = null,
        Func<CategoryItem, TreeViewNode, Task>? refreshUrlStateCallback = null,
        Func<CategoryItem, TreeViewNode, Task>? syncBookmarksCallback = null)
    {
        hideAllViewers();
        
        // Clear content but preserve tab selection
        _detailsViewService.ClearTabbedViewContent();
        
        // Create refresh callback for bookmark import categories
        Func<Task>? refreshBookmarks = category.IsBookmarkImport && refreshBookmarksCallback != null
            ? async () => await refreshBookmarksCallback(category, node)
            : null;
        
        // Create URL state refresh callback for bookmark categories
        Func<Task>? refreshUrlState = category.IsBookmarkCategory && refreshUrlStateCallback != null
            ? async () => await refreshUrlStateCallback(category, node)
            : null;
        
        // Create sync callback for bookmark import categories
        Func<Task>? syncBookmarks = category.IsBookmarkImport && syncBookmarksCallback != null
            ? async () => await syncBookmarksCallback(category, node)
            : null;
        
        // Populate Summary tab with category details
        await _detailsViewService.ShowCategoryDetailsAsync(category, node, refreshBookmarks, refreshUrlState, syncBookmarks);

        var categoryPath = _treeViewService.GetCategoryPath(node);
        _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon, category);

        // Show Content tab message for categories
        _detailsViewService.ShowContentMessage("Categories do not have content. Select a link to view content.");

        showDetailsViewers();
        setStatus($"Viewing: {categoryPath} ({node.Children.Count} item(s))");
    }
}