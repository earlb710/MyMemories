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

    /// <summary>
    /// Gets the appropriate icon for this link based on its type.
    /// </summary>
    public string GetIcon()
    {
        if (IsDirectory)
        {
            return "📂"; // Folder icon for directories
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

    public override string ToString() => $"{GetIcon()} {Title}";
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
/// Result of link edit operation.
/// </summary>
public class LinkEditResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
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
    public List<LinkData> Links { get; set; } = new();
    public List<CategoryData> SubCategories { get; set; } = new();
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