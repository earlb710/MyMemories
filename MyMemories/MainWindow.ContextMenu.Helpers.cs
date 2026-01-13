using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using MyMemories.Services;

namespace MyMemories;

/// <summary>
/// Helper methods and caching infrastructure for context menu operations.
/// </summary>
public sealed partial class MainWindow
{
    // ========================================
    // CACHING FIELDS FOR PERFORMANCE
    // ========================================
    
    // Menu item caches (eliminates 10-15 lookups per menu display)
    private readonly Dictionary<string, MenuFlyoutItem> _categoryMenuItemCache = new();
    private readonly Dictionary<string, MenuFlyoutItem> _linkMenuItemCache = new();
    private readonly Dictionary<string, MenuFlyoutSubItem> _categorySubMenuCache = new();
    private readonly Dictionary<string, MenuFlyoutSubItem> _linkSubMenuCache = new();
    
    // Service instance reuse (reduces allocations)
    private readonly CategoryStatisticsService _statisticsService = new();
    
    // Rating template cache (prevents expensive template switching)
    private List<(string Name, string DisplayName, int DefinitionCount)>? _cachedRatingTemplates;
    private DateTime _ratingTemplateCacheTime;
    private const int RATING_CACHE_MINUTES = 5;

    // ========================================
    // OPTIMIZED MENU ITEM LOOKUPS
    // ========================================
    
    /// <summary>
    /// Finds a menu item by name with caching for performance.
    /// </summary>
    private MenuFlyoutItem? FindMenuItemByName(MenuFlyout menu, string name, Dictionary<string, MenuFlyoutItem> cache)
    {
        // Check cache first (O(1) lookup)
        if (cache.TryGetValue(name, out var cachedItem))
            return cachedItem;

        // Linear search only on cache miss
        foreach (var item in menu.Items)
        {
            if (item is MenuFlyoutItem menuItem && menuItem.Name == name)
            {
                cache[name] = menuItem;
                return menuItem;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a submenu item by name with caching for performance.
    /// </summary>
    private MenuFlyoutSubItem? FindSubMenuItemByName(MenuFlyout menu, string name, Dictionary<string, MenuFlyoutSubItem> cache)
    {
        // Check cache first (O(1) lookup)
        if (cache.TryGetValue(name, out var cachedItem))
            return cachedItem;

        // Linear search only on cache miss
        foreach (var item in menu.Items)
        {
            if (item is MenuFlyoutSubItem subItem && subItem.Name == name)
            {
                cache[name] = subItem;
                return subItem;
            }
        }
        return null;
    }

    // ========================================
    // FAST DIRECTORY CHECK (NO I/O)
    // ========================================
    
    /// <summary>
    /// Fast check for directory links without blocking I/O.
    /// Recursively searches node hierarchy in memory only.
    /// </summary>
    private bool HasDirectoryLinksRecursive(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            // Check if this child is a non-catalog directory link
            if (child.Content is LinkItem link && link.IsDirectory && !link.IsCatalogEntry)
                return true;
            
            // Recursively check subcategories
            if (child.Content is CategoryItem && HasDirectoryLinksRecursive(child))
                return true;
        }
        return false;
    }

    // ========================================
    // RATING TEMPLATE CACHING
    // ========================================
    
    /// <summary>
    /// Gets rating templates with caching to avoid expensive template switching.
    /// Cache expires after 5 minutes or when templates are modified.
    /// </summary>
    private List<(string Name, string DisplayName, int DefinitionCount)> GetRatingTemplates()
    {
        if (_ratingService == null)
            return new List<(string Name, string DisplayName, int DefinitionCount)>();

        // Check if cache is valid
        var cacheExpired = _cachedRatingTemplates == null || 
                          (DateTime.Now - _ratingTemplateCacheTime).TotalMinutes > RATING_CACHE_MINUTES;

        if (!cacheExpired)
            return _cachedRatingTemplates!;

        // Build template list
        var templates = new List<(string Name, string DisplayName, int DefinitionCount)>();
        var templateNames = _ratingService.GetTemplateNames();
        var originalTemplate = _ratingService.CurrentTemplateName;
        
        foreach (var templateName in templateNames)
        {
            _ratingService.SwitchTemplate(templateName);
            var definitionCount = _ratingService.DefinitionCount;

            if (definitionCount > 0)
            {
                var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
                templates.Add((templateName, displayName, definitionCount));
            }
        }
        
        _ratingService.SwitchTemplate(originalTemplate);
        
        // Update cache
        _cachedRatingTemplates = templates;
        _ratingTemplateCacheTime = DateTime.Now;
        
        return templates;
    }

    /// <summary>
    /// Invalidates the rating template cache.
    /// Call this when templates are modified.
    /// </summary>
    private void InvalidateRatingTemplateCache()
    {
        _cachedRatingTemplates = null;
    }

    // ========================================
    // OPTIMIZED TAG FILTERING
    // ========================================
    
    /// <summary>
    /// Gets available tags using O(n) HashSet filtering instead of O(n×m) nested loops.
    /// </summary>
    private List<TagItem> GetAvailableTags(List<string> existingTags)
    {
        if (_tagService == null || _tagService.TagCount == 0)
            return new List<TagItem>();

        // Build HashSet for O(1) lookups
        var existingTagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Add both names and IDs to the set
        foreach (var existingTag in existingTags)
        {
            existingTagSet.Add(existingTag);
        }
        
        // Also add IDs of tags that match existing tags
        foreach (var tag in _tagService.Tags)
        {
            if (existingTags.Contains(tag.Id))
            {
                existingTagSet.Add(tag.Name);
            }
        }

        // Filter using O(n) complexity instead of O(n×m)
        return _tagService.Tags
            .Where(t => !existingTagSet.Contains(t.Name) && !existingTagSet.Contains(t.Id))
            .ToList();
    }

    // ========================================
    // COMMON DIALOG PATTERNS
    // ========================================
    
    /// <summary>
    /// Shows a confirmation dialog and returns true if user confirmed.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        var confirmDialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        
        var result = await confirmDialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    // ========================================
    // HELPER UTILITIES
    // ========================================
    
    /// <summary>
    /// Adds a disabled menu item to a submenu.
    /// </summary>
    private void AddDisabledMenuItem(MenuFlyoutSubItem subMenu, string text)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            IsEnabled = false
        };
        subMenu.Items.Add(item);
    }

    /// <summary>
    /// Finds the parent node of a given tree view node.
    /// </summary>
    private TreeViewNode? FindParentNode(TreeViewNode childNode)
    {
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            var parent = FindParentNodeRecursive(rootNode, childNode);
            if (parent != null)
                return parent;
        }
        return null;
    }

    /// <summary>
    /// Recursively searches for the parent of a given node.
    /// </summary>
    private TreeViewNode? FindParentNodeRecursive(TreeViewNode currentNode, TreeViewNode targetChild)
    {
        foreach (var child in currentNode.Children)
        {
            if (child == targetChild)
                return currentNode;

            var found = FindParentNodeRecursive(child, targetChild);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Generates a unique title by appending a sequence number if needed.
    /// </summary>
    private string GenerateUniqueTitle(string baseTitle, TreeViewNode parentNode)
    {
        // Check if base title already ends with a number in parentheses like " (2)"
        var match = System.Text.RegularExpressions.Regex.Match(baseTitle, @"^(.+?)\s*\((\d+)\)$");
        string coreName;
        int startSequence;

        if (match.Success)
        {
            coreName = match.Groups[1].Value.Trim();
            startSequence = int.Parse(match.Groups[2].Value) + 1;
        }
        else
        {
            coreName = baseTitle;
            startSequence = 2;
        }

        // Collect existing titles in the parent using HashSet for O(1) lookups
        var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in parentNode.Children)
        {
            if (child.Content is LinkItem childLink)
            {
                existingTitles.Add(childLink.Title);
            }
        }

        // Find a unique title
        var newTitle = $"{coreName} ({startSequence})";
        while (existingTitles.Contains(newTitle))
        {
            startSequence++;
            newTitle = $"{coreName} ({startSequence})";
        }

        return newTitle;
    }

    /// <summary>
    /// Recursively collects all folder paths from a category node.
    /// </summary>
    private List<string> CollectFolderPathsFromCategory(TreeViewNode categoryNode)
    {
        return _statisticsService.CollectFolderPathsFromCategory(categoryNode);
    }
}
