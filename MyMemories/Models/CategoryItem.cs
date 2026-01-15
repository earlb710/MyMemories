using System;
using System.Collections.Generic;
using System.Linq;
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
    public string Icon { get; set; } = "\U0001F4C1"; // ?? Folder
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
    /// List of tag names assigned to this category.
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Legacy property for backwards compatibility. Maps to Tags property.
    /// </summary>
    [JsonIgnore]
    public List<string> TagIds
    {
        get => Tags;
        set => Tags = value;
    }
    
    /// <summary>
    /// List of ratings assigned to this category.
    /// </summary>
    public List<RatingValue> Ratings { get; set; } = new();
    
    /// <summary>
    /// Gets formatted display text showing all tags with their names.
    /// Format: [tag icon] TagName  [tag icon] TagName2
    /// </summary>
    [JsonIgnore]
    public string TagDisplayText => Services.TagManagementService.Instance?.GetTagDisplayText(Tags) ?? string.Empty;
    
    /// <summary>
    /// Gets whether this category has any tags assigned.
    /// </summary>
    [JsonIgnore]
    public Visibility HasTags => Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    /// <summary>
    /// Gets tag icons only for tree node display (no names).
    /// </summary>
    [JsonIgnore]
    public string TagIndicator => Services.TagManagementService.Instance?.GetTagIconsOnly(Tags) ?? string.Empty;
    
    /// <summary>
    /// Gets whether this category has any ratings assigned.
    /// </summary>
    [JsonIgnore]
    public Visibility HasRatings => Ratings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    /// <summary>
    /// Gets the average rating score for this category.
    /// </summary>
    [JsonIgnore]
    public double AverageRating => Ratings.Count > 0 ? Ratings.Average(r => r.Score) : 0;
    
    /// <summary>
    /// Gets formatted display text showing all ratings.
    /// </summary>
    [JsonIgnore]
    public string RatingDisplayText => Services.RatingManagementService.Instance?.GetRatingsDisplayText(Ratings) ?? string.Empty;
    
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
    
    // Archive metadata
    /// <summary>
    /// Date when this category was archived (soft deleted).
    /// </summary>
    public DateTime? ArchivedDate { get; set; }
    
    /// <summary>
    /// Original parent path before archiving (for restoration).
    /// </summary>
    public string? OriginalParentPath { get; set; }
    
    /// <summary>
    /// Special flag indicating this is the Archive system node.
    /// </summary>
    [JsonIgnore]
    public bool IsArchiveNode { get; set; }
    
    /// <summary>
    /// Links within this category (used for archive serialization).
    /// </summary>
    public List<LinkItem>? Links { get; set; }
    
    // Export/Sync metadata
    /// <summary>
    /// Last date this category was exported to a browser.
    /// </summary>
    public DateTime? LastExportDate { get; set; }
    
    /// <summary>
    /// The folder name used when exporting to browser (e.g., "MyMemories").
    /// </summary>
    public string? ExportFolderName { get; set; }
    
    /// <summary>
    /// The browser type this category was last exported to.
    /// </summary>
    public BrowserType? ExportedToBrowserType { get; set; }
    
    /// <summary>
    /// The path to the browser's bookmarks file for sync.
    /// </summary>
    public string? ExportedToBookmarksPath { get; set; }
    
    // Backup configuration
    /// <summary>
    /// List of directories to automatically copy the category file to on save.
    /// Only applies to root categories.
    /// </summary>
    public List<string> BackupDirectories { get; set; } = new();
    
    /// <summary>
    /// Gets whether this category has backup directories configured.
    /// </summary>
    [JsonIgnore]
    public bool HasBackupDirectories => BackupDirectories.Count > 0;

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
