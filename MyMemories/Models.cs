using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories;

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

    public override string ToString() => $"{Icon} {Name}";
}

/// <summary>
/// Represents a link item (file, directory, or URL) in the tree view.
/// </summary>
public class LinkItem : INotifyPropertyChanged
{
    private int _catalogFileCount;
    private DateTime? _lastCatalogUpdate;
    
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
    /// Gets the visibility of the change badge
    /// </summary>
    [JsonIgnore]
    public Visibility HasChanged
    {
        get
        {
            if (!IsDirectory || IsCatalogEntry || !LastCatalogUpdate.HasValue)
                return Visibility.Collapsed;

            if (!Directory.Exists(Url))
                return Visibility.Collapsed;

            try
            {
                var dirInfo = new DirectoryInfo(Url);
                
                // Method 1: Check if directory was modified after last catalog update
                if (dirInfo.LastWriteTime > LastCatalogUpdate.Value)
                {
                    return Visibility.Visible;
                }
                
                // Method 2: Compare actual file count with cataloged file count
                // This catches deletions that don't update LastWriteTime
                var currentFileCount = Directory.GetFiles(Url).Length;
                if (currentFileCount != CatalogFileCount)
                {
                    return Visibility.Visible;
                }
                
                return Visibility.Collapsed;
            }
            catch
            {
                return Visibility.Collapsed;
            }
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
        // For directories
        if (IsDirectory)
        {
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
}

/// <summary>
/// Result from the Zip Folder dialog.
/// </summary>
public class ZipFolderResult
{
    public string ZipFileName { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public bool LinkToCategory { get; set; }
}