using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MyMemories.Services;

/// <summary>
/// Service for managing tags persistence.
/// </summary>
public class TagManagementService
{
    private const string TagsFileName = "tags.json";
    private const int MaxTags = 20;
    private const string TagIconGlyph = "\uE8EC"; // Tag icon from Segoe MDL2 Assets
    private const string TagEmojiGlyph = "\U0001F3F7"; // ??? Label/Tag emoji - used for text display
    
    private readonly string _tagsFilePath;
    private TagCollection _tagCollection;
    
    /// <summary>
    /// Static instance for global access (set during initialization).
    /// </summary>
    public static TagManagementService? Instance { get; private set; }

    public TagManagementService(string dataDirectory)
    {
        _tagsFilePath = Path.Combine(dataDirectory, TagsFileName);
        _tagCollection = new TagCollection();
        Instance = this;
    }

    /// <summary>
    /// Gets the maximum number of tags allowed.
    /// </summary>
    public static int MaxTagCount => MaxTags;

    /// <summary>
    /// Gets all tags.
    /// </summary>
    public IReadOnlyList<TagItem> Tags => _tagCollection.Tags.AsReadOnly();

    /// <summary>
    /// Gets the count of current tags.
    /// </summary>
    public int TagCount => _tagCollection.Tags.Count;

    /// <summary>
    /// Checks if the maximum tag limit has been reached.
    /// </summary>
    public bool IsAtMaxCapacity => _tagCollection.Tags.Count >= MaxTags;

    /// <summary>
    /// Loads tags from the file system.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_tagsFilePath))
            {
                var json = await File.ReadAllTextAsync(_tagsFilePath);
                var collection = JsonSerializer.Deserialize<TagCollection>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (collection != null)
                {
                    _tagCollection = collection;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading tags: {ex.Message}");
            _tagCollection = new TagCollection();
        }
    }

    /// <summary>
    /// Saves tags to the file system.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            _tagCollection.LastModified = DateTime.Now;

            var json = JsonSerializer.Serialize(_tagCollection, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_tagsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_tagsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving tags: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds a new tag.
    /// </summary>
    /// <returns>True if the tag was added, false if at max capacity.</returns>
    public bool AddTag(TagItem tag)
    {
        if (IsAtMaxCapacity)
            return false;

        tag.CreatedDate = DateTime.Now;
        tag.ModifiedDate = DateTime.Now;
        _tagCollection.Tags.Add(tag);
        return true;
    }

    /// <summary>
    /// Updates an existing tag.
    /// </summary>
    public bool UpdateTag(TagItem tag)
    {
        var existingIndex = _tagCollection.Tags.FindIndex(t => t.Id == tag.Id);
        if (existingIndex >= 0)
        {
            tag.ModifiedDate = DateTime.Now;
            tag.CreatedDate = _tagCollection.Tags[existingIndex].CreatedDate;
            _tagCollection.Tags[existingIndex] = tag;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a tag by ID.
    /// </summary>
    public bool RemoveTag(string tagId)
    {
        var tag = _tagCollection.Tags.Find(t => t.Id == tagId);
        if (tag != null)
        {
            _tagCollection.Tags.Remove(tag);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a tag by name or ID (for legacy support).
    /// First tries to match by name (case-insensitive), then by GUID ID.
    /// </summary>
    public TagItem? GetTag(string tagNameOrId)
    {
        if (string.IsNullOrEmpty(tagNameOrId))
            return null;
            
        // First try to find by name (case-insensitive)
        var tagByName = _tagCollection.Tags.Find(t => 
            string.Equals(t.Name, tagNameOrId, StringComparison.OrdinalIgnoreCase));
        if (tagByName != null)
            return tagByName;
            
        // Fallback: try to find by ID (legacy GUID support)
        return _tagCollection.Tags.Find(t => t.Id == tagNameOrId);
    }

    /// <summary>
    /// Gets a tag by name (case-insensitive).
    /// </summary>
    public TagItem? GetTagByName(string name)
    {
        return _tagCollection.Tags.Find(t => 
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a tag name already exists.
    /// </summary>
    public bool TagNameExists(string name, string? excludeId = null)
    {
        return _tagCollection.Tags.Exists(t => 
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || t.Id != excludeId));
    }

    /// <summary>
    /// Gets formatted display text for a list of tag names.
    /// Format: [tag icon] TagName  [tag icon] TagName2
    /// For use in tooltips and detail views.
    /// If a tag is not found, displays the raw name.
    /// </summary>
    public string GetTagDisplayText(IEnumerable<string> tagNames)
    {
        if (tagNames == null || !tagNames.Any())
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var tagName in tagNames)
        {
            var tag = GetTag(tagName);
            if (sb.Length > 0)
                sb.Append("  ");
            
            // Use tag name if found, otherwise show raw name
            var displayName = tag?.Name ?? tagName;
            sb.Append($"{TagIconGlyph} {displayName}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets tag icons only (no names) for tree node display.
    /// Returns one tag icon glyph per tag.
    /// Includes orphaned tags (those not found in definitions).
    /// </summary>
    public string GetTagIconsOnly(IEnumerable<string> tagNames)
    {
        if (tagNames == null || !tagNames.Any())
            return string.Empty;

        // Count all tags (including orphaned ones)
        int count = tagNames.Count();

        if (count == 0)
            return string.Empty;

        // Return tag icons (one per tag, up to 3, then show count)
        if (count <= 3)
        {
            return string.Join("", Enumerable.Repeat(TagIconGlyph, count));
        }
        else
        {
            // Show 3 icons + count for many tags
            return $"{TagIconGlyph}{TagIconGlyph}{TagIconGlyph}+{count - 3}";
        }
    }

    /// <summary>
    /// Gets tag information for display (name and color) for a list of tag names.
    /// Returns a list of tuples with (Name, Color) for each tag.
    /// Orphaned tags use a default gray color.
    /// </summary>
    public List<(string Name, string Color)> GetTagsInfo(IEnumerable<string> tagNames)
    {
        var result = new List<(string Name, string Color)>();
        if (tagNames == null)
            return result;

        foreach (var tagName in tagNames)
        {
            var tag = GetTag(tagName);
            if (tag != null)
            {
                result.Add((tag.Name, tag.Color));
            }
            else
            {
                // Orphaned tag - use raw name and gray color
                result.Add((tagName, "#808080"));
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a styled StackPanel with tag badges for display in UI.
    /// Each tag shows: [tag icon] TagName with tag color background and white text.
    /// Orphaned tags are displayed with gray background.
    /// </summary>
    public StackPanel CreateTagBadgesPanel(IEnumerable<string> tagNames, double fontSize = 11, double spacing = 6)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing
        };

        if (tagNames == null)
            return panel;

        foreach (var tagName in tagNames)
        {
            var tag = GetTag(tagName);
            var badge = CreateTagBadgeWithFallback(tag, tagName, fontSize);
            panel.Children.Add(badge);
        }

        return panel;
    }

    /// <summary>
    /// Creates a tag badge, using the raw tag name if tag definition is not found.
    /// </summary>
    private Border CreateTagBadgeWithFallback(TagItem? tag, string tagName, double fontSize = 11)
    {
        var displayName = tag?.Name ?? tagName;
        var backgroundColor = tag != null ? ParseColor(tag.Color) : Colors.Gray;
        
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        contentPanel.Children.Add(new FontIcon
        {
            Glyph = TagIconGlyph,
            FontSize = fontSize,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = new SolidColorBrush(Colors.White)
        });

        contentPanel.Children.Add(new TextBlock
        {
            Text = displayName,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = contentPanel
        };
    }

    /// <summary>
    /// Creates a single tag badge with icon, name, colored background, and white text.
    /// </summary>
    public Border CreateTagBadge(TagItem tag, double fontSize = 11)
    {
        return CreateTagBadgeWithFallback(tag, tag.Name, fontSize);
    }

    /// <summary>
    /// Creates a tag badge from a tag name or ID.
    /// </summary>
    public Border? CreateTagBadgeById(string tagNameOrId, double fontSize = 11)
    {
        var tag = GetTag(tagNameOrId);
        return CreateTagBadgeWithFallback(tag, tagNameOrId, fontSize);
    }

    /// <summary>
    /// Creates a styled StackPanel with small colored tag icon badges for tree node display.
    /// Each tag shows only the icon with the tag's background color.
    /// Limited to 3 visible tags, with a "+N" indicator for additional tags.
    /// Orphaned tags are shown with gray background.
    /// </summary>
    public StackPanel CreateTagIconsPanel(IEnumerable<string> tagNames, double iconSize = 10, double spacing = 2)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (tagNames == null)
            return panel;

        var tagNamesList = tagNames.ToList();
        if (tagNamesList.Count == 0)
            return panel;

        // Show up to 3 tag icons
        int displayCount = Math.Min(tagNamesList.Count, 3);
        for (int i = 0; i < displayCount; i++)
        {
            var tagName = tagNamesList[i];
            var tag = GetTag(tagName);
            var badge = CreateTagIconBadgeWithFallback(tag, tagName, iconSize);
            panel.Children.Add(badge);
        }

        // Show "+N" for additional tags
        if (tagNamesList.Count > 3)
        {
            var moreText = new TextBlock
            {
                Text = $"+{tagNamesList.Count - 3}",
                FontSize = iconSize,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(2, 0, 0, 0)
            };
            panel.Children.Add(moreText);
        }

        return panel;
    }

    /// <summary>
    /// Creates a tag icon badge, using gray for orphaned tags.
    /// </summary>
    private Border CreateTagIconBadgeWithFallback(TagItem? tag, string tagName, double iconSize = 10)
    {
        var displayName = tag?.Name ?? tagName;
        var backgroundColor = tag != null ? ParseColor(tag.Color) : Colors.Gray;

        var icon = new FontIcon
        {
            Glyph = TagIconGlyph,
            FontSize = iconSize,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = new SolidColorBrush(Colors.White)
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(3, 1, 3, 1),
            Child = icon,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Add tooltip with tag name
        ToolTipService.SetToolTip(badge, displayName);

        return badge;
    }

    /// <summary>
    /// Creates a single small tag icon badge with colored background for tree node display.
    /// </summary>
    public Border CreateTagIconBadge(TagItem tag, double iconSize = 10)
    {
        return CreateTagIconBadgeWithFallback(tag, tag.Name, iconSize);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return Color.FromArgb(
                    255,
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
        }
        catch { }
        
        return Colors.DodgerBlue;
    }
}
