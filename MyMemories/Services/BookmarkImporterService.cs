using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for importing and syncing browser bookmarks.
/// </summary>
public class BookmarkImporterService
{
    /// <summary>
    /// Detects installed browsers and returns their bookmark file paths.
    /// </summary>
    public List<BrowserInfo> DetectInstalledBrowsers()
    {
        var browsers = new List<BrowserInfo>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Chrome
        var chromePath = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Bookmarks");
        if (File.Exists(chromePath))
        {
            browsers.Add(new BrowserInfo
            {
                Name = "Google Chrome",
                BrowserType = BrowserType.Chrome,
                BookmarksPath = chromePath
            });
        }

        // Edge
        var edgePath = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Bookmarks");
        if (File.Exists(edgePath))
        {
            browsers.Add(new BrowserInfo
            {
                Name = "Microsoft Edge",
                BrowserType = BrowserType.Edge,
                BookmarksPath = edgePath
            });
        }

        // Brave
        var bravePath = Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Bookmarks");
        if (File.Exists(bravePath))
        {
            browsers.Add(new BrowserInfo
            {
                Name = "Brave Browser",
                BrowserType = BrowserType.Brave,
                BookmarksPath = bravePath
            });
        }

        // Vivaldi
        var vivaldiPath = Path.Combine(localAppData, @"Vivaldi\User Data\Default\Bookmarks");
        if (File.Exists(vivaldiPath))
        {
            browsers.Add(new BrowserInfo
            {
                Name = "Vivaldi",
                BrowserType = BrowserType.Vivaldi,
                BookmarksPath = vivaldiPath
            });
        }

        // Opera
        var operaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            @"Opera Software\Opera Stable\Bookmarks");
        if (File.Exists(operaPath))
        {
            browsers.Add(new BrowserInfo
            {
                Name = "Opera",
                BrowserType = BrowserType.Opera,
                BookmarksPath = operaPath
            });
        }

        return browsers;
    }

    /// <summary>
    /// Gets the folder structure from a browser's bookmarks file for selective import.
    /// </summary>
    public async Task<BrowserFolderStructure?> GetBrowserFolderStructureAsync(string bookmarksPath)
    {
        try
        {
            if (IsFileLocked(bookmarksPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(bookmarksPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots == null)
            {
                return null;
            }

            var structure = new BrowserFolderStructure
            {
                SourcePath = bookmarksPath
            };

            // Parse bookmark bar
            if (bookmarkFile.roots.bookmark_bar != null)
            {
                var barFolder = ParseFolderNode(bookmarkFile.roots.bookmark_bar, "Bookmarks Bar", "");
                structure.RootFolders.Add(barFolder);
            }

            // Parse other bookmarks
            if (bookmarkFile.roots.other != null)
            {
                var otherFolder = ParseFolderNode(bookmarkFile.roots.other, "Other Bookmarks", "");
                structure.RootFolders.Add(otherFolder);
            }

            // Parse synced/mobile bookmarks
            if (bookmarkFile.roots.synced != null)
            {
                var syncedFolder = ParseFolderNode(bookmarkFile.roots.synced, "Mobile Bookmarks", "");
                structure.RootFolders.Add(syncedFolder);
            }

            // Extract unique domains
            ExtractDomainsFromFolders(structure.RootFolders, structure.UniqueDomains);

            return structure;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("BookmarkImporterService.GetBrowserFolderStructureAsync", 
                "Failed to parse folder structure", ex);
            return null;
        }
    }

    /// <summary>
    /// Parses a bookmark folder node into a BrowserFolder structure.
    /// </summary>
    private BrowserFolder ParseFolderNode(ChromeBookmarkNode node, string name, string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath} > {name}";
        
        var folder = new BrowserFolder
        {
            Name = name,
            FullPath = currentPath
        };

        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                if (child.type == "url")
                {
                    folder.BookmarkCount++;
                    folder.TotalBookmarkCount++;
                }
                else if (child.type == "folder")
                {
                    var subFolder = ParseFolderNode(child, child.name ?? "Unnamed", currentPath);
                    folder.SubFolders.Add(subFolder);
                    folder.TotalBookmarkCount += subFolder.TotalBookmarkCount;
                }
            }
        }

        return folder;
    }

    /// <summary>
    /// Extracts unique domains from bookmark folders.
    /// </summary>
    private void ExtractDomainsFromFolders(List<BrowserFolder> folders, HashSet<string> domains)
    {
        // We need to re-parse to get URLs - this is a separate pass
    }

    /// <summary>
    /// Gets all unique domains from a browser's bookmarks.
    /// </summary>
    public async Task<List<string>> GetUniqueDomains(string bookmarksPath)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (IsFileLocked(bookmarksPath))
            {
                return domains.ToList();
            }

            var json = await File.ReadAllTextAsync(bookmarksPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots == null)
            {
                return domains.ToList();
            }

            // Collect domains from all root folders
            if (bookmarkFile.roots.bookmark_bar != null)
                CollectDomainsFromNode(bookmarkFile.roots.bookmark_bar, domains);
            if (bookmarkFile.roots.other != null)
                CollectDomainsFromNode(bookmarkFile.roots.other, domains);
            if (bookmarkFile.roots.synced != null)
                CollectDomainsFromNode(bookmarkFile.roots.synced, domains);
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("BookmarkImporterService.GetUniqueDomains", 
                "Failed to extract domains", ex);
        }

        return domains.OrderBy(d => d).ToList();
    }

    /// <summary>
    /// Collects unique domains from a bookmark node recursively.
    /// </summary>
    private void CollectDomainsFromNode(ChromeBookmarkNode node, HashSet<string> domains)
    {
        if (node.children == null)
            return;

        foreach (var child in node.children)
        {
            if (child.type == "url" && !string.IsNullOrEmpty(child.url))
            {
                try
                {
                    var uri = new Uri(child.url);
                    if (uri.Scheme == "http" || uri.Scheme == "https")
                    {
                        // Get domain without www prefix
                        var host = uri.Host;
                        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                            host = host.Substring(4);
                        domains.Add(host);
                    }
                }
                catch
                {
                    // Invalid URL, skip
                }
            }
            else if (child.type == "folder")
            {
                CollectDomainsFromNode(child, domains);
            }
        }
    }

    /// <summary>
    /// Detects changes between browser bookmarks and existing MyMemories links.
    /// Returns a sync result showing new, modified, and deleted bookmarks.
    /// </summary>
    public async Task<BookmarkSyncResult> DetectChangesAsync(
        string bookmarksPath, 
        IEnumerable<LinkItem> existingLinks,
        ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        var result = new BookmarkSyncResult { SourcePath = bookmarksPath };

        try
        {
            // Import current browser bookmarks
            var importResult = await ImportBookmarksAsync(bookmarksPath, options);
            
            if (!importResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = importResult.ErrorMessage;
                return result;
            }

            // Build lookup of existing URLs
            var existingUrls = existingLinks
                .Where(l => !l.IsDirectory && IsWebUrl(l.Url))
                .ToDictionary(l => NormalizeUrl(l.Url), l => l, StringComparer.OrdinalIgnoreCase);

            // Build lookup of browser bookmarks
            var browserUrls = importResult.Bookmarks
                .ToDictionary(b => NormalizeUrl(b.Url), b => b, StringComparer.OrdinalIgnoreCase);

            // Find new bookmarks (in browser but not in MyMemories)
            foreach (var bookmark in importResult.Bookmarks)
            {
                var normalizedUrl = NormalizeUrl(bookmark.Url);
                if (!existingUrls.ContainsKey(normalizedUrl))
                {
                    result.NewBookmarks.Add(bookmark);
                }
            }

            // Find deleted bookmarks (in MyMemories but not in browser)
            foreach (var link in existingLinks.Where(l => !l.IsDirectory && IsWebUrl(l.Url)))
            {
                var normalizedUrl = NormalizeUrl(link.Url);
                if (!browserUrls.ContainsKey(normalizedUrl))
                {
                    result.DeletedBookmarks.Add(new BookmarkItem
                    {
                        Name = link.Title,
                        Url = link.Url,
                        FolderPath = link.CategoryPath,
                        DateAdded = link.CreatedDate,
                        DateModified = link.ModifiedDate
                    });
                }
            }

            // Find modified bookmarks (same URL but different title)
            foreach (var bookmark in importResult.Bookmarks)
            {
                var normalizedUrl = NormalizeUrl(bookmark.Url);
                if (existingUrls.TryGetValue(normalizedUrl, out var existingLink))
                {
                    if (!string.Equals(bookmark.Name, existingLink.Title, StringComparison.Ordinal))
                    {
                        result.ModifiedBookmarks.Add((bookmark, existingLink));
                    }
                }
            }

            result.TotalBrowserBookmarks = importResult.Bookmarks.Count;
            result.TotalExistingLinks = existingLinks.Count(l => !l.IsDirectory && IsWebUrl(l.Url));
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error detecting changes: {ex.Message}";
            LogUtilities.LogError("BookmarkImporterService.DetectChangesAsync", 
                "Failed to detect changes", ex);
        }

        return result;
    }

    /// <summary>
    /// Normalizes a URL for comparison (removes trailing slash, normalizes scheme).
    /// </summary>
    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            var uri = new Uri(url);
            var normalized = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            if (!string.IsNullOrEmpty(uri.Query))
                normalized += uri.Query;
            return normalized.TrimEnd('/').ToLowerInvariant();
        }
        catch
        {
            return url.TrimEnd('/').ToLowerInvariant();
        }
    }

    /// <summary>
    /// Checks if a URL is a web URL (http/https).
    /// </summary>
    private static bool IsWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Imports bookmarks from a Chrome-based browser.
    /// </summary>
    public async Task<BookmarkImportResult> ImportBookmarksAsync(string bookmarksPath, ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        var result = new BookmarkImportResult
        {
            SourcePath = bookmarksPath
        };

        try
        {
            // Check if file is locked (browser is running)
            if (IsFileLocked(bookmarksPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Browser bookmarks file is locked. Please close the browser and try again.";
                return result;
            }

            // Read and parse bookmarks
            var json = await File.ReadAllTextAsync(bookmarksPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Invalid bookmarks file format.";
                return result;
            }

            // Parse all bookmark folders
            var bookmarks = new List<BookmarkItem>();
            
            if (bookmarkFile.roots.bookmark_bar != null)
            {
                ParseBookmarkFolder(bookmarkFile.roots.bookmark_bar, bookmarks, "Bookmarks Bar", options);
            }

            if (bookmarkFile.roots.other != null)
            {
                ParseBookmarkFolder(bookmarkFile.roots.other, bookmarks, "Other Bookmarks", options);
            }

            if (bookmarkFile.roots.synced != null)
            {
                ParseBookmarkFolder(bookmarkFile.roots.synced, bookmarks, "Mobile Bookmarks", options);
            }

            result.Bookmarks = bookmarks;
            result.TotalBookmarks = bookmarks.Count;
            result.IsSuccess = true;

            LogUtilities.LogInfo("BookmarkImporterService.ImportBookmarksAsync", 
                $"Successfully imported {bookmarks.Count} bookmarks from {bookmarksPath}");

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error importing bookmarks: {ex.Message}";
            
            LogUtilities.LogError("BookmarkImporterService.ImportBookmarksAsync", 
                "Failed to import bookmarks", ex);
            
            return result;
        }
    }

    /// <summary>
    /// Recursively parses a bookmark folder.
    /// Respects excluded folders, domain filter, and folder filter options.
    /// </summary>
    private void ParseBookmarkFolder(ChromeBookmarkNode folder, List<BookmarkItem> bookmarks, 
        string parentPath, ImportOptions options, int depth = 0)
    {
        if (folder.children == null || depth > 20) // Prevent infinite recursion
            return;

        foreach (var child in folder.children)
        {
            if (child.type == "url")
            {
                // Skip if URL doesn't match URL filter
                if (options.UrlFilter != null && !child.url?.Contains(options.UrlFilter, StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                // Skip if URL doesn't match domain filter
                if (!string.IsNullOrEmpty(options.DomainFilter))
                {
                    if (!MatchesDomainFilter(child.url, options.DomainFilter))
                        continue;
                }

                // Skip if URL doesn't match any of the multi-domain filters
                if (options.DomainFilters.Count > 0)
                {
                    if (!options.DomainFilters.Any(df => MatchesDomainFilter(child.url, df)))
                        continue;
                }

                // Skip if folder path doesn't match folder filter
                if (!string.IsNullOrEmpty(options.FolderFilter))
                {
                    if (!parentPath.Contains(options.FolderFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // Skip if folder is not in selected folders list
                if (options.SelectedFolders.Count > 0)
                {
                    var matchesSelectedFolder = options.SelectedFolders.Any(sf => 
                        parentPath.StartsWith(sf, StringComparison.OrdinalIgnoreCase) ||
                        sf.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase));
                    if (!matchesSelectedFolder)
                        continue;
                }

                // Skip if bookmark is older than the date filter
                if (options.DateAddedAfter.HasValue && child.date_added_value.HasValue)
                {
                    var dateAdded = DateTimeOffset.FromUnixTimeMilliseconds(child.date_added_value.Value / 1000).DateTime;
                    if (dateAdded < options.DateAddedAfter.Value)
                        continue;
                }

                bookmarks.Add(new BookmarkItem
                {
                    Name = child.name ?? "Untitled",
                    Url = child.url ?? string.Empty,
                    FolderPath = parentPath,
                    DateAdded = child.date_added_value.HasValue 
                        ? DateTimeOffset.FromUnixTimeMilliseconds(child.date_added_value.Value / 1000).DateTime 
                        : DateTime.Now,
                    DateModified = child.date_modified_value.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(child.date_modified_value.Value / 1000).DateTime
                        : DateTime.Now
                });
            }
            else if (child.type == "folder" && options.IncludeFolders)
            {
                // Skip excluded folders (e.g., "MyMemories" export folder)
                if (options.ExcludedFolders.Any(ef => 
                    string.Equals(child.name, ef, StringComparison.OrdinalIgnoreCase)))
                {
                    LogUtilities.LogInfo("BookmarkImporterService.ParseBookmarkFolder",
                        $"Skipping excluded folder: '{child.name}'");
                    continue;
                }

                var newPath = string.IsNullOrEmpty(parentPath) 
                    ? child.name 
                    : $"{parentPath} > {child.name}";
                
                ParseBookmarkFolder(child, bookmarks, newPath, options, depth + 1);
            }
        }
    }

    /// <summary>
    /// Checks if a URL matches the domain filter.
    /// </summary>
    private bool MatchesDomainFilter(string? url, string domainFilter)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        try
        {
            var uri = new Uri(url);
            return uri.Host.Contains(domainFilter, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If URL parsing fails, do simple string match
            return url.Contains(domainFilter, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Checks if a file is locked by another process.
    /// </summary>
    private bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Exports MyMemories links to browser bookmarks format (READ-ONLY - for backup purposes).
    /// WARNING: Writing to browser bookmarks while browser is running can cause data loss.
    /// This should only be used when the browser is closed.
    /// </summary>
    public async Task<bool> ExportToBookmarksAsync(List<BookmarkItem> bookmarks, string targetPath, bool createBackup = true)
    {
        try
        {
            // Check if file is locked
            if (IsFileLocked(targetPath))
            {
                LogUtilities.LogError("BookmarkImporterService.ExportToBookmarksAsync", 
                    "Cannot export - browser bookmarks file is locked (browser is running)", null);
                return false;
            }

            // Create backup if requested
            if (createBackup && File.Exists(targetPath))
            {
                var backupPath = targetPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(targetPath, backupPath, true);
                
                LogUtilities.LogInfo("BookmarkImporterService.ExportToBookmarksAsync", 
                    $"Created backup: {backupPath}");
            }

            // Read existing bookmarks file to preserve structure
            var json = await File.ReadAllTextAsync(targetPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots?.other == null)
            {
                LogUtilities.LogError("BookmarkImporterService.ExportToBookmarksAsync", 
                    "Invalid bookmarks file structure", null);
                return false;
            }

            // Create a new folder for MyMemories exports
            var myMemoriesFolder = new ChromeBookmarkNode
            {
                date_added_str = (DateTimeOffset.Now.ToUnixTimeMilliseconds() * 1000).ToString(),
                date_modified_str = (DateTimeOffset.Now.ToUnixTimeMilliseconds() * 1000).ToString(),
                id = GenerateUniqueId(),
                name = "MyMemories Export",
                type = "folder",
                children = new List<ChromeBookmarkNode>()
            };

            // Add bookmarks to the folder
            foreach (var bookmark in bookmarks)
            {
                myMemoriesFolder.children.Add(new ChromeBookmarkNode
                {
                    date_added_str = (new DateTimeOffset(bookmark.DateAdded).ToUnixTimeMilliseconds() * 1000).ToString(),
                    date_modified_str = (new DateTimeOffset(bookmark.DateModified).ToUnixTimeMilliseconds() * 1000).ToString(),
                    id = GenerateUniqueId(),
                    name = bookmark.Name,
                    type = "url",
                    url = bookmark.Url
                });
            }

            // Add to "Other Bookmarks" folder
            bookmarkFile.roots.other.children ??= new List<ChromeBookmarkNode>();
            bookmarkFile.roots.other.children.Add(myMemoriesFolder);

            // Update version and checksum
            bookmarkFile.version = 1;
            bookmarkFile.checksum = GenerateChecksum(bookmarkFile);

            // Write back to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var updatedJson = JsonSerializer.Serialize(bookmarkFile, options);
            await File.WriteAllTextAsync(targetPath, updatedJson);

            LogUtilities.LogInfo("BookmarkImporterService.ExportToBookmarksAsync", 
                $"Successfully exported {bookmarks.Count} bookmarks to {targetPath}");

            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("BookmarkImporterService.ExportToBookmarksAsync", 
                "Failed to export bookmarks", ex);
            return false;
        }
    }

    /// <summary>
    /// Generates a unique ID for bookmark nodes.
    /// </summary>
    private string GenerateUniqueId()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
    }

    /// <summary>
    /// Generates a checksum for the bookmarks file (simplified version).
    /// Chrome uses MD5 hash of the bookmarks content.
    /// </summary>
    private string GenerateChecksum(ChromeBookmarkFile bookmarkFile)
    {
        // For simplicity, return empty string
        // Chrome will regenerate the checksum when it starts
        return string.Empty;
    }
}

/// <summary>
/// Browser information.
/// </summary>
public class BrowserInfo
{
    public string Name { get; set; } = string.Empty;
    public BrowserType BrowserType { get; set; }
    public string BookmarksPath { get; set; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>
/// Bookmark import result.
/// </summary>
public class BookmarkImportResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public List<BookmarkItem> Bookmarks { get; set; } = new();
    public int TotalBookmarks { get; set; }
}

/// <summary>
/// Import options.
/// </summary>
public class ImportOptions
{
    public bool IncludeFolders { get; set; } = true;
    public string? UrlFilter { get; set; }
    public bool SkipDuplicates { get; set; } = true;
    
    /// <summary>
    /// Folder names to exclude during import (e.g., "MyMemories" export folder).
    /// </summary>
    public List<string> ExcludedFolders { get; set; } = new() { "MyMemories" };
    
    /// <summary>
    /// Domain filter - only import URLs containing this domain (e.g., "github.com").
    /// </summary>
    public string? DomainFilter { get; set; }
    
    /// <summary>
    /// Multiple domain filters - only import URLs matching any of these domains.
    /// </summary>
    public List<string> DomainFilters { get; set; } = new();
    
    /// <summary>
    /// Folder path filter - only import from folders matching this path (e.g., "Bookmarks Bar > Dev").
    /// </summary>
    public string? FolderFilter { get; set; }
    
    /// <summary>
    /// Selected folder paths - only import from these specific folders.
    /// </summary>
    public List<string> SelectedFolders { get; set; } = new();
    
    /// <summary>
    /// Only import bookmarks added after this date.
    /// </summary>
    public DateTime? DateAddedAfter { get; set; }
}

/// <summary>
/// Represents the folder structure of a browser's bookmarks.
/// </summary>
public class BrowserFolderStructure
{
    public string SourcePath { get; set; } = string.Empty;
    public List<BrowserFolder> RootFolders { get; set; } = new();
    public HashSet<string> UniqueDomains { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a folder in the browser's bookmarks.
/// </summary>
public class BrowserFolder
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int BookmarkCount { get; set; }
    public int TotalBookmarkCount { get; set; }
    public List<BrowserFolder> SubFolders { get; set; } = new();
    public bool IsSelected { get; set; } = true;

    public override string ToString() => $"{Name} ({TotalBookmarkCount} bookmarks)";
}

/// <summary>
/// Result of bookmark sync detection.
/// </summary>
public class BookmarkSyncResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Bookmarks in browser that don't exist in MyMemories.
    /// </summary>
    public List<BookmarkItem> NewBookmarks { get; set; } = new();
    
    /// <summary>
    /// Bookmarks in MyMemories that don't exist in browser.
    /// </summary>
    public List<BookmarkItem> DeletedBookmarks { get; set; } = new();
    
    /// <summary>
    /// Bookmarks with same URL but different title (browser bookmark, existing link).
    /// </summary>
    public List<(BookmarkItem BrowserBookmark, LinkItem ExistingLink)> ModifiedBookmarks { get; set; } = new();
    
    public int TotalBrowserBookmarks { get; set; }
    public int TotalExistingLinks { get; set; }
    
    public bool HasChanges => NewBookmarks.Count > 0 || DeletedBookmarks.Count > 0 || ModifiedBookmarks.Count > 0;
}
