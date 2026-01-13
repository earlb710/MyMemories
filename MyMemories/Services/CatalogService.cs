using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Represents preserved metadata for a catalog entry that should survive refresh.
/// </summary>
public class CatalogEntryMetadata
{
    public List<string> TagIds { get; set; } = new();
    public List<RatingValue> Ratings { get; set; } = new();
}

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

    public async Task RefreshCatalogAsync(LinkItem linkItem, TreeViewNode linkNode, bool silent = false)
    {
        bool wasExpanded = linkNode.IsExpanded;
        bool isZipFile = IsZipFile(linkItem.Url);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Debug.WriteLine($"[RefreshCatalogAsync] Refreshing catalog for '{linkItem.Title}', IsZip: {isZipFile}, Silent: {silent}");
            
            // Extract existing tags and ratings before removing catalog entries
            var preservedMetadata = ExtractCatalogMetadata(linkNode);
            Debug.WriteLine($"[RefreshCatalogAsync] Preserved metadata for {preservedMetadata.Count} entries");
            
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

            // Restore preserved tags and ratings to matching entries
            RestoreCatalogMetadata(linkNode, preservedMetadata);
            Debug.WriteLine($"[RefreshCatalogAsync] Restored metadata to catalog entries");

            linkNode.Children.Remove(tempNode);
            await FinalizeCatalogCreationAsync(linkItem, linkNode, silent);
            
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
            // Only restore expansion state if not silent (silent mode = don't modify UI state)
            if (!silent)
            {
                linkNode.IsExpanded = wasExpanded;
            }
        }
    }

    /// <summary>
    /// Extracts tags and ratings from existing catalog entries before refresh.
    /// Key is the relative path (filename or folder name) to match after refresh.
    /// </summary>
    private Dictionary<string, CatalogEntryMetadata> ExtractCatalogMetadata(TreeViewNode linkNode)
    {
        var metadata = new Dictionary<string, CatalogEntryMetadata>(StringComparer.OrdinalIgnoreCase);
        
        ExtractMetadataRecursive(linkNode, metadata, string.Empty);
        
        return metadata;
    }

    /// <summary>
    /// Recursively extracts metadata from catalog entries.
    /// </summary>
    private void ExtractMetadataRecursive(TreeViewNode node, Dictionary<string, CatalogEntryMetadata> metadata, string parentPath)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && link.IsCatalogEntry)
            {
                // Only extract if there are tags or ratings to preserve
                if (link.TagIds.Count > 0 || link.Ratings.Count > 0)
                {
                    // Use filename/folder name as key for matching after refresh
                    var key = string.IsNullOrEmpty(parentPath) 
                        ? link.Title 
                        : $"{parentPath}/{link.Title}";
                    
                    metadata[key] = new CatalogEntryMetadata
                    {
                        TagIds = new List<string>(link.TagIds),
                        Ratings = new List<RatingValue>(link.Ratings)
                    };
                    
                    Debug.WriteLine($"[ExtractMetadataRecursive] Preserved metadata for '{key}': {link.TagIds.Count} tags, {link.Ratings.Count} ratings");
                }
                
                // Recursively process subdirectories
                if (link.IsDirectory && child.Children.Count > 0)
                {
                    var childPath = string.IsNullOrEmpty(parentPath) 
                        ? link.Title 
                        : $"{parentPath}/{link.Title}";
                    ExtractMetadataRecursive(child, metadata, childPath);
                }
            }
        }
    }

    /// <summary>
    /// Restores tags and ratings to catalog entries after refresh.
    /// </summary>
    private void RestoreCatalogMetadata(TreeViewNode linkNode, Dictionary<string, CatalogEntryMetadata> metadata)
    {
        if (metadata.Count == 0)
            return;
            
        RestoreMetadataRecursive(linkNode, metadata, string.Empty);
    }

    /// <summary>
    /// Recursively restores metadata to catalog entries.
    /// </summary>
    private void RestoreMetadataRecursive(TreeViewNode node, Dictionary<string, CatalogEntryMetadata> metadata, string parentPath)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && link.IsCatalogEntry)
            {
                var key = string.IsNullOrEmpty(parentPath) 
                    ? link.Title 
                    : $"{parentPath}/{link.Title}";
                
                if (metadata.TryGetValue(key, out var savedMetadata))
                {
                    // Restore tags
                    if (savedMetadata.TagIds.Count > 0)
                    {
                        link.TagIds = new List<string>(savedMetadata.TagIds);
                    }
                    
                    // Restore ratings
                    if (savedMetadata.Ratings.Count > 0)
                    {
                        link.Ratings = new List<RatingValue>(savedMetadata.Ratings);
                    }
                    
                    Debug.WriteLine($"[RestoreMetadataRecursive] Restored metadata to '{key}': {link.TagIds.Count} tags, {link.Ratings.Count} ratings");
                }
                
                // Recursively process subdirectories
                if (link.IsDirectory && child.Children.Count > 0)
                {
                    var childPath = string.IsNullOrEmpty(parentPath) 
                        ? link.Title 
                        : $"{parentPath}/{link.Title}";
                    RestoreMetadataRecursive(child, metadata, childPath);
                }
            }
        }
    }

    private async Task CatalogDirectoryAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);

        // Note: We don't mark directories as changed during catalog creation/refresh
        // because we're capturing the current state. Change detection only happens
        // when loading from saved JSON (in CategoryService.CheckAndMarkCatalogEntryAsChanged)

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

            int fileCount = 0;
            ulong totalSize = 0;

            foreach (var subEntry in subCatalogEntries)
            {
                var subEntryNode = new TreeViewNode { Content = subEntry };

                if (subEntry.IsDirectory)
                {
                    await PopulateSubdirectoryAsync(subEntryNode, subEntry, categoryPath);
                    
                    // Add the subdirectory's file count and size to this directory's totals
                    fileCount += subEntry.CatalogFileCount;
                    totalSize += subEntry.CatalogTotalSize;
                }
                else
                {
                    // Count files and their sizes
                    fileCount++;
                    if (subEntry.FileSize.HasValue)
                    {
                        totalSize += subEntry.FileSize.Value;
                    }
                }

                subdirNode.Children.Add(subEntryNode);
            }

            // Set the file count and total size for this subdirectory
            subdirItem.CatalogFileCount = fileCount;
            subdirItem.CatalogTotalSize = totalSize;
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

    private async Task FinalizeCatalogCreationAsync(LinkItem linkItem, TreeViewNode linkNode, bool silent = false)
    {
        linkItem.LastCatalogUpdate = DateTime.Now;
        linkItem.ModifiedDate = DateTime.Now;
        
        _categoryService.UpdateCatalogFileCount(linkNode);
        var refreshedNode = _treeViewService.RefreshLinkNode(linkNode, linkItem);

        // Only expand and select nodes if NOT in silent mode
        if (!silent)
        {
            // Expand the node and select it so user can see the refreshed content
            refreshedNode.IsExpanded = true;
            
            // Also expand the parent node if it exists (in case it collapsed)
            if (refreshedNode.Parent != null)
            {
                refreshedNode.Parent.IsExpanded = true;
            }
            
            _treeViewService.SelectNode(refreshedNode);
        }
        
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
        
        // Save the catalog to persist it
        var rootCategoryNode = GetRootCategoryNode(refreshedNode);
        if (rootCategoryNode != null)
        {
            await _categoryService.SaveCategoryAsync(rootCategoryNode);
            Debug.WriteLine($"[FinalizeCatalogCreationAsync] Saved catalog for '{linkItem.Title}' to category");
        }
        
        // Only update the details view if NOT in silent mode
        if (!silent)
        {
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode),
                isZipFile ? async () => await RefreshArchiveFromManifestAsync(linkItem, refreshedNode) : null,
                saveCallback);
        }

        var count = refreshedNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry);
        Debug.WriteLine($"[CatalogService] Successfully cataloged '{linkItem.Title}' with {count} entries (silent: {silent})");
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