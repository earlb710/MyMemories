using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace MyMemories;

/// <summary>
/// Represents a category item in the tree view.
/// </summary>
public class CategoryItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "??";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public PasswordProtectionType PasswordProtection { get; set; } = PasswordProtectionType.None;
    public string? OwnPasswordHash { get; set; }
    public SortOption SortOrder { get; set; } = SortOption.NameAscending;
    
    /// <summary>
    /// Keywords for searching and categorization. Comma or semicolon separated.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;
    
    /// <summary>
    /// List of tag IDs assigned to this category.
    /// </summary>
    public List<string> TagIds { get; set; } = new();
    
    /// <summary>
    /// Gets formatted display text showing all tags with their names.
    /// </summary>
    [JsonIgnore]
    public string TagDisplayText => Services.TagManagementService.Instance?.GetTagDisplayText(TagIds) ?? string.Empty;
    
    /// <summary>
    /// Gets whether this category has any tags assigned.
    /// </summary>
    [JsonIgnore]
    public Visibility HasTags => TagIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    /// <summary>
    /// Gets a tag indicator string showing number of tags.
    /// </summary>
    [JsonIgnore]
    public string TagIndicator => TagIds.Count > 0 ? $"[{TagIds.Count} tag{(TagIds.Count > 1 ? "s" : "")}]" : string.Empty;
    
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
    
    // Audit logging - enables per-category audit logging
    public bool IsAuditLoggingEnabled { get; set; } = false;

    public override string ToString() => $"{Icon} {Name}";
    
    /// <summary>
    /// Notifies UI that tag-related properties have changed.
    /// </summary>
    public void NotifyTagsChanged()
    {
        // CategoryItem doesn't implement INotifyPropertyChanged yet, 
        // but this method is needed for consistency with the API
    }
}
