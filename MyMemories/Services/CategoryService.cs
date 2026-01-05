using System;
using System.Collections.Generic;
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
                // Add link
                categoryData.Links.Add(new LinkData
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
                    LastCatalogUpdate = link.LastCatalogUpdate // NEW
                });

                // Process catalog entries (children of this link)
                foreach (var catalogChild in child.Children)
                {
                    if (catalogChild.Content is LinkItem catalogEntry)
                    {
                        categoryData.Links.Add(new LinkData
                        {
                            Title = catalogEntry.Title,
                            Url = catalogEntry.Url,
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

        // Track link nodes that have catalog entries
        var linksWithCatalogs = new Dictionary<string, TreeViewNode>();

        // Then add links
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
                LastCatalogUpdate = linkData.LastCatalogUpdate // NEW
            };

            if (linkItem.IsCatalogEntry)
            {
                // This is a catalog entry - find its parent link node
                // Parent links are identified by matching directory path
                var parentUrl = linkData.CategoryPath; // This might need adjustment based on your data structure
                
                // Try to find the parent link node in our temporary tracking dictionary
                // For now, we'll use a simple approach: catalog entries follow their parent link
                // So we look at the last non-catalog link added
                TreeViewNode? parentLinkNode = null;
                foreach (var kvp in linksWithCatalogs.Reverse())
                {
                    if (kvp.Value.Content is LinkItem parentLink && 
                        parentLink.IsDirectory && 
                        !parentLink.IsCatalogEntry)
                    {
                        // Check if this catalog entry belongs to this parent
                        // (catalog entries should have parent folder's path in their URL)
                        if (linkItem.Url.StartsWith(parentLink.Url + Path.DirectorySeparatorChar))
                        {
                            parentLinkNode = kvp.Value;
                            break;
                        }
                    }
                }

                if (parentLinkNode != null)
                {
                    // Add as child of the parent link
                    var catalogEntryNode = new TreeViewNode { Content = linkItem };
                    parentLinkNode.Children.Add(catalogEntryNode);
                    continue; // Skip adding to category node
                }
            }

            // Regular link - add to category
            var linkNode = new TreeViewNode { Content = linkItem };
            categoryNode.Children.Add(linkNode);
            
            // Track this link node if it's a directory (potential parent of catalog entries)
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry)
            {
                linksWithCatalogs[linkItem.Url] = linkNode;
            }
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
    /// </summary>
    public async Task<List<LinkItem>> CreateCatalogEntriesAsync(string directoryPath, string categoryPath)
    {
        var catalogEntries = new List<LinkItem>();

        if (!Directory.Exists(directoryPath))
        {
            return catalogEntries;
        }

        try
        {
            var files = Directory.GetFiles(directoryPath);

            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                
                catalogEntries.Add(new LinkItem
                {
                    Title = fileInfo.Name,
                    Url = filePath,
                    Description = $"Size: {FormatFileSize((ulong)fileInfo.Length)}",
                    IsDirectory = false,
                    CategoryPath = categoryPath,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true // Mark as catalog entry
                });
            }
        }
        catch (Exception ex)
        {
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