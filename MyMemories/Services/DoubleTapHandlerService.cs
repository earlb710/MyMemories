using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

public class DoubleTapHandlerService
{
    private readonly FileLauncherService _fileLauncherService;
    private readonly UrlStateCheckerService? _urlStateCheckerService;
    private readonly CategoryService? _categoryService;
    private readonly TreeViewService? _treeViewService;

    public DoubleTapHandlerService(
        FileLauncherService fileLauncherService,
        UrlStateCheckerService? urlStateCheckerService = null,
        CategoryService? categoryService = null,
        TreeViewService? treeViewService = null)
    {
        _fileLauncherService = fileLauncherService;
        _urlStateCheckerService = urlStateCheckerService;
        _categoryService = categoryService;
        _treeViewService = treeViewService;
    }

    public async Task HandleDoubleTapAsync(LinkItem linkItem, TreeViewNode? selectedNode, Action<string> setStatus)
    {
        // Don't do anything if URL is empty (e.g., temporary "busy creating" nodes)
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            return;
        }

        // Check if this is a zip entry (URL contains "::")
        if (linkItem.Url.Contains("::"))
        {
            // If it's a directory within the zip, just expand/collapse
            if (linkItem.IsDirectory && selectedNode != null)
            {
                selectedNode.IsExpanded = !selectedNode.IsExpanded;
            }
            else
            {
                // It's a file within the zip - extract and open it
                await _fileLauncherService.OpenZipEntryAsync(linkItem, setStatus);
            }
        }
        else if (linkItem.IsDirectory && selectedNode != null)
        {
            // Expand/collapse regular directory
            selectedNode.IsExpanded = !selectedNode.IsExpanded;
        }
        else if (!string.IsNullOrEmpty(linkItem.Url))
        {
            // Check if this is a web URL
            if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                // Open the web URL and check/update status
                await OpenWebUrlAndUpdateStatusAsync(linkItem, selectedNode, setStatus);
            }
            else
            {
                // Open regular file using OpenLinkAsync which handles files properly
                await _fileLauncherService.OpenLinkAsync(linkItem, setStatus);
            }
        }
    }

    /// <summary>
    /// Opens a web URL and checks/updates its status if it has changed.
    /// </summary>
    private async Task OpenWebUrlAndUpdateStatusAsync(LinkItem linkItem, TreeViewNode? selectedNode, Action<string> setStatus)
    {
        // First, open the URL in the default browser
        var opened = await _fileLauncherService.OpenLinkAsync(linkItem, setStatus);

        if (!opened)
        {
            return;
        }

        // If we have the URL state checker, verify and update the status
        if (_urlStateCheckerService != null)
        {
            try
            {
                var previousStatus = linkItem.UrlStatus;
                
                // Check the current URL status
                var (newStatus, message) = await _urlStateCheckerService.CheckSingleUrlAsync(linkItem.Url);

                // Always update the link item with the new status
                linkItem.UrlStatus = newStatus;
                linkItem.UrlStatusMessage = message;
                linkItem.UrlLastChecked = DateTime.Now;

                // Determine if status changed
                bool statusChanged = previousStatus != newStatus;

                if (selectedNode != null)
                {
                    // Get the root node BEFORE refreshing the tree node
                    TreeViewNode? rootNode = null;
                    if (_categoryService != null)
                    {
                        rootNode = GetRootCategoryNode(selectedNode);
                    }
                    
                    // Refresh the tree node visual to show updated badge color
                    if (_treeViewService != null)
                    {
                        _treeViewService.RefreshLinkNode(selectedNode, linkItem);
                    }
                    
                    // Save the category to persist the change
                    if (_categoryService != null && rootNode != null)
                    {
                        await _categoryService.SaveCategoryAsync(rootNode);
                    }
                    
                    // Update status message to reflect the change
                    if (statusChanged)
                    {
                        var statusText = newStatus switch
                        {
                            UrlStatus.Accessible => "? URL accessible",
                            UrlStatus.Error => $"? URL error: {message}",
                            UrlStatus.NotFound => $"? URL not found: {message}",
                            _ => "URL status updated"
                        };
                        
                        setStatus($"Opened: {linkItem.Title} | {statusText}");
                    }
                }
            }
            catch (Exception)
            {
                // Don't fail the operation if status check fails - URL was already opened
            }
        }
    }

    /// <summary>
    /// Gets the root category node for a given tree node.
    /// </summary>
    private TreeViewNode? GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100;

        // Navigate up to find the root category
        while (current?.Parent != null && safetyCounter < maxDepth)
        {
            current = current.Parent;
            safetyCounter++;
        }

        if (current == null || safetyCounter >= maxDepth)
        {
            return null;
        }

        // The root should be a CategoryItem
        if (current.Content is CategoryItem)
        {
            return current;
        }
        
        return null;
    }
}