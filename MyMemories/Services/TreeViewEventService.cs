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

    public async Task HandleSelectionChangedAsync(TreeViewNode node, Action hideAllViewers, Action showDetailsViewers, Action<FileViewerType> showViewer, Action<string> setStatus, Func<CategoryItem, TreeViewNode, Task>? refreshBookmarksCallback = null)
    {
        if (node.Content is CategoryItem category)
        {
            await HandleCategorySelectionAsync(category, node, hideAllViewers, showDetailsViewers, setStatus, refreshBookmarksCallback);
        }
        else if (node.Content is LinkItem linkItem)
        {
            await _linkSelectionService.HandleLinkSelectionAsync(linkItem, node, hideAllViewers, showDetailsViewers, showViewer, setStatus);
        }
    }

    private async Task HandleCategorySelectionAsync(CategoryItem category, TreeViewNode node, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus, Func<CategoryItem, TreeViewNode, Task>? refreshBookmarksCallback = null)
    {
        hideAllViewers();
        
        // Create refresh callback for bookmark import categories
        Func<Task>? refreshBookmarks = category.IsBookmarkImport && refreshBookmarksCallback != null
            ? async () => await refreshBookmarksCallback(category, node)
            : null;
        
        await _detailsViewService.ShowCategoryDetailsAsync(category, node, refreshBookmarks);

        var categoryPath = _treeViewService.GetCategoryPath(node);
        _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon);

        showDetailsViewers();
        setStatus($"Viewing: {categoryPath} ({node.Children.Count} item(s))");
    }
}