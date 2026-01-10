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
    private readonly AuditLogService? _auditLogService;
    private Func<LinkItem, TreeViewNode, Task>? _refreshArchiveCallback;

    public CatalogService(
        CategoryService categoryService,
        TreeViewService treeViewService,
        DetailsViewService detailsViewService,
        ZipCatalogService zipCatalogService,
        AuditLogService? auditLogService = null)
    {
        _categoryService = categoryService;
        _treeViewService = treeViewService;
        _detailsViewService = detailsViewService;
        _zipCatalogService = zipCatalogService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Sets the callback for refreshing an archive from its manifest.
    /// This should be called from MainWindow during initialization.
    /// </summary>
    public void SetRefreshArchiveCallback(Func<LinkItem, TreeViewNode, Task> callback)
    {
        _refreshArchiveCallback = callback;
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
        var stopwatch = Stopwatch.StartNew();

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
            
            stopwatch.Stop();
            
            // Count entries for logging
            var fileCount = linkNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry && !link.IsDirectory);
            var folderCount = linkNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry && link.IsDirectory);
            
            // Audit log the refresh with duration
            await LogCatalogRefreshAsync(linkNode, linkItem.Title, fileCount, folderCount, stopwatch.Elapsed, isZipFile);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
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
        
        // Only pass RefreshArchiveFromManifestAsync for zip files
        bool isZipFile = IsZipFile(linkItem.Url);
        
        // Create save callback for auto-refresh checkbox
        Func<Task> saveCallback = async () =>
        {
            var rootNode = GetRootCategoryNode(refreshedNode);
            if (rootNode != null)
            {
                await _categoryService.SaveCategoryAsync(rootNode);
            }
        };
        
        await _detailsViewService.ShowLinkDetailsAsync(linkItem, refreshedNode,
            async () => await CreateCatalogAsync(linkItem, refreshedNode),
            async () => await RefreshCatalogAsync(linkItem, refreshedNode),
            isZipFile ? async () => await RefreshArchiveFromManifestAsync(linkItem, refreshedNode) : null,
            saveCallback);

        var count = refreshedNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry);
        Debug.WriteLine($"[CatalogService] Successfully cataloged '{linkItem.Title}' with {count} entries");
    }

    /// <summary>
    /// Gets the root category node for a given node.
    /// </summary>
    private TreeViewNode? GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100;
        
        while (current?.Parent != null && safetyCounter < maxDepth)
        {
            if (current.Content is CategoryItem)
            {
                var parent = current.Parent;
                if (parent.Content is not CategoryItem)
                {
                    return current;
                }
                current = parent;
            }
            else
            {
                current = current.Parent;
            }
            safetyCounter++;
        }
        
        if (current == null || safetyCounter >= maxDepth)
        {
            return null;
        }
        
        return current.Content is CategoryItem ? current : null;
    }

    /// <summary>
    /// Refreshes the archive catalog from the manifest file asynchronously.
    /// </summary>
    public async Task RefreshArchiveFromManifestAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (_refreshArchiveCallback != null)
        {
            await _refreshArchiveCallback(linkItem, linkNode);
            
            stopwatch.Stop();
            
            // Audit log the archive refresh with duration
            await LogArchiveRefreshAsync(linkNode, linkItem.Title, stopwatch.Elapsed);
        }
        else
        {
            Debug.WriteLine("[CatalogService] RefreshArchiveCallback not set - cannot refresh archive");
        }
    }

    /// <summary>
    /// Logs a catalog refresh operation to the audit log.
    /// </summary>
    private async Task LogCatalogRefreshAsync(TreeViewNode linkNode, string linkTitle, int fileCount, int folderCount, TimeSpan duration, bool isZipFile)
    {
        if (_auditLogService == null)
            return;
            
        var rootNode = GetRootCategoryNode(linkNode);
        if (rootNode?.Content is CategoryItem category && category.IsAuditLoggingEnabled)
        {
            var durationStr = FormatDuration(duration);
            var typeStr = isZipFile ? "Zip catalog" : "Catalog";
            await _auditLogService.LogAsync(
                category.Name,
                AuditLogType.Change,
                $"{typeStr} refreshed: {linkTitle}",
                $"Files: {fileCount}, Folders: {folderCount}, Duration: {durationStr}");
        }
    }

    /// <summary>
    /// Logs an archive refresh operation to the audit log.
    /// </summary>
    private async Task LogArchiveRefreshAsync(TreeViewNode linkNode, string linkTitle, TimeSpan duration)
    {
        if (_auditLogService == null)
            return;
            
        var rootNode = GetRootCategoryNode(linkNode);
        if (rootNode?.Content is CategoryItem category && category.IsAuditLoggingEnabled)
        {
            var durationStr = FormatDuration(duration);
            await _auditLogService.LogAsync(
                category.Name,
                AuditLogType.Change,
                $"Archive refreshed from manifest: {linkTitle}",
                $"Duration: {durationStr}");
        }
    }

    /// <summary>
    /// Formats a duration to a human-readable string.
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
        {
            return $"{duration.TotalMilliseconds:F0}ms";
        }
        else if (duration.TotalSeconds < 60)
        {
            return $"{duration.TotalSeconds:F2}s";
        }
        else if (duration.TotalMinutes < 60)
        {
            return $"{duration.TotalMinutes:F1}m";
        }
        else
        {
            return $"{duration.TotalHours:F1}h";
        }
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
}