using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
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
        Func<CategoryItem, TreeViewNode, Task>? syncBookmarksCallback = null,
        Func<string, Task>? clearArchiveCallback = null)
    {
        if (node.Content is CategoryItem category)
        {
            await HandleCategorySelectionAsync(category, node, hideAllViewers, showDetailsViewers, setStatus, 
                refreshBookmarksCallback, refreshUrlStateCallback, syncBookmarksCallback, clearArchiveCallback);
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
        Func<CategoryItem, TreeViewNode, Task>? syncBookmarksCallback = null,
        Func<string, Task>? clearArchiveCallback = null)
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
        
        // Create clear archive callback for Archive node
        Func<string, Task>? clearArchive = category.IsArchiveNode && clearArchiveCallback != null
            ? clearArchiveCallback
            : null;
        
        // Populate Summary tab with category details
        await _detailsViewService.ShowCategoryDetailsAsync(category, node, refreshBookmarks, refreshUrlState, syncBookmarks, clearArchive);

        var categoryPath = _treeViewService.GetCategoryPath(node);
        bool isRootCategory = node.Parent == null; // Root category has no parent
        _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon, category, isRootCategory);

        // Show Content tab message for categories
        _detailsViewService.ShowContentMessage("Categories do not have content. Select a link to view content.");

        showDetailsViewers();
        
        // Build status message with file location for root categories
        var statusMessage = $"Viewing: {categoryPath} ({node.Children.Count} item(s))";
        
        // Add file location for root categories
        if (node.Parent == null) // Root category
        {
            var appDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMemories",
                "Categories");
            
            // Use sanitized category name for the filename
            var fileName = FileUtilities.SanitizeFileName(category.Name) + ".json";
            var filePath = System.IO.Path.Combine(appDataFolder, fileName);
            statusMessage += $" | File: {filePath}";
        }
        
        setStatus(statusMessage);
    }
}