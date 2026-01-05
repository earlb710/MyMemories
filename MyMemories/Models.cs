using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;

namespace MyMemories;

/// <summary>
/// Link item data.
/// </summary>
public class LinkItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
    public bool IsCatalogEntry { get; set; } = false; // Indicates if this is a catalog entry (read-only)
    public DateTime? LastCatalogUpdate { get; set; } // NEW: Timestamp when catalog was last updated

    /// <summary>
    /// Gets the appropriate icon for this link based on its type.
    /// </summary>
    public string GetIcon()
    {
        if (IsDirectory)
        {
            // Return different icons based on folder type
            return FolderType switch
            {
                FolderLinkType.LinkOnly => "🔗",           // Link icon for link only
                FolderLinkType.CatalogueFiles => "📂",     // Open folder for catalogue
                FolderLinkType.FilteredCatalogue => "🗂️",  // Card index for filtered catalogue
                _ => "📂"
            };
        }

        if (string.IsNullOrEmpty(Url))
        {
            return "🔗"; // Generic link icon
        }

        // Check if it's a URL
        if (Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
        {
            return "🌐"; // Web/URL icon
        }

        // Get file extension
        string extension = Path.GetExtension(Url).ToLowerInvariant();

        return extension switch
        {
            // Images
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".ico" or ".svg" or ".webp" => "🖼️",
            
            // Documents
            ".pdf" => "📕",
            ".doc" or ".docx" => "📘",
            ".xls" or ".xlsx" => "📗",
            ".ppt" or ".pptx" => "📙",
            
            // Text/Code
            ".txt" => "📝",
            ".md" or ".markdown" => "📋",
            ".cs" => "💻",
            ".xaml" or ".xml" => "🔖",
            ".json" => "📊",
            ".html" or ".htm" => "🌐",
            ".css" => "🎨",
            ".js" or ".ts" => "⚙️",
            
            // Archives
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            
            // Audio
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" => "🎵",
            
            // Video
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => "🎬",
            
            // Executables
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "⚡",
            ".dll" or ".so" => "🔧",
            
            // Data/Config
            ".ini" or ".config" or ".yaml" or ".yml" or ".toml" => "⚙️",
            ".log" => "📋",
            ".csv" => "📊",
            ".sql" or ".db" or ".sqlite" => "🗃️",
            
            // Default for unknown file types
            _ => "📄"
        };
    }

    /// <summary>
    /// Checks if the folder catalog is outdated (folder modified after last catalog update).
    /// </summary>
    public bool IsCatalogOutdated()
    {
        if (!IsDirectory || !Directory.Exists(Url) || LastCatalogUpdate == null)
        {
            return false;
        }

        try
        {
            var dirInfo = new DirectoryInfo(Url);
            return dirInfo.LastWriteTime > LastCatalogUpdate.Value;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString()
    {
        var icon = GetIcon();
        var title = Title;
        
        // Add red asterisk if catalog is outdated
        if (IsDirectory && IsCatalogOutdated())
        {
            title = $"{title} *";
        }
        
        return $"{icon} {title}";
    }
}

/// <summary>
/// Enum defining folder link types.
/// </summary>
public enum FolderLinkType
{
    LinkOnly,          // Just a link to the folder
    CatalogueFiles,    // Show all files in the folder
    FilteredCatalogue  // Show filtered files based on file filters
}

/// <summary>
/// Helper class for category node information.
/// </summary>
public class CategoryNode
{
    public string Name { get; set; } = string.Empty;
    public TreeViewNode? Node { get; set; }
}

/// <summary>
/// Link data for JSON serialization.
/// </summary>
public class LinkData
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public FolderLinkType FolderType { get; set; } = FolderLinkType.LinkOnly;
    public string FileFilters { get; set; } = string.Empty;
    public bool IsCatalogEntry { get; set; } = false; // Indicates if this is a catalog entry
    public DateTime? LastCatalogUpdate { get; set; } // NEW: Timestamp when catalog was last updated
}

/// <summary>
/// Result of link edit operation.
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
/// Result of add link operation.
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
/// Result of move link operation.
/// </summary>
public class MoveLinkResult
{
    public TreeViewNode? TargetCategoryNode { get; set; }
}

/// <summary>
/// Helper class to store category information.
/// </summary>
public class CategoryItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁"; // Default folder icon
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    public override string ToString() => $"{Icon} {Name}";
}

/// <summary>
/// Category data for JSON serialization (supports hierarchical subcategories).
/// </summary>
public class CategoryData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁"; // Default folder icon
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public List<LinkData> Links { get; set; } = new();
    public List<CategoryData> SubCategories { get; set; } = new();
}

/// <summary>
/// Result of category edit operation.
/// </summary>
public class CategoryEditResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
}