using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Services;

public class ZipCatalogService
{
    private readonly CategoryService _categoryService;
    private readonly TreeViewService _treeViewService;

    public ZipCatalogService(CategoryService categoryService, TreeViewService treeViewService)
    {
        _categoryService = categoryService;
        _treeViewService = treeViewService;
    }

    /// <summary>
    /// Creates a catalog of all files inside a zip archive.
    /// </summary>
    public async Task CatalogZipFileAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        if (!File.Exists(zipLinkItem.Url))
        {
            Debug.WriteLine($"[CatalogZipFileAsync] Zip file not found: {zipLinkItem.Url}");
            return;
        }

        try
        {
            Debug.WriteLine($"[CatalogZipFileAsync] Cataloging zip file: {zipLinkItem.Url}");

            // Read zip archive on background thread and collect data
            var catalogData = await Task.Run(() =>
            {
                var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
                
                using (var archive = ZipFile.OpenRead(zipLinkItem.Url))
                {
                    // Group entries by their immediate parent directory - get root level items only
                    var rootEntries = archive.Entries
                        .Where(entry => !string.IsNullOrEmpty(entry.Name)) // Skip directory-only entries
                        .Where(entry => !entry.FullName.Contains('/') || entry.FullName.Split('/').Length == 2) // Root level files or first-level subdirs
                        .ToList();

                    foreach (var entry in rootEntries)
                    {
                        entries.Add((
                            entry.Name,
                            entry.FullName,
                            entry.FullName.EndsWith("/"),
                            entry.Length,
                            entry.LastWriteTime.DateTime
                        ));
                    }
                }
                
                return entries;
            });

            Debug.WriteLine($"[CatalogZipFileAsync] Found {catalogData.Count} root entries");

            // Create TreeViewNode objects on UI thread
            foreach (var (name, fullName, isDirectory, length, lastWrite) in catalogData)
            {
                var entryName = isDirectory 
                    ? fullName.TrimEnd('/').Split('/').Last()
                    : name;

                var catalogEntry = new LinkItem
                {
                    Title = entryName,
                    Url = $"{zipLinkItem.Url}::{fullName}", // Special format for zip entries
                    Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FormatFileSize((ulong)length)})",
                    IsDirectory = isDirectory,
                    CategoryPath = zipLinkItem.CategoryPath,
                    CreatedDate = lastWrite,
                    ModifiedDate = lastWrite,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true,
                    FileSize = isDirectory ? null : (ulong)length
                };

                var catalogNode = new TreeViewNode { Content = catalogEntry };

                // If it's a directory, catalog its contents recursively
                if (isDirectory)
                {
                    await CatalogZipDirectoryRecursiveAsync(zipLinkItem.Url, fullName, catalogNode, zipLinkItem);
                }

                zipLinkNode.Children.Add(catalogNode);
            }

            // Update file count - this will populate CatalogFileCount
            _categoryService.UpdateCatalogFileCount(zipLinkNode);
            
            // Trigger property change notification to update the display text
            if (zipLinkNode.Content is LinkItem linkItem)
            {
                linkItem.RefreshChangeStatus();
            }

            Debug.WriteLine($"[CatalogZipFileAsync] Successfully cataloged zip with {zipLinkNode.Children.Count} entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogZipFileAsync] Error cataloging zip file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Recursively catalogs subdirectories within a zip archive.
    /// </summary>
    private async Task CatalogZipDirectoryRecursiveAsync(string zipPath, string directoryPath, TreeViewNode parentNode, LinkItem zipLinkItem)
    {
        Debug.WriteLine($"[CatalogZipDirectoryRecursiveAsync] Cataloging subdirectory: {directoryPath}");

        // Read subdirectory entries on background thread
        var subCatalogData = await Task.Run(() =>
        {
            var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
            
            var dirPath = directoryPath.TrimEnd('/') + "/";
            
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var subEntries = archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name))
                    .Where(entry => entry.FullName.StartsWith(dirPath) && entry.FullName != dirPath)
                    .Where(entry => entry.FullName.Substring(dirPath.Length).Split('/').Length <= 2) // Direct children only
                    .ToList();

                foreach (var entry in subEntries)
                {
                    entries.Add((
                        entry.Name,
                        entry.FullName,
                        entry.FullName.EndsWith("/"),
                        entry.Length,
                        entry.LastWriteTime.DateTime
                    ));
                }
            }
            
            return entries;
        });

        Debug.WriteLine($"[CatalogZipDirectoryRecursiveAsync] Found {subCatalogData.Count} entries in {directoryPath}");

        // Create TreeViewNode objects on UI thread
        foreach (var (name, fullName, isDirectory, length, lastWrite) in subCatalogData)
        {
            var relativePath = fullName.Substring(directoryPath.TrimEnd('/').Length + 1);
            var entryName = isDirectory 
                ? relativePath.TrimEnd('/').Split('/').Last()
                : name;

            var catalogEntry = new LinkItem
            {
                Title = entryName,
                Url = $"{zipLinkItem.Url}::{fullName}",
                Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FormatFileSize((ulong)length)})",
                IsDirectory = isDirectory,
                CategoryPath = zipLinkItem.CategoryPath,
                CreatedDate = lastWrite,
                ModifiedDate = lastWrite,
                FolderType = FolderLinkType.LinkOnly,
                IsCatalogEntry = true,
                FileSize = isDirectory ? null : (ulong)length
            };

            var catalogNode = new TreeViewNode { Content = catalogEntry };

            // Recursively catalog subdirectories
            if (isDirectory)
            {
                await CatalogZipDirectoryRecursiveAsync(zipPath, fullName, catalogNode, zipLinkItem);
            }

            parentNode.Children.Add(catalogNode);
        }
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}