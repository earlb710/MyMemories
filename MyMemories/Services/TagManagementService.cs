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
    private const string TagIconGlyph = "\U0001F3F7"; // ??? Label/Tag emoji
    
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
    /// Gets a tag by ID.
    /// </summary>
    public TagItem? GetTag(string tagId)
    {
        return _tagCollection.Tags.Find(t => t.Id == tagId);
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
    /// Gets formatted display text for a list of tag IDs.
    /// Format: [tag icon] TagName  [tag icon] TagName2
    /// For use in tooltips and detail views.
    /// </summary>
    public string GetTagDisplayText(IEnumerable<string> tagIds)
    {
        if (tagIds == null || !tagIds.Any())
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var tagId in tagIds)
        {
            var tag = GetTag(tagId);
            if (tag != null)
            {
                if (sb.Length > 0)
                    sb.Append("  ");
                sb.Append($"{TagIconGlyph} {tag.Name}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets tag icons only (no names) for tree node display.
    /// Returns one tag icon glyph per tag.
    /// </summary>
    public string GetTagIconsOnly(IEnumerable<string> tagIds)
    {
        if (tagIds == null || !tagIds.Any())
            return string.Empty;

        int count = 0;
        foreach (var tagId in tagIds)
        {
            if (GetTag(tagId) != null)
            {
                count++;
            }
        }

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
    /// Gets tag information for display (name and color) for a list of tag IDs.
    /// Returns a list of tuples with (Name, Color) for each valid tag.
    /// </summary>
    public List<(string Name, string Color)> GetTagsInfo(IEnumerable<string> tagIds)
    {
        var result = new List<(string Name, string Color)>();
        if (tagIds == null)
            return result;

        foreach (var tagId in tagIds)
        {
            var tag = GetTag(tagId);
            if (tag != null)
            {
                result.Add((tag.Name, tag.Color));
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a styled StackPanel with tag badges for display in UI.
    /// Each tag shows: [tag icon] TagName with tag color background and white text.
    /// </summary>
    public StackPanel CreateTagBadgesPanel(IEnumerable<string> tagIds, double fontSize = 11, double spacing = 6)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing
        };

        if (tagIds == null)
            return panel;

        foreach (var tagId in tagIds)
        {
            var tag = GetTag(tagId);
            if (tag != null)
            {
                var badge = CreateTagBadge(tag, fontSize);
                panel.Children.Add(badge);
            }
        }

        return panel;
    }

    /// <summary>
    /// Creates a single tag badge with icon, name, colored background, and white text.
    /// </summary>
    public Border CreateTagBadge(TagItem tag, double fontSize = 11)
    {
        var backgroundColor = ParseColor(tag.Color);
        
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        contentPanel.Children.Add(new TextBlock
        {
            Text = TagIconGlyph,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        });

        contentPanel.Children.Add(new TextBlock
        {
            Text = tag.Name,
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
    /// Creates a tag badge from a tag ID.
    /// </summary>
    public Border? CreateTagBadgeById(string tagId, double fontSize = 11)
    {
        var tag = GetTag(tagId);
        return tag != null ? CreateTagBadge(tag, fontSize) : null;
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
