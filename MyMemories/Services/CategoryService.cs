using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

/// <summary>
/// Service for managing category data persistence and operations.
/// </summary>
public class CategoryService
{
    private readonly string _dataFolder;
    private readonly JsonSerializerOptions _jsonOptions;

    public CategoryService(string dataFolder)
    {
        _dataFolder = dataFolder;
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        // Ensure data folder exists
        Directory.CreateDirectory(_dataFolder);
    }

    /// <summary>
    /// Loads all categories from JSON files.
    /// </summary>
    public async Task<List<TreeViewNode>> LoadAllCategoriesAsync()
    {
        var categories = new List<TreeViewNode>();

        if (!Directory.Exists(_dataFolder))
        {
            return categories;
        }

        var jsonFiles = Directory.GetFiles(_dataFolder, "*.json");

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(jsonFile);
                var categoryData = JsonSerializer.Deserialize<CategoryData>(json);

                if (categoryData != null)
                {
                    var categoryNode = CreateCategoryNode(categoryData);
                    categories.Add(categoryNode);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading {Path.GetFileName(jsonFile)}: {ex.Message}", ex);
            }
        }

        return categories;
    }

    /// <summary>
    /// Saves a category to a JSON file.
    /// </summary>
    public async Task SaveCategoryAsync(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem category)
        {
            throw new ArgumentException("Node must contain a CategoryItem", nameof(categoryNode));
        }

        var categoryData = ConvertNodeToCategoryData(categoryNode);

        var json = JsonSerializer.Serialize(categoryData, _jsonOptions);
        var fileName = SanitizeFileName(category.Name) + ".json";
        var filePath = Path.Combine(_dataFolder, fileName);
        
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Deletes a category file.
    /// </summary>
    public Task DeleteCategoryAsync(string categoryName)
    {
        var fileName = SanitizeFileName(categoryName) + ".json";
        var filePath = Path.Combine(_dataFolder, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Recursively converts a TreeViewNode to CategoryData including all subcategories and links.
    /// </summary>
    private CategoryData ConvertNodeToCategoryData(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem category)
        {
            throw new ArgumentException("Node must contain a CategoryItem");
        }

        var categoryData = new CategoryData
        {
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            CreatedDate = category.CreatedDate,
            ModifiedDate = category.ModifiedDate,
            Links = new List<LinkData>(),
            SubCategories = new List<CategoryData>()
        };

        // Process all children
        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Create link data with potential catalog entries as children
                var linkData = new LinkData
                {
                    Title = link.Title,
                    Url = link.Url,
                    Description = link.Description,
                    IsDirectory = link.IsDirectory,
                    CategoryPath = link.CategoryPath,
                    CreatedDate = link.CreatedDate,
                    ModifiedDate = link.ModifiedDate,
                    FolderType = link.FolderType,
                    FileFilters = link.FileFilters,
                    IsCatalogEntry = link.IsCatalogEntry,
                    LastCatalogUpdate = link.LastCatalogUpdate,
                    CatalogEntries = new List<LinkData>() // Initialize catalog entries list
                };

                // Process catalog entries (children of this link) as nested items
                foreach (var catalogChild in child.Children)
                {
                    if (catalogChild.Content is LinkItem catalogEntry)
                    {
                        // Convert full path to relative path (just the filename)
                        string relativeUrl = catalogEntry.Url;
                        if (!string.IsNullOrEmpty(link.Url) && catalogEntry.Url.StartsWith(link.Url))
                        {
                            // Extract just the filename from the full path
                            relativeUrl = Path.GetFileName(catalogEntry.Url);
                        }

                        linkData.CatalogEntries.Add(new LinkData
                        {
                            Title = catalogEntry.Title,
                            Url = relativeUrl, // Store relative path (filename only)
                            Description = catalogEntry.Description,
                            IsDirectory = catalogEntry.IsDirectory,
                            CategoryPath = catalogEntry.CategoryPath,
                            CreatedDate = catalogEntry.CreatedDate,
                            ModifiedDate = catalogEntry.ModifiedDate,
                            FolderType = catalogEntry.FolderType,
                            FileFilters = catalogEntry.FileFilters,
                            IsCatalogEntry = catalogEntry.IsCatalogEntry,
                            LastCatalogUpdate = catalogEntry.LastCatalogUpdate
                        });
                    }
                }

                categoryData.Links.Add(linkData);
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively add subcategory
                var subCategoryData = ConvertNodeToCategoryData(child);
                categoryData.SubCategories.Add(subCategoryData);
            }
        }

        return categoryData;
    }

    /// <summary>
    /// Creates a TreeViewNode from CategoryData recursively.
    /// </summary>
    private TreeViewNode CreateCategoryNode(CategoryData categoryData)
    {
        var categoryNode = new TreeViewNode
        {
            Content = new CategoryItem
            {
                Name = categoryData.Name,
                Description = categoryData.Description,
                Icon = categoryData.Icon,
                CreatedDate = categoryData.CreatedDate ?? DateTime.Now,
                ModifiedDate = categoryData.ModifiedDate ?? DateTime.Now
            }
        };

        // Add subcategories first (they should appear before links)
        foreach (var subCategoryData in categoryData.SubCategories)
        {
            var subCategoryNode = CreateCategoryNode(subCategoryData);
            categoryNode.Children.Add(subCategoryNode);
        }

        // Add links with their catalog entries
        foreach (var linkData in categoryData.Links)
        {
            var linkItem = new LinkItem
            {
                Title = linkData.Title,
                Url = linkData.Url,
                Description = linkData.Description,
                IsDirectory = linkData.IsDirectory,
                CategoryPath = linkData.CategoryPath,
                CreatedDate = linkData.CreatedDate ?? DateTime.Now,
                ModifiedDate = linkData.ModifiedDate ?? DateTime.Now,
                FolderType = linkData.FolderType,
                FileFilters = linkData.FileFilters ?? string.Empty,
                IsCatalogEntry = linkData.IsCatalogEntry,
                LastCatalogUpdate = linkData.LastCatalogUpdate
            };

            var linkNode = new TreeViewNode { Content = linkItem };

            // Add catalog entries as children of the link node
            if (linkData.CatalogEntries != null)
            {
                foreach (var catalogData in linkData.CatalogEntries)
                {
                    // Reconstruct full path from parent URL + relative path
                    var fullUrl = string.IsNullOrEmpty(linkData.Url) 
                        ? catalogData.Url 
                        : Path.Combine(linkData.Url, catalogData.Url);

                    var catalogEntry = new LinkItem
                    {
                        Title = catalogData.Title,
                        Url = fullUrl, // Use full path for TreeView
                        Description = catalogData.Description,
                        IsDirectory = catalogData.IsDirectory,
                        CategoryPath = catalogData.CategoryPath,
                        CreatedDate = catalogData.CreatedDate ?? DateTime.Now,
                        ModifiedDate = catalogData.ModifiedDate ?? DateTime.Now,
                        FolderType = catalogData.FolderType,
                        FileFilters = catalogData.FileFilters ?? string.Empty,
                        IsCatalogEntry = true,
                        LastCatalogUpdate = catalogData.LastCatalogUpdate
                    };

                    var catalogEntryNode = new TreeViewNode { Content = catalogEntry };
                    linkNode.Children.Add(catalogEntryNode);
                }
            }

            categoryNode.Children.Add(linkNode);
        }

        return categoryNode;
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    /// <summary>
    /// Creates a catalog of all files in a directory and adds them as catalog entries to a folder link.
    /// Returns LinkItems with full paths for TreeView usage.
    /// </summary>
    public async Task<List<LinkItem>> CreateCatalogEntriesAsync(string directoryPath, string categoryPath)
    {
        Debug.WriteLine($"[CategoryService] CreateCatalogEntriesAsync called for: {directoryPath}");
        var catalogEntries = new List<LinkItem>();

        if (!Directory.Exists(directoryPath))
        {
            Debug.WriteLine($"[CategoryService] Directory does not exist: {directoryPath}");
            return catalogEntries;
        }

        try
        {
            var files = Directory.GetFiles(directoryPath);
            Debug.WriteLine($"[CategoryService] Found {files.Length} files in directory");

            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                
                Debug.WriteLine($"[CategoryService] Creating catalog entry: {fileInfo.Name} (full path: {filePath})");
                
                catalogEntries.Add(new LinkItem
                {
                    Title = fileInfo.Name,
                    Url = filePath, // Full path for TreeView usage
                    Description = $"Size: {FormatFileSize((ulong)fileInfo.Length)}",
                    IsDirectory = false,
                    CategoryPath = categoryPath,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true
                });
            }
            
            Debug.WriteLine($"[CategoryService] Created {catalogEntries.Count} catalog entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CategoryService] ERROR in CreateCatalogEntriesAsync: {ex.Message}");
            throw new InvalidOperationException($"Error creating catalog for {directoryPath}: {ex.Message}", ex);
        }

        return catalogEntries;
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

    /// <summary>
    /// Checks if a node already has catalog entries.
    /// </summary>
    public bool HasCatalogEntries(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && link.IsCatalogEntry)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes all catalog entries from a node.
    /// </summary>
    public void RemoveCatalogEntries(TreeViewNode node)
    {
        var catalogEntries = node.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .ToList();

        foreach (var entry in catalogEntries)
        {
            node.Children.Remove(entry);
        }
    }
}