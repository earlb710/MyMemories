using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories;

/// <summary>
/// URL accessibility status for bookmark links.
/// </summary>
public enum UrlStatus
{
    Unknown = 0,     // Not checked yet
    Accessible = 1,  // Green - URL is accessible
    Error = 2,       // Yellow - URL returned an error
    NotFound = 3     // Red - URL does not exist (404, DNS failure, etc.)
}

/// <summary>
/// Browser types for bookmark import.
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
/// Password protection type for categories.
/// </summary>
public enum PasswordProtectionType
{
    None = 0,
    GlobalPassword = 1,
    OwnPassword = 2
}

/// <summary>
/// Sort options for categories and catalog items.
/// </summary>
public enum SortOption
{
    NameAscending,
    NameDescending,
    SizeAscending,
    SizeDescending,
    DateAscending,
    DateDescending
}

/// <summary>
/// Represents a category item in the tree view.
/// </summary>
public class CategoryItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public PasswordProtectionType PasswordProtection { get; set; } = PasswordProtectionType.None;
    public string? OwnPasswordHash { get; set; }
    public SortOption SortOrder { get; set; } = SortOption.NameAscending;
    
    // Bookmark import metadata
    public bool IsBookmarkImport { get; set; } = false;
    public BrowserType? SourceBrowserType { get; set; }
    public string? SourceBrowserName { get; set; }
    public string? SourceBookmarksPath { get; set; }
    public DateTime? LastBookmarkImportDate { get; set; }
    public int? ImportedBookmarkCount { get; set; }
    
    // URL Bookmarks category - restricts to URLs only
    public bool IsBookmarkCategory { get; set; } = false;
    
    // Bookmark lookup - makes this category available for bookmark search
    public bool IsBookmarkLookup { get; set; } = false;

    public override string ToString() => $"{Icon} {Name}";
}

/// <summary>
/// Represents a link item (file, directory, or URL) in the tree view.
/// </summary>
public class LinkItem : INotifyPropertyChanged
{
    private int _catalogFileCount;
    private DateTime? _lastCatalogUpdate;
    private bool _isZipPasswordProtected;
    private UrlStatus _urlStatus = UrlStatus.Unknown;
    private DateTime? _urlLastChecked;
    private string _urlStatusMessage = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
    public bool IsCatalogEntry { get; set; }
    public SortOption CatalogSortOrder { get; set; } = SortOption.NameAscending;
    
    /// <summary>
    /// URL accessibility status (for bookmark categories).
    /// </summary>
    public UrlStatus UrlStatus
    {
        get => _urlStatus;
        set
        {
            if (_urlStatus != value)
            {
                _urlStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowUrlStatusBadge));
                OnPropertyChanged(nameof(UrlStatusColor));
            }
        }
    }

    /// <summary>
    /// Date and time when the URL status was last checked.
    /// </summary>
    public DateTime? UrlLastChecked
    {
        get => _urlLastChecked;
        set
        {
            if (_urlLastChecked != value)
            {
                _urlLastChecked = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Detailed message from the last URL status check (e.g., HTTP status code, error message).
    /// </summary>
    public string UrlStatusMessage
    {
        get => _urlStatusMessage;
        set
        {
            if (_urlStatusMessage != value)
            {
                _urlStatusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the visibility of the URL status badge.
    /// </summary>
    [JsonIgnore]
    public Visibility ShowUrlStatusBadge => UrlStatus != UrlStatus.Unknown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets the color for the URL status badge.
    /// </summary>
    [JsonIgnore]
    public Windows.UI.Color UrlStatusColor
    {
        get
        {
            return UrlStatus switch
            {
                UrlStatus.Accessible => Microsoft.UI.Colors.LimeGreen,
                UrlStatus.Error => Microsoft.UI.Colors.Yellow,
                UrlStatus.NotFound => Microsoft.UI.Colors.Red,
                _ => Microsoft.UI.Colors.Gray
            };
        }
    }

    /// <summary>
    /// Gets the display text for URL status in tooltips.
    /// </summary>
    [JsonIgnore]
    public string UrlStatusDisplayText
    {
        get
        {
            return UrlStatus switch
            {
                UrlStatus.Accessible => "✓ URL is accessible",
                UrlStatus.Error => "⚠ URL returned an error",
                UrlStatus.NotFound => "✗ URL not found",
                UrlStatus.Unknown => "URL status not checked",
                _ => "URL status unknown"
            };
        }
    }

    /// <summary>
    /// Gets whether there is a URL status message to display.
    /// </summary>
    [JsonIgnore]
    public Visibility HasUrlStatusMessage => !string.IsNullOrWhiteSpace(UrlStatusMessage) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets whether the URL last checked date is available.
    /// </summary>
    [JsonIgnore]
    public Visibility HasUrlLastChecked => UrlLastChecked.HasValue ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets formatted text for when the URL was last checked.
    /// </summary>
    [JsonIgnore]
    public string UrlLastCheckedText => UrlLastChecked.HasValue 
        ? $"Last checked: {UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}" 
        : string.Empty;

    public DateTime? LastCatalogUpdate
    {
        get => _lastCatalogUpdate;
        set
        {
            if (_lastCatalogUpdate != value)
            {
                _lastCatalogUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChanged));
            }
        }
    }

    public ulong? FileSize { get; set; }
    public bool AutoRefreshCatalog { get; set; } = false;
    
    public bool IsZipPasswordProtected
    {
        get => _isZipPasswordProtected;
        set
        {
            if (_isZipPasswordProtected != value)
            {
                System.Diagnostics.Debug.WriteLine($"[LinkItem] IsZipPasswordProtected changing: {_isZipPasswordProtected} -> {value} for '{Title}'");
                _isZipPasswordProtected = value;
                OnPropertyChanged();
            }
        }
    }

    // Internal property to store catalog count for display
    [JsonIgnore]
    public int CatalogFileCount
    {
        get => _catalogFileCount;
        set
        {
            if (_catalogFileCount != value)
            {
                _catalogFileCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    /// <summary>
    /// Gets the display text for the tree node (without icon)
    /// </summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            // Show file count for catalog folders
            if (IsDirectory && !IsCatalogEntry && CatalogFileCount > 0)
            {
                var catalogInfo = $" ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")})";
                return $"{Title}{catalogInfo}";
            }

            return Title;
        }
    }

    /// <summary>
    /// Gets the visibility of the link badge for LinkOnly folders
    /// </summary>
    [JsonIgnore]
    public Visibility ShowLinkBadge
    {
        get
        {
            // Show link badge only for LinkOnly folder types (not catalog entries)
            if (IsDirectory && !IsCatalogEntry && FolderType == FolderLinkType.LinkOnly)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Gets the visibility of the change badge
    /// </summary>
    [JsonIgnore]
    public Visibility HasChanged
    {
        get
        {
            if (!IsDirectory || !LastCatalogUpdate.HasValue)
                return Visibility.Collapsed;

            if (!Directory.Exists(Url))
                return Visibility.Collapsed;

            try
            {
                // Check if this folder or any of its subdirectories have been modified
                // after the last catalog update
                return HasDirectoryChangedRecursive(Url, LastCatalogUpdate.Value)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// Recursively checks if a directory or any of its subdirectories have changed
    /// after the specified timestamp.
    /// </summary>
    private bool HasDirectoryChangedRecursive(string directoryPath, DateTime lastUpdate)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            // Method 1: Check if the directory itself was modified after last update
            if (dirInfo.LastWriteTime > lastUpdate)
            {
                return true;
            }

            // Method 2: For catalog entries (subdirectories), compare file count
            // This catches deletions that don't update LastWriteTime
            if (IsCatalogEntry)
            {
                var currentFileCount = Directory.GetFiles(directoryPath).Length;
                if (currentFileCount != CatalogFileCount)
                {
                    return true;
                }
            }

            // Method 3: Check all subdirectories recursively
            var subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subdir in subdirectories)
            {
                var subdirInfo = new DirectoryInfo(subdir);

                // Check if this subdirectory was modified after last update
                if (subdirInfo.LastWriteTime > lastUpdate)
                {
                    return true;
                }

                // Recursively check deeper subdirectories
                if (HasDirectoryChangedRecursive(subdir, lastUpdate))
                {
                    return true;
                }
            }

            // Method 4: Check all files in the current directory
            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime > lastUpdate)
                {
                    return true;
                }
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // If we can't access a directory, assume no changes
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Manually refreshes the HasChanged property by checking the file system.
    /// Call this method after operations that might change the folder state.
    /// </summary>
    public void RefreshChangeStatus()
    {
        OnPropertyChanged(nameof(HasChanged));
    }

    /// <summary>
    /// Gets the icon without the warning badge (for binding)
    /// </summary>
    [JsonIgnore]
    public string IconWithoutBadge => GetIconWithoutBadge();

    /// <summary>
    /// Gets the icon without the warning badge (for XAML binding)
    /// </summary>
    public string GetIconWithoutBadge()
    {
        // Special case: If title starts with an emoji or unicode symbol, don't show an icon
        if (!string.IsNullOrEmpty(Title) && Title.Length > 0)
        {
            // Check for high surrogate (emoji) or common symbol characters
            char firstChar = Title[0];
            if (char.IsHighSurrogate(firstChar) || 
                firstChar == '⏳' || 
                firstChar == '⚠' || 
                firstChar == '✓' || 
                firstChar == '❌' ||
                (firstChar >= 0x2600 && firstChar <= 0x26FF) || // Miscellaneous Symbols
                (firstChar >= 0x2700 && firstChar <= 0x27BF) || // Dingbats
                (firstChar >= 0x1F300 && firstChar <= 0x1F9FF))  // Emoticons and symbols
            {
                return string.Empty;
            }
        }

        // Check if this is a zip archive (directory with .zip URL)
        bool isZipArchive = IsDirectory && Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        // For directories
        if (IsDirectory)
        {
            // For zip archives, always use folder icon (will have Zip badge)
            if (isZipArchive)
            {
                return "📁";
            }

            // IMPORTANT: Catalog subdirectories should always use normal folder icon
            if (IsCatalogEntry)
            {
                return "📁"; // Normal folder icon for catalog subdirectories
            }

            // For non-catalog directories, use FolderType to determine icon
            return FolderType switch
            {
                FolderLinkType.LinkOnly => "📁",           // Normal folder with link badge overlay
                FolderLinkType.CatalogueFiles => "📂",     // Open folder for cataloged folders
                FolderLinkType.FilteredCatalogue => "🗂️",  // Card index/filtered folder icon
                _ => "📁"                                   // Default folder icon (fallback)
            };
        }

        // For web URLs (non-file URIs)
        if (Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !uri.IsFile)
            return "🌐";

        // For files (both catalog entries and direct file links), use file extension
        var extension = Path.GetExtension(Url).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" => "🎬",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "🎵",
            ".pdf" => "📋",
            ".doc" or ".docx" => "📝",
            ".xls" or ".xlsx" => "📊",
            ".zip" or ".rar" or ".7z" => "📦",
            ".txt" or ".md" => "📃",
            _ => "📄" // Default file icon
        };
    }

    /// <summary>
    /// Gets the visibility of the zip badge for zip archive folders
    /// </summary>
    [JsonIgnore]
    public Visibility ShowZipBadge
    {
        get
        {
            // Show Zip badge only for zip archives (directories with .zip URLs)
            if (IsDirectory && !IsCatalogEntry && Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }
    }

    public override string ToString()
    {
        var icon = GetIconWithoutBadge();

        // Show file count for catalog folders
        if (IsDirectory && !IsCatalogEntry && CatalogFileCount > 0)
        {
            var catalogInfo = $" ({CatalogFileCount} file{(CatalogFileCount != 1 ? "s" : "")})";
            return $"{icon} {Title}{catalogInfo}";
        }

        return $"{icon} {Title}";
    }

    public string GetIcon()
    {
        return GetIconWithoutBadge();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Category node wrapper for displaying in ComboBox.
/// </summary>
public class CategoryNode
{
    public string Name { get; set; } = string.Empty;
    public TreeViewNode Node { get; set; } = null!;

    public override string ToString() => Name;
}

/// <summary>
/// Data model for serializing/deserializing category to JSON.
/// </summary>
public class CategoryData
{
    public string Name { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PasswordProtectionType PasswordProtection { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OwnPasswordHash { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public SortOption SortOrder { get; set; }

    // Bookmark import metadata
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBookmarkImport { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BrowserType? SourceBrowserType { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceBrowserName { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceBookmarksPath { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastBookmarkImportDate { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImportedBookmarkCount { get; set; }
    
    // URL Bookmarks category restriction
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBookmarkCategory { get; set; }
    
    // Bookmark lookup availability
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBookmarkLookup { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkData>? Links { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CategoryData>? SubCategories { get; set; }
}

/// <summary>
/// Data model for serializing/deserializing link to JSON.
/// </summary>
public class LinkData
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDirectory { get; set; }

    public string CategoryPath { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FolderLinkType? FolderType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileFilters { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsCatalogEntry { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastCatalogUpdate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? FileSize { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutoRefreshCatalog { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsZipPasswordProtected { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public SortOption CatalogSortOrder { get; set; }

    /// <summary>
    /// URL accessibility status (for bookmark categories).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public UrlStatus UrlStatus { get; set; }

    /// <summary>
    /// Date and time when the URL status was last checked.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UrlLastChecked { get; set; }

    /// <summary>
    /// Detailed message from the last URL status check.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlStatusMessage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkData>? CatalogEntries { get; set; }
}

/// <summary>
/// Type of folder link.
/// </summary>
public enum FolderLinkType
{
    LinkOnly,
    CatalogueFiles,
    FilteredCatalogue
}

/// <summary>
/// Result from the Add Link dialog.
/// </summary>
public class AddLinkResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public TreeViewNode? CategoryNode { get; set; }
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
}

/// <summary>
/// Result from the Edit Link dialog.
/// </summary>
public class LinkEditResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
}

/// <summary>
/// Result from the Move Link dialog.
/// </summary>
public class MoveLinkResult
{
    public TreeViewNode? TargetCategoryNode { get; set; }
}

/// <summary>
/// Result from the Category Edit dialog.
/// </summary>
public class CategoryEditResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public PasswordProtectionType PasswordProtection { get; set; } = PasswordProtectionType.None;
    public string? OwnPassword { get; set; }
    public bool IsBookmarkCategory { get; set; } = false;
    public bool IsBookmarkLookup { get; set; } = false;
}

/// <summary>
/// Result from the Zip Folder dialog.
/// </summary>
public class ZipFolderResult
{
    public string ZipFileName { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public bool LinkToCategory { get; set; }
    public bool UsePassword { get; set; }
    public string? Password { get; set; }
}