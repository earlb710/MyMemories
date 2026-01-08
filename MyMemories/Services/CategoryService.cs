using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for managing category data persistence and operations with password protection support.
/// </summary>
public class CategoryService
{
    private readonly string _dataFolder;
    private readonly JsonSerializerOptions _jsonOptions;
    private ConfigurationService? _configService;

    // Cache for storing actual passwords (not hashes) for encryption/decryption
    private readonly Dictionary<string, string> _passwordCache = new();

    public CategoryService(string dataFolder, ConfigurationService? configService = null)
    {
        _dataFolder = dataFolder;
        _configService = configService;
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
    /// Sets or updates the configuration service for password management.
    /// </summary>
    public void SetConfigurationService(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Caches the global password for encryption/decryption operations.
    /// Must be called after user authenticates or sets a global password.
    /// </summary>
    public void CacheGlobalPassword(string password)
    {
        _passwordCache["__GLOBAL__"] = password;
    }

    /// <summary>
    /// Caches a category's own password for encryption/decryption operations.
    /// Must be called when user sets or enters a category password.
    /// </summary>
    public void CacheCategoryPassword(string categoryName, string password)
    {
        _passwordCache[categoryName] = password;
    }

    /// <summary>
    /// Clears all cached passwords (call on app exit or logout).
    /// </summary>
    public void ClearPasswordCache()
    {
        _passwordCache.Clear();
    }

    /// <summary>
    /// Loads all categories from JSON files (both encrypted and unencrypted).
    /// </summary>
    public async Task<List<TreeViewNode>> LoadAllCategoriesAsync()
    {
        var categories = new List<TreeViewNode>();

        if (!Directory.Exists(_dataFolder))
        {
            return categories;
        }

        // Get all .json and .zip.json files
        var jsonFiles = Directory.GetFiles(_dataFolder, "*.json")
            .Where(f => !f.EndsWith(".zip.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var encryptedFiles = Directory.GetFiles(_dataFolder, "*.zip.json");

        // Process unencrypted files
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
                // Log but continue with other categories
                LogUtilities.LogError("CategoryService.LoadAllCategoriesAsync", $"Error loading {Path.GetFileName(jsonFile)}", ex);
            }
        }

        // Process encrypted files
        foreach (var encryptedFile in encryptedFiles)
        {
            try
            {
                var json = await LoadEncryptedCategoryAsync(encryptedFile);
                if (json != null)
                {
                    var categoryData = JsonSerializer.Deserialize<CategoryData>(json, _jsonOptions);

                    if (categoryData != null)
                    {
                        var categoryNode = CreateCategoryNode(categoryData);
                        categories.Add(categoryNode);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but continue with other categories
                LogUtilities.LogError("CategoryService.LoadAllCategoriesAsync", $"Error loading encrypted {Path.GetFileName(encryptedFile)}", ex);
                // Don't throw - just skip this encrypted category if password is wrong
            }
        }

        return categories;
    }

    /// <summary>
    /// Loads and decrypts an encrypted category file.
    /// </summary>
    private async Task<string?> LoadEncryptedCategoryAsync(string encryptedFilePath)
    {
        // Extract category name from filename (remove .zip.json)
        var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(encryptedFilePath));

        // Get the password for this category
        var password = GetCategoryPassword(fileName);
        if (password == null)
        {
            return null; // Return null instead of throwing - will be handled gracefully
        }

        try
        {
            // Use ZipFile.OpenRead instead of ZipInputStream for better AES support
            using var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(encryptedFilePath);
            zipFile.Password = password; // Set password for the archive

            if (zipFile.Count == 0)
            {
                throw new InvalidOperationException($"No entries found in encrypted file: {encryptedFilePath}");
            }

            // Get the first entry
            var entry = zipFile[0];

            // Read the JSON content
            using var entryStream = zipFile.GetInputStream(entry);
            using var memoryStream = new MemoryStream();
            await entryStream.CopyToAsync(memoryStream);

            var jsonBytes = memoryStream.ToArray();
            var json = Encoding.UTF8.GetString(jsonBytes);

            return json;
        }
        catch (ICSharpCode.SharpZipLib.Zip.ZipException ex)
        {
            throw new InvalidOperationException($"Cannot decrypt {fileName}: {ex.Message}. Please verify the password is correct.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot decrypt {fileName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the password for a category (either own password or global password).
    /// </summary>
    private string? GetCategoryPassword(string categoryName)
    {
        // First try category-specific password
        if (_passwordCache.TryGetValue(categoryName, out var categoryPassword))
        {
            return categoryPassword;
        }

        // Then try global password
        if (_passwordCache.TryGetValue("__GLOBAL__", out var globalPassword))
        {
            return globalPassword;
        }

        return null;
    }

    /// <summary>
    /// Saves a category to a JSON file (encrypted if password is set).
    /// </summary>
    public async Task SaveCategoryAsync(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem)
        {
            throw new ArgumentException("Node must contain a CategoryItem", nameof(categoryNode));
        }

        var category = (CategoryItem)categoryNode.Content;
        var categoryData = ConvertNodeToCategoryData(categoryNode);
        var json = JsonSerializer.Serialize(categoryData, _jsonOptions);

        var fileName = SanitizeFileName(category.Name);
        var shouldEncrypt = ShouldEncryptCategory(category);

        if (shouldEncrypt)
        {
            await SaveEncryptedCategoryAsync(fileName, json, category);
        }
        else
        {
            // Save as regular JSON
            var filePath = Path.Combine(_dataFolder, fileName + ".json");
            await File.WriteAllTextAsync(filePath, json);

            // Delete encrypted version if it exists
            var encryptedPath = Path.Combine(_dataFolder, fileName + ".zip.json");
            if (File.Exists(encryptedPath))
            {
                File.Delete(encryptedPath);
            }
        }
    }

    /// <summary>
    /// Determines if a category should be encrypted based on its password protection settings.
    /// </summary>
    private bool ShouldEncryptCategory(CategoryItem category)
    {
        return category.PasswordProtection == PasswordProtectionType.GlobalPassword ||
               category.PasswordProtection == PasswordProtectionType.OwnPassword;
    }

    /// <summary>
    /// Saves a category as an encrypted .zip.json file.
    /// </summary>
    private async Task SaveEncryptedCategoryAsync(string fileName, string json, CategoryItem category)
    {
        var password = GetPasswordForSaving(category);
        if (password == null)
        {
            throw new InvalidOperationException($"Cannot encrypt category '{category.Name}': No password available.");
        }

        var encryptedFilePath = Path.Combine(_dataFolder, fileName + ".zip.json");
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Create encrypted zip using SharpZipLib
        using var fileStream = new FileStream(encryptedFilePath, FileMode.Create);
        using var zipOutputStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fileStream);

        zipOutputStream.SetLevel(6); // Compression level
        zipOutputStream.Password = password;

        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(fileName + ".json")
        {
            DateTime = DateTime.Now,
            Size = jsonBytes.Length
        };

        // Enable AES-256 encryption
        entry.AESKeySize = 256;

        zipOutputStream.PutNextEntry(entry);
        await zipOutputStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        zipOutputStream.CloseEntry();
        zipOutputStream.Finish();

        // Delete unencrypted version if it exists
        var unencryptedPath = Path.Combine(_dataFolder, fileName + ".json");
        if (File.Exists(unencryptedPath))
        {
            File.Delete(unencryptedPath);
        }
    }

    /// <summary>
    /// Gets the password to use for saving a category.
    /// </summary>
    private string? GetPasswordForSaving(CategoryItem category)
    {
        if (category.PasswordProtection == PasswordProtectionType.OwnPassword)
        {
            // Try to get the cached password first
            if (_passwordCache.TryGetValue(category.Name, out var ownPassword))
            {
                return ownPassword;
            }

            // If not in cache, we can't encrypt - this should never happen if passwords are cached properly
            return null;
        }
        else if (category.PasswordProtection == PasswordProtectionType.GlobalPassword)
        {
            // Use cached global password
            if (_passwordCache.TryGetValue("__GLOBAL__", out var globalPassword))
            {
                return globalPassword;
            }

            return null;
        }

        return null;
    }

    /// <summary>
    /// Deletes a category file (both encrypted and unencrypted versions).
    /// </summary>
    public Task DeleteCategoryAsync(string categoryName)
    {
        var fileName = SanitizeFileName(categoryName);

        var jsonPath = Path.Combine(_dataFolder, fileName + ".json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }

        var encryptedPath = Path.Combine(_dataFolder, fileName + ".zip.json");
        if (File.Exists(encryptedPath))
        {
            File.Delete(encryptedPath);
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
            var contentType = categoryNode.Content?.GetType().Name ?? "null";
            throw new ArgumentException($"Node must contain a CategoryItem, but contains: {contentType}");
        }

        var categoryData = new CategoryData
        {
            Name = category.Name,
            Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description,
            Icon = category.Icon == "📁" ? null : category.Icon,
            CreatedDate = category.CreatedDate,
            ModifiedDate = category.ModifiedDate,
            PasswordProtection = category.PasswordProtection,
            OwnPasswordHash = category.OwnPasswordHash,
            SortOrder = category.SortOrder,
            IsBookmarkImport = category.IsBookmarkImport,
            SourceBrowserType = category.SourceBrowserType,
            SourceBrowserName = category.SourceBrowserName,
            SourceBookmarksPath = category.SourceBookmarksPath,
            LastBookmarkImportDate = category.LastBookmarkImportDate,
            ImportedBookmarkCount = category.ImportedBookmarkCount,
            IsBookmarkCategory = category.IsBookmarkCategory,
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
                // Skip catalog entries that are direct children of categories
                if (link.IsCatalogEntry)
                {
                    continue;
                }

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
                    IsZipPasswordProtected = link.IsZipPasswordProtected ? true : null,
                    CatalogSortOrder = link.CatalogSortOrder,
                    CatalogEntries = null
                };

                // Debug: Log zip file password protection status
                if ((linkData.IsDirectory ?? false) && (linkData.Url ?? string.Empty).EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[CategoryService] Loaded zip link: '{linkData.Title}', IsZipPasswordProtected={linkData.IsZipPasswordProtected}");
                }

                // Process catalog entries (only for non-catalog-entry links)
                if (child.Children.Count > 0 && !link.IsCatalogEntry)
                {
                    var catalogEntries = new List<LinkData>();

                    foreach (var catalogChild in child.Children)
                    {
                        if (catalogChild.Content is LinkItem catalogEntry)
                        {
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
                                }
                            }

                            catalogEntries.Add(catalogData);
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
                ModifiedDate = categoryData.ModifiedDate ?? DateTime.Now,
                PasswordProtection = categoryData.PasswordProtection,
                OwnPasswordHash = categoryData.OwnPasswordHash,
                SortOrder = categoryData.SortOrder,
                IsBookmarkImport = categoryData.IsBookmarkImport,
                SourceBrowserType = categoryData.SourceBrowserType,
                SourceBrowserName = categoryData.SourceBrowserName,
                SourceBookmarksPath = categoryData.SourceBookmarksPath,
                LastBookmarkImportDate = categoryData.LastBookmarkImportDate,
                ImportedBookmarkCount = categoryData.ImportedBookmarkCount,
                IsBookmarkCategory = categoryData.IsBookmarkCategory
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
                    AutoRefreshCatalog = linkData.AutoRefreshCatalog ?? false,
                    IsZipPasswordProtected = linkData.IsZipPasswordProtected ?? false,
                    CatalogSortOrder = linkData.CatalogSortOrder
                };

                // Debug: Log zip file password protection status
                if (linkItem.IsDirectory && linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[CategoryService] Loaded zip link: '{linkItem.Title}', IsZipPasswordProtected={linkItem.IsZipPasswordProtected}");
                }

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

    /// <summary>
    /// Gets the cached global password if available.
    /// </summary>
    public string? GetCachedGlobalPassword()
    {
        if (_passwordCache.TryGetValue("__GLOBAL__", out var globalPassword))
        {
            return globalPassword;
        }
        return null;
    }
}