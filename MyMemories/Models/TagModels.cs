using System;
using System.Collections.Generic;

namespace MyMemories;

/// <summary>
/// Represents a user-defined tag for categorization.
/// </summary>
public class TagItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078D4";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public override string ToString() => Name;
}

/// <summary>
/// Container for storing all tags.
/// </summary>
public class TagCollection
{
    public List<TagItem> Tags { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.Now;
}
