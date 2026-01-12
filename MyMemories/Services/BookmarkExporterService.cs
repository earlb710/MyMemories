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
/// Service for exporting bookmarks to browser bookmark files.
/// </summary>
public class BookmarkExporterService
{
    // Chrome uses microseconds since Windows epoch (Jan 1, 1601)
    private static readonly DateTime WindowsEpoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    
    // Counter for unique IDs to avoid duplicates
    private long _idCounter;

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
    /// Checks if the browser bookmarks file is locked (browser is running).
    /// </summary>
    public bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Collects all web URL bookmarks from a tree of nodes.
    /// Uses a HashSet to prevent duplicate URLs from being collected.
    /// Root categories are skipped in the folder path as they map to the export folder name.
    /// </summary>
    public List<ExportBookmarkItem> CollectBookmarksFromNodes(IList<TreeViewNode> selectedNodes)
    {
        var bookmarks = new List<ExportBookmarkItem>();
        var collectedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in selectedNodes)
        {
            // For root nodes (categories selected at top level), skip the category name
            // as it maps to the export folder (e.g., "MyMemories")
            if (node.Content is CategoryItem)
            {
                // Process children directly without adding root category to path
                foreach (var child in node.Children)
                {
                    CollectBookmarksRecursive(child, bookmarks, collectedUrls, string.Empty, isRootLevel: true);
                }
            }
            else
            {
                // For directly selected links, process normally
                CollectBookmarksRecursive(node, bookmarks, collectedUrls, string.Empty, isRootLevel: true);
            }
        }

        return bookmarks;
    }

    private void CollectBookmarksRecursive(TreeViewNode node, List<ExportBookmarkItem> bookmarks, 
        HashSet<string> collectedUrls, string parentPath, bool isRootLevel = false)
    {
        if (node.Content is CategoryItem category)
        {
            // Build path: subcategories get added to the path
            var currentPath = string.IsNullOrEmpty(parentPath) ? category.Name : $"{parentPath}/{category.Name}";

            foreach (var child in node.Children)
            {
                CollectBookmarksRecursive(child, bookmarks, collectedUrls, currentPath, isRootLevel: false);
            }
        }
        else if (node.Content is LinkItem link)
        {
            // Only export web URLs (http/https) that haven't been collected yet
            if (IsWebUrl(link.Url) && collectedUrls.Add(link.Url))
            {
                bookmarks.Add(new ExportBookmarkItem
                {
                    Name = link.Title,
                    Url = link.Url,
                    FolderPath = parentPath,
                    DateAdded = link.CreatedDate,
                    DateModified = link.ModifiedDate
                });
            }

            // Process child links (sub-links)
            foreach (var child in node.Children)
            {
                if (child.Content is LinkItem childLink && !childLink.IsCatalogEntry)
                {
                    var childPath = string.IsNullOrEmpty(parentPath) ? link.Title : $"{parentPath}/{link.Title}";
                    CollectBookmarksRecursive(child, bookmarks, collectedUrls, childPath, isRootLevel: false);
                }
            }
        }
    }

    /// <summary>
    /// Exports bookmarks to a browser's bookmarks file.
    /// </summary>
    public async Task<BookmarkExportResult> ExportBookmarksAsync(
        string targetBookmarksPath,
        List<ExportBookmarkItem> bookmarks,
        string folderName,
        bool createBackup = true)
    {
        var result = new BookmarkExportResult
        {
            TargetPath = targetBookmarksPath,
            TotalBookmarks = bookmarks.Count
        };

        try
        {
            // Check if file is locked
            if (IsFileLocked(targetBookmarksPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Browser bookmarks file is locked. Please close the browser and try again.";
                return result;
            }

            // Create backup if requested
            if (createBackup && File.Exists(targetBookmarksPath))
            {
                var backupPath = targetBookmarksPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(targetBookmarksPath, backupPath, true);
                result.BackupPath = backupPath;

                LogUtilities.LogInfo("BookmarkExporterService.ExportBookmarksAsync",
                    $"Created backup: {backupPath}");
            }

            // Read existing bookmarks file
            var json = await File.ReadAllTextAsync(targetBookmarksPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots?.other == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Invalid bookmarks file structure.";
                return result;
            }

            // Initialize ID counter based on existing max ID
            _idCounter = GetMaxId(bookmarkFile) + 1;

            // Remove existing folder with the same name (to allow sync/overwrite)
            bookmarkFile.roots.other.children ??= new List<ChromeBookmarkNode>();
            var existingFolder = bookmarkFile.roots.other.children
                .FirstOrDefault(c => c.type == "folder" && c.name == folderName);
            if (existingFolder != null)
            {
                bookmarkFile.roots.other.children.Remove(existingFolder);
                LogUtilities.LogInfo("BookmarkExporterService.ExportBookmarksAsync",
                    $"Removed existing folder '{folderName}' for overwrite");
            }

            // Create the export folder
            var exportFolder = new ChromeBookmarkNode
            {
                date_added_str = ToChromeTimestamp(DateTime.UtcNow),
                date_modified_str = ToChromeTimestamp(DateTime.UtcNow),
                id = GenerateUniqueId(),
                name = folderName,
                type = "folder",
                children = new List<ChromeBookmarkNode>()
            };

            // Group bookmarks by folder path
            var folderStructure = new Dictionary<string, ChromeBookmarkNode>();
            folderStructure[string.Empty] = exportFolder;

            foreach (var bookmark in bookmarks)
            {
                var targetFolder = GetOrCreateFolder(folderStructure, exportFolder, bookmark.FolderPath);

                targetFolder.children!.Add(new ChromeBookmarkNode
                {
                    date_added_str = ToChromeTimestamp(bookmark.DateAdded.ToUniversalTime()),
                    date_modified_str = ToChromeTimestamp(bookmark.DateModified.ToUniversalTime()),
                    id = GenerateUniqueId(),
                    name = bookmark.Name,
                    type = "url",
                    url = bookmark.Url
                });
            }

            // Add to "Other Bookmarks" folder
            bookmarkFile.roots.other.children.Add(exportFolder);

            // Update date_modified on the "other" folder
            bookmarkFile.roots.other.date_modified_str = ToChromeTimestamp(DateTime.UtcNow);

            // Update version (keep existing or set to 1)
            if (bookmarkFile.version < 1)
                bookmarkFile.version = 1;
            
            // Remove checksum - Chrome will regenerate it
            bookmarkFile.checksum = null;

            // Write back to file - preserve all fields
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var updatedJson = JsonSerializer.Serialize(bookmarkFile, options);
            await File.WriteAllTextAsync(targetBookmarksPath, updatedJson, Encoding.UTF8);

            result.IsSuccess = true;
            result.ExportedCount = bookmarks.Count;

            LogUtilities.LogInfo("BookmarkExporterService.ExportBookmarksAsync",
                $"Successfully exported {bookmarks.Count} bookmarks to {targetBookmarksPath}");

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error exporting bookmarks: {ex.Message}";

            LogUtilities.LogError("BookmarkExporterService.ExportBookmarksAsync",
                "Failed to export bookmarks", ex);

            return result;
        }
    }

    private ChromeBookmarkNode GetOrCreateFolder(
        Dictionary<string, ChromeBookmarkNode> folderStructure,
        ChromeBookmarkNode rootFolder,
        string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return rootFolder;

        if (folderStructure.TryGetValue(folderPath, out var existingFolder))
            return existingFolder;

        // Create folder hierarchy
        var parts = folderPath.Split('/');
        var currentPath = string.Empty;
        var currentParent = rootFolder;

        foreach (var part in parts)
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

            if (!folderStructure.TryGetValue(currentPath, out var folder))
            {
                folder = new ChromeBookmarkNode
                {
                    date_added_str = ToChromeTimestamp(DateTime.UtcNow),
                    date_modified_str = ToChromeTimestamp(DateTime.UtcNow),
                    id = GenerateUniqueId(),
                    name = part,
                    type = "folder",
                    children = new List<ChromeBookmarkNode>()
                };

                currentParent.children!.Add(folder);
                folderStructure[currentPath] = folder;
            }

            currentParent = folder;
        }

        return currentParent;
    }

    /// <summary>
    /// Converts a DateTime to Chrome's timestamp format (microseconds since Jan 1, 1601).
    /// </summary>
    private static string ToChromeTimestamp(DateTime dateTime)
    {
        var utcDateTime = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        var microseconds = (utcDateTime - WindowsEpoch).Ticks / 10; // Ticks are 100-nanosecond intervals
        return microseconds.ToString();
    }

    /// <summary>
    /// Gets the maximum ID used in the bookmarks file.
    /// </summary>
    private long GetMaxId(ChromeBookmarkFile bookmarkFile)
    {
        long maxId = 0;
        
        if (bookmarkFile.roots?.bookmark_bar != null)
            maxId = Math.Max(maxId, GetMaxIdRecursive(bookmarkFile.roots.bookmark_bar));
        if (bookmarkFile.roots?.other != null)
            maxId = Math.Max(maxId, GetMaxIdRecursive(bookmarkFile.roots.other));
        if (bookmarkFile.roots?.synced != null)
            maxId = Math.Max(maxId, GetMaxIdRecursive(bookmarkFile.roots.synced));
            
        return maxId;
    }

    private long GetMaxIdRecursive(ChromeBookmarkNode node)
    {
        long maxId = 0;
        
        if (long.TryParse(node.id, out var nodeId))
            maxId = nodeId;
            
        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                maxId = Math.Max(maxId, GetMaxIdRecursive(child));
            }
        }
        
        return maxId;
    }

    private static bool IsWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private string GenerateUniqueId()
    {
        return (_idCounter++).ToString();
    }

    /// <summary>
    /// Detects changes in the browser's MyMemories folder that should be synced back.
    /// Compares bookmarks in the browser's export folder with existing MyMemories links.
    /// </summary>
    public async Task<BrowserSyncDetectionResult> DetectBrowserChangesAsync(
        string browserBookmarksPath,
        string folderName,
        IEnumerable<LinkItem> existingLinks,
        DateTime? lastSyncDate = null)
    {
        var result = new BrowserSyncDetectionResult();

        try
        {
            if (IsFileLocked(browserBookmarksPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Browser bookmarks file is locked. Please close the browser.";
                return result;
            }

            var json = await File.ReadAllTextAsync(browserBookmarksPath);
            var bookmarkFile = JsonSerializer.Deserialize<ChromeBookmarkFile>(json);

            if (bookmarkFile?.roots?.other == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Invalid bookmarks file structure.";
                return result;
            }

            // Find the MyMemories folder in "Other Bookmarks"
            var myMemoriesFolder = bookmarkFile.roots.other.children?
                .FirstOrDefault(c => c.type == "folder" && 
                    string.Equals(c.name, folderName, StringComparison.OrdinalIgnoreCase));

            if (myMemoriesFolder == null)
            {
                // No folder exists yet - nothing to sync from browser
                result.IsSuccess = true;
                return result;
            }

            // Collect all bookmarks from the browser's MyMemories folder
            var browserBookmarks = new List<SyncedBookmarkItem>();
            CollectBrowserBookmarksRecursive(myMemoriesFolder, browserBookmarks, string.Empty);

            // Build lookup of existing URLs (normalized for comparison) - handle duplicates by taking first
            var existingUrlLookup = new Dictionary<string, LinkItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in existingLinks.Where(l => !l.IsDirectory && IsWebUrl(l.Url)))
            {
                var normalizedUrl = NormalizeUrl(link.Url);
                if (!existingUrlLookup.ContainsKey(normalizedUrl))
                {
                    existingUrlLookup[normalizedUrl] = link;
                }
            }

            // Build lookup of browser URLs - handle duplicates by taking first
            var browserUrlLookup = new Dictionary<string, SyncedBookmarkItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var bookmark in browserBookmarks)
            {
                var normalizedUrl = NormalizeUrl(bookmark.Url);
                if (!browserUrlLookup.ContainsKey(normalizedUrl))
                {
                    browserUrlLookup[normalizedUrl] = bookmark;
                }
            }

            // Find new bookmarks (in browser but not in MyMemories)
            foreach (var browserBookmark in browserBookmarks)
            {
                var normalizedUrl = NormalizeUrl(browserBookmark.Url);
                
                if (!existingUrlLookup.ContainsKey(normalizedUrl))
                {
                    // Check if added after last sync
                    if (!lastSyncDate.HasValue || browserBookmark.DateAdded > lastSyncDate.Value)
                    {
                        browserBookmark.Action = SyncAction.Add;
                        result.NewInBrowser.Add(browserBookmark);
                    }
                }
                else
                {
                    // Check for title changes
                    var existingLink = existingUrlLookup[normalizedUrl];
                    if (!string.Equals(browserBookmark.Name, existingLink.Title, StringComparison.Ordinal))
                    {
                        // Only consider it modified if changed after last sync
                        if (!lastSyncDate.HasValue || browserBookmark.DateModified > lastSyncDate.Value)
                        {
                            browserBookmark.Action = SyncAction.Update;
                            result.ModifiedInBrowser.Add(browserBookmark);
                        }
                    }
                }
            }

            // Find deleted bookmarks (in MyMemories but not in browser anymore)
            foreach (var link in existingLinks.Where(l => !l.IsDirectory && IsWebUrl(l.Url)))
            {
                var normalizedUrl = NormalizeUrl(link.Url);
                
                if (!browserUrlLookup.ContainsKey(normalizedUrl))
                {
                    // This bookmark was deleted from the browser
                    result.DeletedFromBrowser.Add(new SyncedBookmarkItem
                    {
                        Name = link.Title,
                        Url = link.Url,
                        FolderPath = link.CategoryPath,
                        DateAdded = link.CreatedDate,
                        DateModified = link.ModifiedDate,
                        Action = SyncAction.Delete
                    });
                }
            }

            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error detecting browser changes: {ex.Message}";
            LogUtilities.LogError("BookmarkExporterService.DetectBrowserChangesAsync",
                "Failed to detect browser changes", ex);
        }

        return result;
    }

    /// <summary>
    /// Recursively collects bookmarks from a browser folder node.
    /// </summary>
    private void CollectBrowserBookmarksRecursive(ChromeBookmarkNode node, List<SyncedBookmarkItem> bookmarks, string parentPath)
    {
        if (node.children == null)
            return;

        foreach (var child in node.children)
        {
            if (child.type == "url" && IsWebUrl(child.url))
            {
                bookmarks.Add(new SyncedBookmarkItem
                {
                    Name = child.name ?? "Untitled",
                    Url = child.url ?? string.Empty,
                    FolderPath = parentPath,
                    DateAdded = ParseChromeTimestamp(child.date_added_str),
                    DateModified = ParseChromeTimestamp(child.date_modified_str)
                });
            }
            else if (child.type == "folder")
            {
                var childPath = string.IsNullOrEmpty(parentPath) 
                    ? child.name 
                    : $"{parentPath}/{child.name}";
                CollectBrowserBookmarksRecursive(child, bookmarks, childPath);
            }
        }
    }

    /// <summary>
    /// Parses Chrome's timestamp format to DateTime.
    /// </summary>
    private static DateTime ParseChromeTimestamp(string? timestampStr)
    {
        if (string.IsNullOrEmpty(timestampStr) || !long.TryParse(timestampStr, out var microseconds))
            return DateTime.Now;

        try
        {
            var ticks = microseconds * 10; // Convert microseconds to 100-nanosecond intervals
            return WindowsEpoch.AddTicks(ticks).ToLocalTime();
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    /// Normalizes a URL for comparison.
    /// </summary>
    private static string NormalizeUrl(string url)
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
    /// Exports bookmarks with optional sync from browser.
    /// </summary>
    public async Task<BookmarkExportResult> ExportWithSyncAsync(
        string targetBookmarksPath,
        List<ExportBookmarkItem> bookmarks,
        string folderName,
        IEnumerable<LinkItem> existingLinks,
        DateTime? lastSyncDate,
        bool syncFromBrowser,
        bool createBackup = true)
    {
        var result = new BookmarkExportResult
        {
            TargetPath = targetBookmarksPath,
            TotalBookmarks = bookmarks.Count
        };

        try
        {
            // First, detect changes from browser if sync is enabled
            if (syncFromBrowser)
            {
                var syncDetection = await DetectBrowserChangesAsync(
                    targetBookmarksPath, folderName, existingLinks, lastSyncDate);

                if (syncDetection.IsSuccess && syncDetection.HasChanges)
                {
                    result.SyncedItems.AddRange(syncDetection.NewInBrowser);
                    result.SyncedItems.AddRange(syncDetection.ModifiedInBrowser);
                    result.SyncedItems.AddRange(syncDetection.DeletedFromBrowser);
                    result.SyncedFromBrowserCount = syncDetection.TotalChanges;
                }
            }

            // Now do the export
            var exportResult = await ExportBookmarksAsync(targetBookmarksPath, bookmarks, folderName, createBackup);
            
            result.IsSuccess = exportResult.IsSuccess;
            result.ErrorMessage = exportResult.ErrorMessage;
            result.BackupPath = exportResult.BackupPath;
            result.ExportedCount = exportResult.ExportedCount;

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error during export/sync: {ex.Message}";
            LogUtilities.LogError("BookmarkExporterService.ExportWithSyncAsync",
                "Failed to export with sync", ex);
            return result;
        }
    }
}

/// <summary>
/// Bookmark item for export.
/// </summary>
public class ExportBookmarkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
}

/// <summary>
/// Result of a bookmark export operation.
/// </summary>
public class BookmarkExportResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public string? BackupPath { get; set; }
    public int TotalBookmarks { get; set; }
    public int ExportedCount { get; set; }
    public int SyncedFromBrowserCount { get; set; }
    public List<SyncedBookmarkItem> SyncedItems { get; set; } = new();
}

/// <summary>
/// Represents a bookmark synced from browser back to MyMemories.
/// </summary>
public class SyncedBookmarkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    public SyncAction Action { get; set; }
}

/// <summary>
/// Sync action type.
/// </summary>
public enum SyncAction
{
    Add,
    Update,
    Delete
}

/// <summary>
/// Result of detecting sync changes from browser.
/// </summary>
public class BrowserSyncDetectionResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// New bookmarks added in browser that don't exist in MyMemories.
    /// </summary>
    public List<SyncedBookmarkItem> NewInBrowser { get; set; } = new();
    
    /// <summary>
    /// Bookmarks modified in browser (title changed).
    /// </summary>
    public List<SyncedBookmarkItem> ModifiedInBrowser { get; set; } = new();
    
    /// <summary>
    /// Bookmarks deleted from browser that exist in MyMemories.
    /// </summary>
    public List<SyncedBookmarkItem> DeletedFromBrowser { get; set; } = new();
    
    public bool HasChanges => NewInBrowser.Count > 0 || ModifiedInBrowser.Count > 0 || DeletedFromBrowser.Count > 0;
    public int TotalChanges => NewInBrowser.Count + ModifiedInBrowser.Count + DeletedFromBrowser.Count;
}
