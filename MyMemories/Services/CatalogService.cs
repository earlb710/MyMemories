using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Services;

public class CatalogService
{
    private readonly CategoryService _categoryService;
    private readonly TreeViewService _treeViewService;
    private readonly DetailsViewService _detailsViewService;
    private readonly ZipCatalogService _zipCatalogService;

    public CatalogService(
        CategoryService categoryService,
        TreeViewService treeViewService,
        DetailsViewService detailsViewService,
        ZipCatalogService zipCatalogService)
    {
        _categoryService = categoryService;
        _treeViewService = treeViewService;
        _detailsViewService = detailsViewService;
        _zipCatalogService = zipCatalogService;
    }

    public async Task CreateCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        bool isZipFile = IsZipFile(linkItem.Url);
        
        try
        {
            Debug.WriteLine($"[CreateCatalogAsync] Creating catalog for '{linkItem.Title}', IsZip: {isZipFile}");
            
            var tempNode = ShowCatalogingProgress(linkNode, isZipFile);

            if (isZipFile)
            {
                await _zipCatalogService.CatalogZipFileAsync(linkItem, linkNode);
            }
            else
            {
                await CatalogDirectoryAsync(linkItem, linkNode);
            }

            linkNode.Children.Remove(tempNode);
            await FinalizeCatalogCreationAsync(linkItem, linkNode);
        }
        catch (Exception ex)
        {
            HandleCatalogError("creating", linkItem.Title, ex);
            throw; // Re-throw the exception after logging
        }
    }

    public async Task RefreshCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        bool wasExpanded = linkNode.IsExpanded;
        bool isZipFile = IsZipFile(linkItem.Url);

        try
        {
            Debug.WriteLine($"[RefreshCatalogAsync] Refreshing catalog for '{linkItem.Title}', IsZip: {isZipFile}");
            
            _categoryService.RemoveCatalogEntries(linkNode);
            var tempNode = ShowCatalogingProgress(linkNode, isZipFile, isRefresh: true);

            if (isZipFile)
            {
                await _zipCatalogService.CatalogZipFileAsync(linkItem, linkNode);
            }
            else
            {
                await CatalogDirectoryAsync(linkItem, linkNode);
            }

            linkNode.Children.Remove(tempNode);
            await FinalizeCatalogCreationAsync(linkItem, linkNode);
        }
        catch (Exception ex)
        {
            HandleCatalogError("refreshing", linkItem.Title, ex);
            throw; // Re-throw the exception after logging
        }
        finally
        {
            linkNode.IsExpanded = wasExpanded;
        }
    }

    private async Task CatalogDirectoryAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);

        foreach (var entry in catalogEntries)
        {
            var entryNode = new TreeViewNode { Content = entry };

            if (entry.IsDirectory)
            {
                await PopulateSubdirectoryAsync(entryNode, entry, linkItem.CategoryPath);
            }

            linkNode.Children.Add(entryNode);
        }
    }

    private async Task PopulateSubdirectoryAsync(TreeViewNode subdirNode, LinkItem subdirItem, string categoryPath)
    {
        try
        {
            var subCatalogEntries = await _categoryService.CreateSubdirectoryCatalogEntriesAsync(subdirItem.Url, categoryPath);

            foreach (var subEntry in subCatalogEntries)
            {
                var subEntryNode = new TreeViewNode { Content = subEntry };

                if (subEntry.IsDirectory)
                {
                    await PopulateSubdirectoryAsync(subEntryNode, subEntry, categoryPath);
                }

                subdirNode.Children.Add(subEntryNode);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopulateSubdirectoryAsync] Exception for '{subdirItem.Title}': {ex}");
        }
    }

    private TreeViewNode ShowCatalogingProgress(TreeViewNode linkNode, bool isZipFile, bool isRefresh = false)
    {
        var action = isRefresh ? "Refreshing" : "Cataloging";
        var tempItem = new LinkItem
        {
            Title = isZipFile ? $"{action} zip..." : $"{action} catalog...",
            Description = $"Please wait while scanning {(isZipFile ? "zip archive" : "directory")}",
            IsDirectory = false,
            IsCatalogEntry = false
        };

        var tempNode = new TreeViewNode { Content = tempItem };
        linkNode.Children.Add(tempNode);
        linkNode.IsExpanded = true;
        return tempNode;
    }

    private async Task FinalizeCatalogCreationAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        linkItem.LastCatalogUpdate = DateTime.Now;
        linkItem.ModifiedDate = DateTime.Now;
        
        _categoryService.UpdateCatalogFileCount(linkNode);
        var refreshedNode = _treeViewService.RefreshLinkNode(linkNode, linkItem);

        refreshedNode.IsExpanded = true;
        await _detailsViewService.ShowLinkDetailsAsync(linkItem, refreshedNode,
            async () => await CreateCatalogAsync(linkItem, refreshedNode),
            async () => await RefreshCatalogAsync(linkItem, refreshedNode),
            async () => await RefreshArchiveFromManifestAsync(linkItem, refreshedNode));

        var count = refreshedNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry);
        Debug.WriteLine($"[CatalogService] Successfully cataloged '{linkItem.Title}' with {count} entries");
    }

    private bool IsZipFile(string url)
    {
        return url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
    }

    private void HandleCatalogError(string action, string title, Exception ex)
    {
        Debug.WriteLine($"[CatalogService] Exception {action} catalog for '{title}': {ex}");
        // Don't throw here - let the caller handle it
    }

    public Task RefreshArchiveFromManifestAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        // TODO: Implement zip archive refresh from manifest
        return Task.CompletedTask;
    }
}