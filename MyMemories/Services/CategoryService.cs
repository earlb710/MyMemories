using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
        
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
                var categoryData = JsonSerializer.Deserialize<CategoryData>(json, _jsonOptions);

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
            Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description,
            Icon = category.Icon == "📁" ? null : category.Icon,
            CreatedDate = category.CreatedDate,
            ModifiedDate = category.ModifiedDate,
            Links = null,
            SubCategories = null
        };

        var links = new List<LinkData>();
        var subCategories = new List<CategoryData>();

        // Process all children
        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                var linkData = new LinkData
                {
                    Title = link.Title,
                    Url = link.Url,
                    Description = string.IsNullOrWhiteSpace(link.Description) ? null : link.Description,
                    IsDirectory = link.IsDirectory ? true : null,
                    CategoryPath = link.CategoryPath,
                    CreatedDate = link.CreatedDate,
                    ModifiedDate = link.ModifiedDate,
                    FolderType = link.IsDirectory && link.FolderType != FolderLinkType.LinkOnly ? link.FolderType : null,
                    FileFilters = !string.IsNullOrWhiteSpace(link.FileFilters) ? link.FileFilters : null,
                    IsCatalogEntry = link.IsCatalogEntry ? true : null,
                    LastCatalogUpdate = link.LastCatalogUpdate,
                    FileSize = link.FileSize,
                    AutoRefreshCatalog = link.AutoRefreshCatalog ? true : null, // NEW LINE
                    CatalogEntries = null
                };

                // Process catalog entries
                if (child.Children.Count > 0)
                {
                    var catalogEntries = new List<LinkData>();
                    
                    foreach (var catalogChild in child.Children)
                    {
                        if (catalogChild.Content is LinkItem catalogEntry)
                        {
                            string relativeUrl = catalogEntry.Url;
                            if (!string.IsNullOrEmpty(link.Url) && catalogEntry.Url.StartsWith(link.Url))
                            {
                                relativeUrl = Path.GetFileName(catalogEntry.Url);
                            }

                            catalogEntries.Add(new LinkData
                            {
                                Title = catalogEntry.Title,
                                Url = relativeUrl,
                                Description = string.IsNullOrWhiteSpace(catalogEntry.Description) ? null : catalogEntry.Description,
                                IsDirectory = null,
                                CategoryPath = catalogEntry.CategoryPath,
                                CreatedDate = catalogEntry.CreatedDate,
                                ModifiedDate = catalogEntry.ModifiedDate,
                                FolderType = null,
                                FileFilters = null,
                                IsCatalogEntry = null,
                                LastCatalogUpdate = null,
                                FileSize = catalogEntry.FileSize,
                                CatalogEntries = null
                            });
                        }
                    }
                    
                    if (catalogEntries.Count > 0)
                    {
                        linkData.CatalogEntries = catalogEntries;
                    }
                }

                links.Add(linkData);
            }
            else if (child.Content is CategoryItem)
            {
                var subCategoryData = ConvertNodeToCategoryData(child);
                subCategories.Add(subCategoryData);
            }
        }

        if (links.Count > 0)
        {
            categoryData.Links = links;
        }
        
        if (subCategories.Count > 0)
        {
            categoryData.SubCategories = subCategories;
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
                Description = categoryData.Description ?? string.Empty,
                Icon = categoryData.Icon ?? "📁",
                CreatedDate = categoryData.CreatedDate ?? DateTime.Now,
                ModifiedDate = categoryData.ModifiedDate ?? DateTime.Now
            }
        };

        // Add subcategories first
        if (categoryData.SubCategories != null)
        {
            foreach (var subCategoryData in categoryData.SubCategories)
            {
                var subCategoryNode = CreateCategoryNode(subCategoryData);
                categoryNode.Children.Add(subCategoryNode);
            }
        }

        // Add links with their catalog entries
        if (categoryData.Links != null)
        {
            foreach (var linkData in categoryData.Links)
            {
                var linkItem = new LinkItem
                {
                    Title = linkData.Title ?? string.Empty,
                    Url = linkData.Url ?? string.Empty,
                    Description = linkData.Description ?? string.Empty,
                    IsDirectory = linkData.IsDirectory ?? false,
                    CategoryPath = linkData.CategoryPath ?? string.Empty,
                    CreatedDate = linkData.CreatedDate ?? DateTime.Now,
                    ModifiedDate = linkData.ModifiedDate ?? DateTime.Now,
                    FolderType = linkData.FolderType ?? FolderLinkType.LinkOnly,
                    FileFilters = linkData.FileFilters ?? string.Empty,
                    IsCatalogEntry = linkData.IsCatalogEntry ?? false,
                    LastCatalogUpdate = linkData.LastCatalogUpdate,
                    FileSize = linkData.FileSize,
                    AutoRefreshCatalog = linkData.AutoRefreshCatalog ?? false // NEW LINE
                };

                if (linkData.CatalogEntries != null)
                {
                    linkItem.CatalogFileCount = linkData.CatalogEntries.Count;
                }

                var linkNode = new TreeViewNode { Content = linkItem };

                // Add catalog entries as children
                if (linkData.CatalogEntries != null)
                {
                    foreach (var catalogData in linkData.CatalogEntries)
                    {
                        var fullUrl = string.IsNullOrEmpty(linkData.Url) 
                            ? catalogData.Url 
                            : Path.Combine(linkData.Url, catalogData.Url ?? string.Empty);

                        var catalogEntry = new LinkItem
                        {
                            Title = catalogData.Title ?? string.Empty,
                            Url = fullUrl,
                            Description = catalogData.Description ?? string.Empty,
                            IsDirectory = false,
                            CategoryPath = catalogData.CategoryPath ?? string.Empty,
                            CreatedDate = catalogData.CreatedDate ?? DateTime.Now,
                            ModifiedDate = catalogData.ModifiedDate ?? DateTime.Now,
                            FolderType = FolderLinkType.LinkOnly,
                            FileFilters = string.Empty,
                            IsCatalogEntry = true,
                            LastCatalogUpdate = null,
                            FileSize = catalogData.FileSize
                        };

                        var catalogEntryNode = new TreeViewNode { Content = catalogEntry };
                        linkNode.Children.Add(catalogEntryNode);
                    }
                }

                categoryNode.Children.Add(linkNode);
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
    /// Returns LinkItems with full paths for TreeView usage.
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
                
                // Generate rich description based on file type
                var description = await FileMetadataService.GenerateFileDescriptionAsync(filePath);
                
                catalogEntries.Add(new LinkItem
                {
                    Title = fileInfo.Name,
                    Url = filePath,
                    Description = description,
                    IsDirectory = false,
                    CategoryPath = categoryPath,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true,
                    FileSize = (ulong)fileInfo.Length
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

    /// <summary>
    /// Updates the catalog file count for a link item based on its children.
    /// </summary>
    public void UpdateCatalogFileCount(TreeViewNode linkNode)
    {
        if (linkNode.Content is LinkItem link && link.IsDirectory)
        {
            var catalogCount = linkNode.Children.Count(child => 
                child.Content is LinkItem catalogEntry && catalogEntry.IsCatalogEntry);
            
            link.CatalogFileCount = catalogCount;
        }
    }
}