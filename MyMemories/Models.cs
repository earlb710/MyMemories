using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories;

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

    public override string ToString() => $"{Icon} {Name}";
}

/// <summary>
/// Represents a link item (file, directory, or URL) in the tree view.
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
    public bool IsCatalogEntry { get; set; }
    public DateTime? LastCatalogUpdate { get; set; }

    public override string ToString()
    {
        var icon = GetIcon();
        
        // Show asterisk if folder needs catalog update
        if (IsDirectory && !IsCatalogEntry && LastCatalogUpdate.HasValue)
        {
            var daysSinceUpdate = (DateTime.Now - LastCatalogUpdate.Value).TotalDays;
            if (daysSinceUpdate > 7) // Show asterisk if catalog is more than 7 days old
            {
                return $"{icon} {Title} *";
            }
        }
        
        return $"{icon} {Title}";
    }

    public string GetIcon()
    {
        if (IsCatalogEntry)
        {
            // Catalog entries use file extension icons
            var extension = Path.GetExtension(Url).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "🎵",
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".zip" or ".rar" or ".7z" => "📦",
                ".txt" => "📃",
                _ => "📄"
            };
        }
        
        if (IsDirectory)
            return "📁";
        
        if (Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !uri.IsFile)
            return "🌐";
        
        return "📄";
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
}