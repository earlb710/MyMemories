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
        try
        {
            if (categoryNode.Content is not CategoryItem category)
            {
                throw new ArgumentException("Node must contain a CategoryItem", nameof(categoryNode));
            }

            System.Diagnostics.Debug.WriteLine($"[SaveCategory] Starting save for category: {category.Name}");
            
            var categoryData = ConvertNodeToCategoryData(categoryNode);
            
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] Converted to CategoryData successfully");

            var json = JsonSerializer.Serialize(categoryData, _jsonOptions);
            
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] Serialized to JSON successfully");
            
            var fileName = SanitizeFileName(category.Name) + ".json";
            var filePath = Path.Combine(_dataFolder, fileName);
            
            await File.WriteAllTextAsync(filePath, json);
            
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] Saved to file successfully: {fileName}");
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] ArgumentException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] ParamName: {ex.ParamName}");
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] StackTrace: {ex.StackTrace}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] Exception: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SaveCategory] StackTrace: {ex.StackTrace}");
            throw;
        }
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
        try
        {
            if (categoryNode.Content is not CategoryItem category)
            {
                var contentType = categoryNode.Content?.GetType().Name ?? "null";
                System.Diagnostics.Debug.WriteLine($"[ConvertNode] ERROR: Expected CategoryItem, got {contentType}");
                throw new ArgumentException($"Node must contain a CategoryItem, but contains: {contentType}");
            }

            System.Diagnostics.Debug.WriteLine($"[ConvertNode] Processing category: {category.Name}");

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

            System.Diagnostics.Debug.WriteLine($"[ConvertNode] Processing {categoryNode.Children.Count} children");

            // Process all children
            foreach (var child in categoryNode.Children)
            {
                if (child.Content is LinkItem link)
                {
                    // Skip catalog entries that are direct children of categories
                    // (they should only be children of folder links)
                    if (link.IsCatalogEntry)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConvertNode] WARNING: Skipping orphaned catalog entry: {link.Title}");
                        continue;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[ConvertNode] Processing link: {link.Title}, IsDir: {link.IsDirectory}, IsCatalogEntry: {link.IsCatalogEntry}");
                    
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
                        AutoRefreshCatalog = link.AutoRefreshCatalog ? true : null,
                        CatalogEntries = null
                    };

                    // Process catalog entries (only for non-catalog-entry links)
                    if (child.Children.Count > 0 && !link.IsCatalogEntry)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConvertNode] Link has {child.Children.Count} catalog children");
                        
                        var catalogEntries = new List<LinkData>();
                        
                        foreach (var catalogChild in child.Children)
                        {
                            if (catalogChild.Content is LinkItem catalogEntry)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConvertNode]   Catalog entry: {catalogEntry.Title}, IsDir: {catalogEntry.IsDirectory}, Children: {catalogChild.Children.Count}");
                                
                                string relativeUrl = catalogEntry.Url;
                                if (!string.IsNullOrEmpty(link.Url) && catalogEntry.Url.StartsWith(link.Url, StringComparison.OrdinalIgnoreCase))
                                {
                                    relativeUrl = catalogEntry.IsDirectory 
                                        ? new DirectoryInfo(catalogEntry.Url).Name
                                        : Path.GetFileName(catalogEntry.Url);
                                }

                                var catalogData = new LinkData
                                {
                                    Title = catalogEntry.Title,
                                    Url = relativeUrl,
                                    Description = string.IsNullOrWhiteSpace(catalogEntry.Description) ? null : catalogEntry.Description,
                                    IsDirectory = catalogEntry.IsDirectory ? true : null,
                                    CategoryPath = catalogEntry.CategoryPath,
                                    CreatedDate = catalogEntry.CreatedDate,
                                    ModifiedDate = catalogEntry.ModifiedDate,
                                    FolderType = null,
                                    FileFilters = null,
                                    IsCatalogEntry = null,
                                    LastCatalogUpdate = null,
                                    FileSize = catalogEntry.FileSize,
                                    CatalogEntries = null
                                };

                                // Recursively process subdirectory catalog entries ONLY if they have children
                                if (catalogEntry.IsDirectory && catalogChild.Children.Count > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ConvertNode]     Processing subdirectory: {catalogEntry.Title} with {catalogChild.Children.Count} children");
                                    
                                    var subCatalogEntries = new List<LinkData>();
                                    
                                    foreach (var subCatalogChild in catalogChild.Children)
                                    {
                                        if (subCatalogChild.Content is LinkItem subCatalogEntry)
                                        {
                                            string subRelativeUrl = subCatalogEntry.Url;
                                            if (!string.IsNullOrEmpty(catalogEntry.Url) && subCatalogEntry.Url.StartsWith(catalogEntry.Url, StringComparison.OrdinalIgnoreCase))
                                            {
                                                subRelativeUrl = subCatalogEntry.IsDirectory
                                                    ? new DirectoryInfo(subCatalogEntry.Url).Name
                                                    : Path.GetFileName(subCatalogEntry.Url);
                                            }

                                            subCatalogEntries.Add(new LinkData
                                            {
                                                Title = subCatalogEntry.Title,
                                                Url = subRelativeUrl,
                                                Description = string.IsNullOrWhiteSpace(subCatalogEntry.Description) ? null : subCatalogEntry.Description,
                                                IsDirectory = subCatalogEntry.IsDirectory ? true : null,
                                                CategoryPath = subCatalogEntry.CategoryPath,
                                                CreatedDate = subCatalogEntry.CreatedDate,
                                                ModifiedDate = subCatalogEntry.ModifiedDate,
                                                FileSize = subCatalogEntry.FileSize,
                                                CatalogEntries = null  // IMPORTANT: Don't go deeper than 2 levels
                                            });
                                        }
                                    }
                                    
                                    if (subCatalogEntries.Count > 0)
                                    {
                                        catalogData.CatalogEntries = subCatalogEntries;
                                        System.Diagnostics.Debug.WriteLine($"[ConvertNode]     Added {subCatalogEntries.Count} sub-entries");
                                    }
                                }

                                catalogEntries.Add(catalogData);
                            }
                        }
                        
                        if (catalogEntries.Count > 0)
                        {
                            linkData.CatalogEntries = catalogEntries;
                            System.Diagnostics.Debug.WriteLine($"[ConvertNode] Added {catalogEntries.Count} catalog entries to link");
                        }
                    }

                    links.Add(linkData);
                }
                else if (child.Content is CategoryItem)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConvertNode] Recursing into subcategory");
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

            System.Diagnostics.Debug.WriteLine($"[ConvertNode] Successfully converted category: {category.Name} with {links.Count} links and {subCategories.Count} subcategories");
            
            return categoryData;
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConvertNode] ArgumentException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ConvertNode] StackTrace: {ex.StackTrace}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConvertNode] Exception: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ConvertNode] StackTrace: {ex.StackTrace}");
            throw;
        }
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
                    // Count only files (not subdirectories) for the file count
                    linkItem.CatalogFileCount = linkData.CatalogEntries.Count(entry => entry.IsDirectory != true);
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
                            IsDirectory = catalogData.IsDirectory ?? false,
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
                        
                        // Recursively add sub-catalog entries (files within subdirectories)
                        if (catalogData.CatalogEntries != null && catalogData.CatalogEntries.Count > 0)
                        {
                            foreach (var subCatalogData in catalogData.CatalogEntries)
                            {
                                var subFullUrl = Path.Combine(fullUrl, subCatalogData.Url ?? string.Empty);
                                
                                var subCatalogEntry = new LinkItem
                                {
                                    Title = subCatalogData.Title ?? string.Empty,
                                    Url = subFullUrl,
                                    Description = subCatalogData.Description ?? string.Empty,
                                    IsDirectory = subCatalogData.IsDirectory ?? false,
                                    CategoryPath = subCatalogData.CategoryPath ?? string.Empty,
                                    CreatedDate = subCatalogData.CreatedDate ?? DateTime.Now,
                                    ModifiedDate = subCatalogData.ModifiedDate ?? DateTime.Now,
                                    FolderType = FolderLinkType.LinkOnly,
                                    FileFilters = string.Empty,
                                    IsCatalogEntry = true,
                                    FileSize = subCatalogData.FileSize
                                };
                                
                                var subCatalogEntryNode = new TreeViewNode { Content = subCatalogEntry };
                                catalogEntryNode.Children.Add(subCatalogEntryNode);
                            }
                        }
                        
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
    /// Creates a catalog of all files and subdirectories in a directory recursively.
    /// Returns LinkItems with full paths for TreeView usage, with ALL subdirectory contents pre-loaded recursively.
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
            // First, add all subdirectories as folder entries WITH their contents recursively
            var directories = Directory.GetDirectories(directoryPath);
            foreach (var subDirPath in directories)
            {
                var dirInfo = new DirectoryInfo(subDirPath);
                
                var subDirEntry = new LinkItem
                {
                    Title = dirInfo.Name,
                    Url = subDirPath,
                    Description = $"Subfolder",
                    IsDirectory = true,
                    CategoryPath = categoryPath,
                    CreatedDate = dirInfo.CreationTime,
                    ModifiedDate = dirInfo.LastWriteTime,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true
                };
                
                catalogEntries.Add(subDirEntry);
            }

            // Then, add all files in the current directory
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
    /// Recursively catalogs a subdirectory and returns its entries with children populated.
    /// Used when expanding a catalog subdirectory for the first time.
    /// </summary>
    public async Task<List<LinkItem>> CreateSubdirectoryCatalogEntriesAsync(String directoryPath, string categoryPath)
    {
        var catalogEntries = new List<LinkItem>();

        if (!Directory.Exists(directoryPath))
        {
            return catalogEntries;
        }

        try
        {
            // Add all files in this subdirectory
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

            // Add subdirectories (without recursing into them)
            var directories = Directory.GetDirectories(directoryPath);
            foreach (var subDirPath in directories)
            {
                var dirInfo = new DirectoryInfo(subDirPath);
                
                catalogEntries.Add(new LinkItem
                {
                    Title = dirInfo.Name,
                    Url = subDirPath,
                    Description = $"Subfolder",
                    IsDirectory = true,
                    CategoryPath = categoryPath,
                    CreatedDate = dirInfo.CreationTime,
                    ModifiedDate = dirInfo.LastWriteTime,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error creating catalog for subdirectory {directoryPath}: {ex.Message}", ex);
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
    /// Counts only direct file children (not subdirectories).
    /// </summary>
    public void UpdateCatalogFileCount(TreeViewNode linkNode)
    {
        if (linkNode.Content is LinkItem link && link.IsDirectory)
        {
            // Count only files that are direct children, exclude subdirectory entries
            var fileCount = linkNode.Children.Count(child => 
                child.Content is LinkItem catalogEntry && 
                catalogEntry.IsCatalogEntry && 
                !catalogEntry.IsDirectory);
            
            link.CatalogFileCount = fileCount;
        }
    }
}