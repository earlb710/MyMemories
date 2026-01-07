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

    public async Task HandleSelectionChangedAsync(TreeViewNode node, Action hideAllViewers, Action<string> setStatus)
    {
        if (node.Content is CategoryItem category)
        {
            await HandleCategorySelectionAsync(category, node, hideAllViewers, setStatus);
        }
        else if (node.Content is LinkItem linkItem)
        {
            // CRITICAL FIX: Pass the node to the link selection service
            await _linkSelectionService.HandleLinkSelectionAsync(linkItem, node, hideAllViewers, setStatus);
        }
    }

    private async Task HandleCategorySelectionAsync(CategoryItem category, TreeViewNode node, Action hideAllViewers, Action<string> setStatus)
    {
        hideAllViewers();
        _detailsViewService.ShowCategoryDetails(category, node);

        var categoryPath = _treeViewService.GetCategoryPath(node);
        _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon);

        setStatus($"Viewing: {categoryPath} ({node.Children.Count} item(s))");
    }
}