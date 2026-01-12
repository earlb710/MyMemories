using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyMemories.Services;

/// <summary>
/// Represents a single bookmark item from a browser import.
/// </summary>
public class BookmarkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; } = DateTime.Now;
    public DateTime DateModified { get; set; } = DateTime.Now;
}

/// <summary>
/// Chrome bookmarks file structure.
/// </summary>
public class ChromeBookmarkFile
{
    public string? checksum { get; set; }
    public ChromeBookmarkRoots? roots { get; set; }
    public int version { get; set; }
}

/// <summary>
/// Chrome bookmark roots structure.
/// </summary>
public class ChromeBookmarkRoots
{
    public ChromeBookmarkNode? bookmark_bar { get; set; }
    public ChromeBookmarkNode? other { get; set; }
    public ChromeBookmarkNode? synced { get; set; }
}

/// <summary>
/// Chrome bookmark node structure (can be folder or URL).
/// </summary>
public class ChromeBookmarkNode
{
    [JsonPropertyName("date_added")]
    public string? date_added_str { get; set; }
    
    [JsonPropertyName("date_modified")]
    public string? date_modified_str { get; set; }
    
    public string? id { get; set; }
    public string? name { get; set; }
    public string? type { get; set; }
    public string? url { get; set; }
    public List<ChromeBookmarkNode>? children { get; set; }
    
    /// <summary>
    /// Gets the date_added value as a long (Chrome uses microseconds since 1601).
    /// </summary>
    [JsonIgnore]
    public long? date_added_value
    {
        get
        {
            if (long.TryParse(date_added_str, out var value))
                return value;
            return null;
        }
    }
    
    /// <summary>
    /// Gets the date_modified value as a long (Chrome uses microseconds since 1601).
    /// </summary>
    [JsonIgnore]
    public long? date_modified_value
    {
        get
        {
            if (long.TryParse(date_modified_str, out var value))
                return value;
            return null;
        }
    }
}
