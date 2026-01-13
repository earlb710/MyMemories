using System;
using System.Collections.Generic;

namespace MyMemories.Models;

/// <summary>
/// Root object for category import file.
/// </summary>
public class CategoryImportData
{
    public string Version { get; set; } = "1.0";
    public string? Description { get; set; }
    public DateTime? ImportDate { get; set; }
    public List<ImportOperation> Operations { get; set; } = new();
}

/// <summary>
/// Represents a single import operation.
/// </summary>
public class ImportOperation
{
    public string Operation { get; set; } = "Add"; // Add, Update, Delete
    public string Target { get; set; } = "Link"; // Category, SubCategory, Link, Tag, Rating
    public ImportIdentifier? Identifier { get; set; }
    public object? Data { get; set; }
    public ImportOptions? Options { get; set; }
}

/// <summary>
/// Identifies the target of an operation.
/// </summary>
public class ImportIdentifier
{
    public string? Name { get; set; }
    public string? CategoryPath { get; set; }
    public string? ParentCategoryPath { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Options for import operations.
/// </summary>
public class ImportOptions
{
    // Add options
    public bool? MergeIfExists { get; set; }
    public bool? UpdateExisting { get; set; }
    public bool? SkipIfExists { get; set; }
    public bool? UpdateIfExists { get; set; }
    
    // Update options
    public bool? MergeTagIds { get; set; }
    public bool? MergeRatings { get; set; }
    public bool? PreserveTimestamps { get; set; }
    public bool? UpdateUrlStatus { get; set; }
    
    // Delete options
    public bool? Recursive { get; set; }
    public bool? BackupBeforeDelete { get; set; }
    public bool? DeleteCatalogEntries { get; set; }
    
    // Tag options
    public bool? CreateTagsIfMissing { get; set; }
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportOperationResult
{
    public string Operation { get; set; } = "";
    public string Target { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Success, Failed, Skipped
    public string Message { get; set; } = "";
    public ImportIdentifier? Identifier { get; set; }
}

/// <summary>
/// Overall result of an import batch.
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int TotalOperations { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<ImportOperationResult> OperationResults { get; set; } = new();
    public TimeSpan ImportDuration { get; set; }
    public List<string> CategoriesModified { get; set; } = new();
}

/// <summary>
/// Data structure for category add/update operations.
/// </summary>
public class ImportCategoryData
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Keywords { get; set; }
    public List<string>? TagIds { get; set; }
    public SortOption? SortOrder { get; set; }
    public bool? IsBookmarkCategory { get; set; }
    public bool? IsBookmarkLookup { get; set; }
    public bool? IsAuditLoggingEnabled { get; set; }
    public PasswordProtectionType? PasswordProtection { get; set; }
}

/// <summary>
/// Data structure for link add/update operations.
/// </summary>
public class ImportLinkData
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public List<string>? TagIds { get; set; }
    public bool? IsDirectory { get; set; }
    public FolderLinkType? FolderType { get; set; }
    public string? FileFilters { get; set; }
    public bool? AutoRefreshCatalog { get; set; }
}

/// <summary>
/// Data structure for tag operations.
/// </summary>
public class ImportTagData
{
    public List<string> TagIds { get; set; } = new();
}

/// <summary>
/// Data structure for rating operations.
/// </summary>
public class ImportRatingData
{
    public List<RatingValue>? Ratings { get; set; }
    public List<string>? RatingNames { get; set; }
}

/// <summary>
/// Import rating format from JSON (uses Name/Value instead of Rating/Score).
/// </summary>
public class ImportRatingEntry
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Rating data with import format.
/// </summary>
public class ImportRatingDataWithEntries
{
    public List<ImportRatingEntry>? Ratings { get; set; }
}
