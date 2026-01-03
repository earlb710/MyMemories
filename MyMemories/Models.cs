using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

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

    public override string ToString() => IsDirectory ? $"📂 {Title}" : Title;
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
/// Category data for JSON serialization.
/// </summary>
public class CategoryData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁"; // Default folder icon
    public List<LinkData> Links { get; set; } = new();
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