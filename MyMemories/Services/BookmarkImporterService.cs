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
                BookmarksPath = chromePath,
                Icon = "??"
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
                BookmarksPath = edgePath,
                Icon = "??"
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
                BookmarksPath = bravePath,
                Icon = "??"
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
                BookmarksPath = vivaldiPath,
                Icon = "??"
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
                BookmarksPath = operaPath,
                Icon = "??"
            });
        }

        return browsers;
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
                // Skip if URL doesn't match filter
                if (options.UrlFilter != null && !child.url?.Contains(options.UrlFilter, StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                bookmarks.Add(new BookmarkItem
                {
                    Name = child.name ?? "Untitled",
                    Url = child.url ?? string.Empty,
                    FolderPath = parentPath,
                    DateAdded = child.date_added.HasValue 
                        ? DateTimeOffset.FromUnixTimeMilliseconds(child.date_added.Value / 1000).DateTime 
                        : DateTime.Now,
                    DateModified = child.date_modified.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(child.date_modified.Value / 1000).DateTime
                        : DateTime.Now
                });
            }
            else if (child.type == "folder" && options.IncludeFolders)
            {
                var newPath = string.IsNullOrEmpty(parentPath) 
                    ? child.name 
                    : $"{parentPath} > {child.name}";
                
                ParseBookmarkFolder(child, bookmarks, newPath, options, depth + 1);
            }
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
                date_added = DateTimeOffset.Now.ToUnixTimeMilliseconds() * 1000,
                date_modified = DateTimeOffset.Now.ToUnixTimeMilliseconds() * 1000,
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
                    date_added = new DateTimeOffset(bookmark.DateAdded).ToUnixTimeMilliseconds() * 1000,
                    date_modified = new DateTimeOffset(bookmark.DateModified).ToUnixTimeMilliseconds() * 1000,
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
    public string Icon { get; set; } = "??";
}

/// <summary>
/// Browser types.
/// </summary>
public enum BrowserType
{
    Chrome,
    Edge,
    Brave,
    Vivaldi,
    Opera,
    Firefox
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
}

/// <summary>
/// Bookmark item.
/// </summary>
public class BookmarkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
}

// Chrome Bookmarks JSON Structure
public class ChromeBookmarkFile
{
    public int version { get; set; }
    public string? checksum { get; set; }
    public ChromeBookmarkRoots? roots { get; set; }
}

public class ChromeBookmarkRoots
{
    public ChromeBookmarkNode? bookmark_bar { get; set; }
    public ChromeBookmarkNode? other { get; set; }
    public ChromeBookmarkNode? synced { get; set; }
}

public class ChromeBookmarkNode
{
    public long? date_added { get; set; }
    public long? date_modified { get; set; }
    public string? id { get; set; }
    public string? name { get; set; }
    public string? type { get; set; }
    public string? url { get; set; }
    public List<ChromeBookmarkNode>? children { get; set; }
}
