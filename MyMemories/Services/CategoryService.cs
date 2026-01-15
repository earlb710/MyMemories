using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services.Interfaces;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for managing category data persistence and operations with password protection support.
/// </summary>
public class CategoryService : ICategoryService
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
                    
                    // Log the category load if audit logging is enabled for this category
                    await LogCategoryLoadedAsync(categoryNode);
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
                        
                        // Log the category load if audit logging is enabled for this category
                        await LogCategoryLoadedAsync(categoryNode);
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
    /// Logs when a category is loaded at startup if audit logging is enabled.
    /// </summary>
    private async Task LogCategoryLoadedAsync(TreeViewNode categoryNode)
    {
        if (_configService == null || !_configService.IsLoggingEnabled())
            return;

        if (categoryNode.Content is not CategoryItem category)
            return;

        // Only log if audit logging is enabled for this category
        if (!category.IsAuditLoggingEnabled)
            return;

        var linkCount = CountLinksRecursive(categoryNode);
        var subcategoryCount = CountSubcategoriesRecursive(categoryNode);
        
        // Use AuditLogService directly with INFO type instead of CHANGE
        var auditLogService = _configService.AuditLogService;
        if (auditLogService != null)
        {
            await auditLogService.LogAsync(
                category.Name,
                AuditLogType.Info,
                "Category loaded at startup",
                $"Links: {linkCount}, Subcategories: {subcategoryCount}");
        }
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

        string savedFilePath;

        if (shouldEncrypt)
        {
            await SaveEncryptedCategoryAsync(fileName, json, category);
            savedFilePath = Path.Combine(_dataFolder, fileName + ".zip.json");
        }
        else
        {
            // Save as regular JSON
            savedFilePath = Path.Combine(_dataFolder, fileName + ".json");
            await File.WriteAllTextAsync(savedFilePath, json);

            // Delete encrypted version if it exists
            var encryptedPath = Path.Combine(_dataFolder, fileName + ".zip.json");
            if (File.Exists(encryptedPath))
            {
                File.Delete(encryptedPath);
            }
        }

        // Backup to configured directories if any
        if (category.HasBackupDirectories)
        {
            await BackupCategoryFileAsync(savedFilePath, category.BackupDirectories);
        }

        // Log the save operation if logging is enabled (always log when log directory is set)
        if (_configService != null && _configService.IsLoggingEnabled())
        {
            var linkCount = CountLinksRecursive(categoryNode);
            var subcategoryCount = CountSubcategoriesRecursive(categoryNode);
            await _configService.LogCategoryChangeAsync(
                category.Name, 
                "Category saved", 
                $"Links: {linkCount}, Subcategories: {subcategoryCount}",
                category.IsAuditLoggingEnabled);
        }
    }

    /// <summary>
    /// Backs up the category file to configured automatic backup directories only.
    /// Directories prefixed with [MANUAL] are skipped during automatic backup.
    /// </summary>
    private async Task BackupCategoryFileAsync(string sourceFilePath, List<string> backupDirectories)
    {
        // Filter to only automatic directories (those without [MANUAL] prefix)
        var autoDirectories = BackupService.GetAutomaticDirectories(backupDirectories).ToList();
        
        if (autoDirectories.Count == 0 || !File.Exists(sourceFilePath))
            return;

        try
        {
            var summary = await BackupService.Instance.BackupFileAsync(sourceFilePath, autoDirectories);

            if (summary.HasFailures)
            {
                foreach (var failure in summary.Results.Where(r => !r.Success))
                {
                    LogUtilities.LogError("CategoryService.BackupCategoryFileAsync",
                        $"Backup failed to {failure.DestinationPath}: {failure.ErrorMessage}", null);
                }
            }

            if (summary.SuccessCount > 0)
            {
                LogUtilities.LogInfo("CategoryService.BackupCategoryFileAsync",
                    $"Successfully backed up to {summary.SuccessCount} automatic location(s)");
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("CategoryService.BackupCategoryFileAsync",
                "Error during backup operation", ex);
        }
    }

    /// <summary>
    /// Counts links recursively in a category node.
    /// </summary>
    private int CountLinksRecursive(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && !link.IsCatalogEntry)
            {
                count++;
            }
            else if (child.Content is CategoryItem)
            {
                count += CountLinksRecursive(child);
            }
        }
        return count;
    }

    /// <summary>
    /// Counts subcategories recursively in a category node.
    /// </summary>
    private int CountSubcategoriesRecursive(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem)
            {
                count++;
                count += CountSubcategoriesRecursive(child);
            }
        }
        return count;
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
            Icon = category.Icon == "??" ? null : category.Icon,
            Keywords = string.IsNullOrWhiteSpace(category.Keywords) ? null : category.Keywords,
            TagIds = category.TagIds.Count > 0 ? category.TagIds : null,
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
            IsBookmarkLookup = category.IsBookmarkLookup,
            IsAuditLoggingEnabled = category.IsAuditLoggingEnabled,
            BackupDirectories = category.BackupDirectories.Count > 0 ? category.BackupDirectories : null,
            Links = null,
            SubCategories = null
        };

        var links = new List<LinkData>();
        var subCategories = new List<CategoryData>();

        // Process all children - validate before processing
        foreach (var child in categoryNode.Children)
        {
            // Skip nodes with null or invalid content (data validation)
            if (child.Content == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ConvertNodeToCategoryData] Skipping child with null content");
                continue;
            }
            
            if (child.Content is LinkItem link)
            {
                // Skip catalog entries that are direct children of categories
                if (link.IsCatalogEntry)
                {
                    continue;
                }
                
                // Validate LinkItem has required data
                if (string.IsNullOrWhiteSpace(link.Title))
                {
                    System.Diagnostics.Debug.WriteLine($"[ConvertNodeToCategoryData] Skipping link with empty title");
                    continue;
                }

                var linkData = new LinkData
                {
                    Title = link.Title,
                    Url = link.Url,
                    Description = string.IsNullOrWhiteSpace(link.Description) ? null : link.Description,
                    Keywords = string.IsNullOrWhiteSpace(link.Keywords) ? null : link.Keywords,
                    TagIds = link.TagIds.Count > 0 ? link.TagIds : null,
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
                    BackupDirectories = link.BackupDirectories.Count > 0 ? link.BackupDirectories : null,
                    CatalogSortOrder = link.CatalogSortOrder,
                    UrlStatus = link.UrlStatus,
                    UrlLastChecked = link.UrlLastChecked,
                    UrlStatusMessage = string.IsNullOrWhiteSpace(link.UrlStatusMessage) ? null : link.UrlStatusMessage,
                    CatalogEntries = null
                };

                // Process catalog entries (only for directory links)
                if (child.Children.Count > 0 && !link.IsCatalogEntry && link.IsDirectory)
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
                                // Save subdirectory file count and size for display
                                CatalogFileCount = catalogEntry.IsDirectory && catalogEntry.CatalogFileCount > 0 ? catalogEntry.CatalogFileCount : null,
                                CatalogTotalSize = catalogEntry.IsDirectory && catalogEntry.CatalogTotalSize > 0 ? catalogEntry.CatalogTotalSize : null,
                                // Save tags and ratings for catalog entries
                                TagIds = catalogEntry.TagIds.Count > 0 ? catalogEntry.TagIds : null,
                                Ratings = catalogEntry.Ratings.Count > 0 ? catalogEntry.Ratings : null,
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
                                            Title = subCatalogEntry.Title ?? string.Empty,
                                            Url = subRelativeUrl,
                                            Description = string.IsNullOrWhiteSpace(subCatalogEntry.Description) ? null : subCatalogEntry.Description,
                                            IsDirectory = subCatalogEntry.IsDirectory ? true : null,
                                            CategoryPath = subCatalogEntry.CategoryPath,
                                            CreatedDate = subCatalogEntry.CreatedDate,
                                            ModifiedDate = subCatalogEntry.ModifiedDate,
                                            FileSize = subCatalogEntry.FileSize,
                                            // Save subdirectory file count and size for display
                                            CatalogFileCount = subCatalogEntry.IsDirectory && subCatalogEntry.CatalogFileCount > 0 ? subCatalogEntry.CatalogFileCount : null,
                                            CatalogTotalSize = subCatalogEntry.IsDirectory && subCatalogEntry.CatalogTotalSize > 0 ? subCatalogEntry.CatalogTotalSize : null,
                                            // Save tags and ratings for sub-catalog entries
                                            TagIds = subCatalogEntry.TagIds.Count > 0 ? subCatalogEntry.TagIds : null,
                                            Ratings = subCatalogEntry.Ratings.Count > 0 ? subCatalogEntry.Ratings : null,
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
                // Process sub-links for non-directory links (URL links with children)
                else if (child.Children.Count > 0 && !link.IsCatalogEntry && !link.IsDirectory)
                {
                    var subLinks = new List<LinkData>();

                    foreach (var subLinkChild in child.Children)
                    {
                        if (subLinkChild.Content is LinkItem subLink && !subLink.IsCatalogEntry)
                        {
                            var subLinkData = new LinkData
                            {
                                Title = subLink.Title,
                                Url = subLink.Url,
                                Description = string.IsNullOrWhiteSpace(subLink.Description) ? null : subLink.Description,
                                Keywords = string.IsNullOrWhiteSpace(subLink.Keywords) ? null : subLink.Keywords,
                                TagIds = subLink.TagIds.Count > 0 ? subLink.TagIds : null,
                                IsDirectory = subLink.IsDirectory ? true : null,
                                CategoryPath = subLink.CategoryPath,
                                CreatedDate = subLink.CreatedDate,
                                ModifiedDate = subLink.ModifiedDate,
                                FolderType = subLink.IsDirectory && subLink.FolderType != FolderLinkType.LinkOnly ? subLink.FolderType : null,
                                FileFilters = !string.IsNullOrWhiteSpace(subLink.FileFilters) ? subLink.FileFilters : null,
                                UrlStatus = subLink.UrlStatus,
                                UrlLastChecked = subLink.UrlLastChecked,
                                UrlStatusMessage = string.IsNullOrWhiteSpace(subLink.UrlStatusMessage) ? null : subLink.UrlStatusMessage
                            };

                            subLinks.Add(subLinkData);
                        }
                    }

                    if (subLinks.Count > 0)
                    {
                        linkData.SubLinks = subLinks;
                    }
                }

                links.Add(linkData);
            }
            else if (child.Content is CategoryItem subCategory)
            {
                // Validate subcategory has required data before saving
                if (string.IsNullOrWhiteSpace(subCategory.Name))
                {
                    System.Diagnostics.Debug.WriteLine($"[ConvertNodeToCategoryData] Skipping subcategory with empty name");
                    continue;
                }
                
                var subCategoryData = ConvertNodeToCategoryData(child);
                subCategories.Add(subCategoryData);
            }
            else
            {
                // Log unexpected content type
                var contentType = child.Content?.GetType().Name ?? "null";
                System.Diagnostics.Debug.WriteLine($"[ConvertNodeToCategoryData] Skipping child with unexpected content type: {contentType}");
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
        var categoryItem = new CategoryItem
        {
            Name = categoryData.Name,
            Description = categoryData.Description ?? string.Empty,
            Icon = categoryData.Icon ?? "??",
            Keywords = categoryData.Keywords ?? string.Empty,
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
            IsBookmarkCategory = categoryData.IsBookmarkCategory,
            IsBookmarkLookup = categoryData.IsBookmarkLookup,
            IsAuditLoggingEnabled = categoryData.IsAuditLoggingEnabled
        };
        
        // Copy TagIds if present
        if (categoryData.TagIds != null && categoryData.TagIds.Count > 0)
        {
            categoryItem.TagIds = new List<string>(categoryData.TagIds);
        }
        
        // Copy BackupDirectories if present
        if (categoryData.BackupDirectories != null && categoryData.BackupDirectories.Count > 0)
        {
            categoryItem.BackupDirectories = new List<string>(categoryData.BackupDirectories);
        }

        var categoryNode = new TreeViewNode { Content = categoryItem };

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
                    Keywords = linkData.Keywords ?? string.Empty,
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
                    CatalogSortOrder = linkData.CatalogSortOrder,
                    UrlStatus = linkData.UrlStatus,
                    UrlLastChecked = linkData.UrlLastChecked,
                    UrlStatusMessage = linkData.UrlStatusMessage ?? string.Empty
                };
                
                // Copy TagIds if present
                if (linkData.TagIds != null && linkData.TagIds.Count > 0)
                {
                    linkItem.TagIds = new List<string>(linkData.TagIds);
                }

                // Copy BackupDirectories if present
                if (linkData.BackupDirectories != null && linkData.BackupDirectories.Count > 0)
                {
                    linkItem.BackupDirectories = new List<string>(linkData.BackupDirectories);
                }

                if (linkData.CatalogEntries != null)
                {
                    // Count only files (not subdirectories) for the file count
                    // Also calculate total size
                    int fileCount = 0;
                    ulong totalSize = 0;
                    
                    foreach (var entry in linkData.CatalogEntries)
                    {
                        if (entry.IsDirectory != true)
                        {
                            fileCount++;
                            if (entry.FileSize.HasValue)
                            {
                                totalSize += entry.FileSize.Value;
                            }
                        }
                    }
                    
                    linkItem.CatalogFileCount = fileCount;
                    linkItem.CatalogTotalSize = totalSize;
                }

                var linkNode = new TreeViewNode { Content = linkItem };

                // Add catalog entries as children
                if (linkData.CatalogEntries != null)
                {
                    // Get reference time from parent link's LastCatalogUpdate
                    var referenceTime = linkItem.LastCatalogUpdate;
                    
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
                            FileSize = catalogData.FileSize,
                            // Restore subdirectory file count and size for display
                            CatalogFileCount = catalogData.CatalogFileCount ?? 0,
                            CatalogTotalSize = catalogData.CatalogTotalSize ?? 0
                        };

                        // Restore TagIds if present
                        if (catalogData.TagIds != null && catalogData.TagIds.Count > 0)
                        {
                            catalogEntry.TagIds = new List<string>(catalogData.TagIds);
                        }

                        // Restore Ratings if present
                        if (catalogData.Ratings != null && catalogData.Ratings.Count > 0)
                        {
                            catalogEntry.Ratings = new List<RatingValue>(catalogData.Ratings);
                            // Clean up corrupt ratings (those without names)
                            CleanupCorruptRatings(catalogEntry.Ratings);
                        }

                        // Check if this directory has changed since the catalog was last updated
                        if (catalogEntry.IsDirectory && referenceTime.HasValue)
                        {
                            CheckAndMarkCatalogEntryAsChanged(catalogEntry, referenceTime.Value);
                        }

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
                                    FileSize = subCatalogData.FileSize,
                                    // Restore subdirectory file count and size for display
                                    CatalogFileCount = subCatalogData.CatalogFileCount ?? 0,
                                    CatalogTotalSize = subCatalogData.CatalogTotalSize ?? 0
                                };

                                // Restore TagIds if present
                                if (subCatalogData.TagIds != null && subCatalogData.TagIds.Count > 0)
                                {
                                    subCatalogEntry.TagIds = new List<string>(subCatalogData.TagIds);
                                }

                                // Restore Ratings if present
                                if (subCatalogData.Ratings != null && subCatalogData.Ratings.Count > 0)
                                {
                                    subCatalogEntry.Ratings = new List<RatingValue>(subCatalogData.Ratings);
                                }

                                // Check if this subdirectory has changed since the catalog was last updated
                                if (subCatalogEntry.IsDirectory && referenceTime.HasValue)
                                {
                                    CheckAndMarkCatalogEntryAsChanged(subCatalogEntry, referenceTime.Value);
                                }

                                var subCatalogEntryNode = new TreeViewNode { Content = subCatalogEntry };
                                catalogEntryNode.Children.Add(subCatalogEntryNode);
                            }
                        }

                        linkNode.Children.Add(catalogEntryNode);
                    }
                }

                // Add sub-links as children (for URL links with child links)
                if (linkData.SubLinks != null && linkData.SubLinks.Count > 0)
                {
                    foreach (var subLinkData in linkData.SubLinks)
                    {
                        var subLinkItem = new LinkItem
                        {
                            Title = subLinkData.Title ?? string.Empty,
                            Url = subLinkData.Url ?? string.Empty,
                            Description = subLinkData.Description ?? string.Empty,
                            Keywords = subLinkData.Keywords ?? string.Empty,
                            IsDirectory = subLinkData.IsDirectory ?? false,
                            CategoryPath = subLinkData.CategoryPath ?? string.Empty,
                            CreatedDate = subLinkData.CreatedDate ?? DateTime.Now,
                            ModifiedDate = subLinkData.ModifiedDate ?? DateTime.Now,
                            FolderType = subLinkData.FolderType ?? FolderLinkType.LinkOnly,
                            FileFilters = subLinkData.FileFilters ?? string.Empty,
                            IsCatalogEntry = false,
                            UrlStatus = subLinkData.UrlStatus,
                            UrlLastChecked = subLinkData.UrlLastChecked,
                            UrlStatusMessage = subLinkData.UrlStatusMessage ?? string.Empty
                        };

                        // Copy TagIds if present
                        if (subLinkData.TagIds != null && subLinkData.TagIds.Count > 0)
                        {
                            subLinkItem.TagIds = new List<string>(subLinkData.TagIds);
                        }

                        var subLinkNode = new TreeViewNode { Content = subLinkItem };
                        linkNode.Children.Add(subLinkNode);
                    }
                }

                categoryNode.Children.Add(linkNode);
            }
        }

        return categoryNode;
    }

    /// <summary>
    /// Checks if a catalog entry directory has been modified since the reference time and marks it as changed.
    /// Used when loading catalog entries from saved JSON.
    /// </summary>
    private static void CheckAndMarkCatalogEntryAsChanged(LinkItem catalogEntry, DateTime referenceTime)
    {
        if (!catalogEntry.IsDirectory || !catalogEntry.IsCatalogEntry)
            return;

        if (string.IsNullOrEmpty(catalogEntry.Url))
            return;

        try
        {
            if (!Directory.Exists(catalogEntry.Url))
                return;

            var dirInfo = new DirectoryInfo(catalogEntry.Url);

            // Compare the directory's current LastWriteTime to the catalog entry's stored ModifiedDate
            // This detects if the directory has changed since we last cataloged it
            // The stored ModifiedDate was set to dirInfo.LastWriteTime when the catalog was created
            // If current LastWriteTime > stored ModifiedDate, the directory has changed
            
            // Use a 2 second tolerance to avoid false positives from timestamp precision
            var storedModifiedDate = catalogEntry.ModifiedDate;
            var currentLastWriteTime = dirInfo.LastWriteTime;
            
            // Only mark as changed if the current timestamp is significantly newer than what we stored
            if (currentLastWriteTime > storedModifiedDate.AddSeconds(2))
            {
                catalogEntry.CatalogEntryHasChanged = true;
                System.Diagnostics.Debug.WriteLine($"[CheckAndMarkCatalogEntryAsChanged] Directory '{catalogEntry.Title}' marked as changed (Current: {currentLastWriteTime}, Stored: {storedModifiedDate})");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CheckAndMarkCatalogEntryAsChanged] Error checking '{catalogEntry.Title}': {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        return FileUtilities.SanitizeFileName(fileName);
    }

    // Directories to skip during cataloging (common large/irrelevant folders)
    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        ".git",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "packages",
        ".nuget",
        "__pycache__",
        ".cache",
        "bower_components",
        "vendor"
    };

    /// <summary>
    /// Checks if a directory should be skipped during cataloging.
    /// </summary>
    private static bool ShouldSkipDirectory(string directoryName)
    {
        return SkipDirectories.Contains(directoryName);
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
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(directoryPath);
            }
            catch (PathTooLongException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] PathTooLong: Skipping directories in {directoryPath}");
                directories = Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Unauthorized: Skipping directories in {directoryPath}");
                directories = Array.Empty<string>();
            }

            foreach (var subDirPath in directories)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(subDirPath);
                    
                    // Skip problematic directories
                    if (ShouldSkipDirectory(dirInfo.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Skipping directory: {dirInfo.Name}");
                        continue;
                    }

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
                catch (PathTooLongException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] PathTooLong: Skipping {subDirPath}");
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Unauthorized: Skipping {subDirPath}");
                }
            }

            // Then, add all files in the current directory
            string[] files;
            try
            {
                files = Directory.GetFiles(directoryPath);
            }
            catch (PathTooLongException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] PathTooLong: Skipping files in {directoryPath}");
                files = Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Unauthorized: Skipping files in {directoryPath}");
                files = Array.Empty<string>();
            }

            foreach (var filePath in files)
            {
                try
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
                catch (PathTooLongException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] PathTooLong: Skipping file {filePath}");
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Unauthorized: Skipping file {filePath}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateCatalogEntriesAsync] Error: {ex.Message}");
            // Return what we have so far instead of throwing
        }

        return catalogEntries;
    }

    /// <summary>
    /// Recursively catalogs a subdirectory and returns its entries with children populated.
    /// Used when expanding a catalog subdirectory for the first time.
    /// </summary>
    public async Task<List<LinkItem>> CreateSubdirectoryCatalogEntriesAsync(string directoryPath, string categoryPath)
    {
        var catalogEntries = new List<LinkItem>();

        if (!Directory.Exists(directoryPath))
        {
            return catalogEntries;
        }

        try
        {
            // Add all files in this subdirectory
            string[] files;
            try
            {
                files = Directory.GetFiles(directoryPath);
            }
            catch (PathTooLongException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] PathTooLong: Skipping files in {directoryPath}");
                files = Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Unauthorized: Skipping files in {directoryPath}");
                files = Array.Empty<string>();
            }

            foreach (var filePath in files)
            {
                try
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
                catch (PathTooLongException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] PathTooLong: Skipping file {filePath}");
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Unauthorized: Skipping file {filePath}");
                }
            }

            // Add subdirectories (without recursing into them)
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(directoryPath);
            }
            catch (PathTooLongException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] PathTooLong: Skipping directories in {directoryPath}");
                directories = Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Unauthorized: Skipping directories in {directoryPath}");
                directories = Array.Empty<string>();
            }

            foreach (var subDirPath in directories)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(subDirPath);

                    // Skip problematic directories
                    if (ShouldSkipDirectory(dirInfo.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Skipping directory: {dirInfo.Name}");
                        continue;
                    }

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
                catch (PathTooLongException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] PathTooLong: Skipping {subDirPath}");
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Unauthorized: Skipping {subDirPath}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateSubdirectoryCatalogEntriesAsync] Error: {ex.Message}");
            // Return what we have so far instead of throwing
        }

        return catalogEntries;
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
    /// Updates the catalog file count and total size for a link item based on its children.
    /// Counts only direct file children (not subdirectories).
    /// </summary>
    public void UpdateCatalogFileCount(TreeViewNode linkNode)
    {
        if (linkNode.Content is LinkItem link && link.IsDirectory)
        {
            // Count only files that are direct children, exclude subdirectory entries
            int fileCount = 0;
            ulong totalSize = 0;
            
            foreach (var child in linkNode.Children)
            {
                if (child.Content is LinkItem catalogEntry && 
                    catalogEntry.IsCatalogEntry && 
                    !catalogEntry.IsDirectory)
                {
                    fileCount++;
                    if (catalogEntry.FileSize.HasValue)
                    {
                        totalSize += catalogEntry.FileSize.Value;
                    }
                }
            }

            link.CatalogFileCount = fileCount;
            link.CatalogTotalSize = totalSize;
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

    /// <summary>
    /// Gets the cached password for a specific category if available.
    /// </summary>
    public string? GetCachedCategoryPassword(string categoryName)
    {
        if (_passwordCache.TryGetValue(categoryName, out var categoryPassword))
        {
            return categoryPassword;
        }
        return null;
    }

    /// <summary>
    /// Removes corrupt ratings (those without valid names) from a ratings list.
    /// A rating is considered corrupt if its Rating property is null, empty, or whitespace.
    /// </summary>
    private static void CleanupCorruptRatings(List<RatingValue> ratings)
    {
        if (ratings == null || ratings.Count == 0)
            return;

        // Remove ratings without names
        var corruptRatings = ratings.Where(r => string.IsNullOrWhiteSpace(r.Rating)).ToList();
        
        if (corruptRatings.Count > 0)
        {
            foreach (var corrupt in corruptRatings)
            {
                ratings.Remove(corrupt);
            }
            
            System.Diagnostics.Debug.WriteLine($"[CleanupCorruptRatings] Removed {corruptRatings.Count} corrupt rating(s) without names");
        }
    }

    /// <summary>
    /// Gets the file path for a category by name.
    /// Returns the path to the .json or .zip.json file, whichever exists.
    /// Returns null if neither file exists.
    /// </summary>
    public string? GetCategoryFilePath(string categoryName)
    {
        var fileName = SanitizeFileName(categoryName);
        
        var jsonPath = Path.Combine(_dataFolder, fileName + ".json");
        if (File.Exists(jsonPath))
        {
            return jsonPath;
        }
        
        var encryptedPath = Path.Combine(_dataFolder, fileName + ".zip.json");
        if (File.Exists(encryptedPath))
        {
            return encryptedPath;
        }
        
        return null;
    }

    /// <summary>
    /// Gets the data folder path used by this CategoryService.
    /// </summary>
    public string DataFolder => _dataFolder;
}