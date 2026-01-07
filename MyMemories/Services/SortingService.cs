using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyMemories.Services;

/// <summary>
/// Service for sorting categories and catalog items in the tree view.
/// </summary>
public static class SortingService
{
    /// <summary>
    /// Sorts the children of a category node based on the specified sort option.
    /// </summary>
    public static void SortCategoryChildren(TreeViewNode categoryNode, SortOption sortOption)
    {
        if (categoryNode.Content is not CategoryItem category)
            return;

        var children = categoryNode.Children.ToList();
        
        // Separate categories and links
        var categories = children.Where(n => n.Content is CategoryItem).ToList();
        var links = children.Where(n => n.Content is LinkItem).ToList();

        // Sort each group
        var sortedCategories = SortCategories(categories, sortOption);
        var sortedLinks = SortLinks(links, sortOption);

        // Clear and re-add in sorted order (categories first, then links)
        categoryNode.Children.Clear();
        
        foreach (var node in sortedCategories)
        {
            categoryNode.Children.Add(node);
        }
        
        foreach (var node in sortedLinks)
        {
            categoryNode.Children.Add(node);
        }

        // Update the category's sort order
        category.SortOrder = sortOption;
    }

    /// <summary>
    /// Sorts catalog entries within a folder link.
    /// </summary>
    public static void SortCatalogEntries(TreeViewNode linkNode, SortOption sortOption)
    {
        if (linkNode.Content is not LinkItem link || !link.IsDirectory)
            return;

        var children = linkNode.Children.ToList();
        
        // Separate directories and files
        var directories = children.Where(n => n.Content is LinkItem l && l.IsDirectory).ToList();
        var files = children.Where(n => n.Content is LinkItem l && !l.IsDirectory).ToList();

        // Sort each group
        var sortedDirectories = SortLinks(directories, sortOption);
        var sortedFiles = SortLinks(files, sortOption);

        // Clear and re-add in sorted order (directories first, then files)
        linkNode.Children.Clear();
        
        foreach (var node in sortedDirectories)
        {
            linkNode.Children.Add(node);
        }
        
        foreach (var node in sortedFiles)
        {
            linkNode.Children.Add(node);
        }

        // Update the link's catalog sort order
        link.CatalogSortOrder = sortOption;
    }

    /// <summary>
    /// Recursively sorts all catalog entries in a folder and its subdirectories.
    /// </summary>
    public static void SortCatalogEntriesRecursive(TreeViewNode linkNode, SortOption sortOption)
    {
        if (linkNode.Content is not LinkItem link || !link.IsDirectory)
            return;

        // Sort current level
        SortCatalogEntries(linkNode, sortOption);

        // Recursively sort subdirectories
        var subdirectories = linkNode.Children
            .Where(n => n.Content is LinkItem l && l.IsDirectory)
            .ToList();

        foreach (var subdir in subdirectories)
        {
            SortCatalogEntriesRecursive(subdir, sortOption);
        }
    }

    private static List<TreeViewNode> SortCategories(List<TreeViewNode> categories, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.NameAscending => categories
                .OrderBy(n => (n.Content as CategoryItem)?.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.NameDescending => categories
                .OrderByDescending(n => (n.Content as CategoryItem)?.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.DateAscending => categories
                .OrderBy(n => (n.Content as CategoryItem)?.CreatedDate ?? DateTime.MinValue)
                .ToList(),
            
            SortOption.DateDescending => categories
                .OrderByDescending(n => (n.Content as CategoryItem)?.CreatedDate ?? DateTime.MinValue)
                .ToList(),
            
            // Size sorting not applicable for categories
            SortOption.SizeAscending or SortOption.SizeDescending => categories
                .OrderBy(n => (n.Content as CategoryItem)?.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            _ => categories
        };
    }

    private static List<TreeViewNode> SortLinks(List<TreeViewNode> links, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.NameAscending => links
                .OrderBy(n => (n.Content as LinkItem)?.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.NameDescending => links
                .OrderByDescending(n => (n.Content as LinkItem)?.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.SizeAscending => links
                .OrderBy(n => GetLinkSize(n))
                .ThenBy(n => (n.Content as LinkItem)?.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.SizeDescending => links
                .OrderByDescending(n => GetLinkSize(n))
                .ThenBy(n => (n.Content as LinkItem)?.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            
            SortOption.DateAscending => links
                .OrderBy(n => (n.Content as LinkItem)?.ModifiedDate ?? DateTime.MinValue)
                .ToList(),
            
            SortOption.DateDescending => links
                .OrderByDescending(n => (n.Content as LinkItem)?.ModifiedDate ?? DateTime.MinValue)
                .ToList(),
            
            _ => links
        };
    }

    private static ulong GetLinkSize(TreeViewNode node)
    {
        if (node.Content is not LinkItem link)
            return 0;

        // If FileSize is already set, use it
        if (link.FileSize.HasValue)
            return link.FileSize.Value;

        // Try to get size from file system
        try
        {
            if (link.IsDirectory && Directory.Exists(link.Url))
            {
                // For directories, calculate total size
                var dirInfo = new DirectoryInfo(link.Url);
                return (ulong)dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            else if (File.Exists(link.Url))
            {
                var fileInfo = new FileInfo(link.Url);
                return (ulong)fileInfo.Length;
            }
        }
        catch
        {
            // If we can't access the file/directory, return 0
        }

        return 0;
    }

    /// <summary>
    /// Gets a user-friendly display name for a sort option.
    /// </summary>
    public static string GetSortOptionDisplayName(SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.NameAscending => "Name (A-Z)",
            SortOption.NameDescending => "Name (Z-A)",
            SortOption.SizeAscending => "Size (Smallest First)",
            SortOption.SizeDescending => "Size (Largest First)",
            SortOption.DateAscending => "Date (Oldest First)",
            SortOption.DateDescending => "Date (Newest First)",
            _ => "Name (A-Z)"
        };
    }
}