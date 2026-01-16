using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Archive functionality - soft delete and restore for categories and links.
/// </summary>
public sealed partial class MainWindow
{
    private TreeViewNode? _archiveNode;
    private const string ArchiveFileName = "Archive.json";
    
    /// <summary>
    /// Gets or creates the Archive node in the tree.
    /// </summary>
    private TreeViewNode GetOrCreateArchiveNode()
    {
        if (_archiveNode != null)
            return _archiveNode;
        
        // Find existing archive node
        foreach (var node in LinksTreeView.RootNodes)
        {
            if (node.Content is CategoryItem cat && cat.IsArchiveNode)
            {
                _archiveNode = node;
                return _archiveNode;
            }
        }
        
        // Should not happen as Archive is created in LoadAllCategoriesAsync
        return LinksTreeView.RootNodes[^1]; // Return last node (should be Archive)
    }
    
    /// <summary>
    /// Updates the Archive node display name to show item count.
    /// </summary>
    private void UpdateArchiveNodeName()
    {
        try
        {
            var archiveNode = GetOrCreateArchiveNode();
            if (archiveNode?.Content is CategoryItem category)
            {
                int count = archiveNode.Children.Count;
                var newName = $"Archived ({count})";
                
                // Only update if name changed
                if (category.Name != newName)
                {
                    category.Name = newName;
                    
                    // Force TreeView to update by creating a new CategoryItem instance
                    // This avoids issues with node refresh and index out of range errors
                    var updatedCategory = new CategoryItem
                    {
                        Name = newName,
                        Description = category.Description,
                        Icon = category.Icon,
                        IsArchiveNode = true
                    };
                    
                    archiveNode.Content = updatedCategory;
                    _archiveNode = archiveNode; // Update reference
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Error updating archive name: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Loads archived items from JSON file.
    /// </summary>
    private async Task LoadArchiveFromJsonAsync()
    {
        try
        {
            var archivePath = Path.Combine(_dataFolder, ArchiveFileName);
            if (!File.Exists(archivePath))
            {
                UpdateArchiveNodeName();
                return;
            }
            
            var json = await File.ReadAllTextAsync(archivePath);
            var archive = JsonSerializer.Deserialize<ArchiveData>(json);
            
            if (archive == null)
            {
                UpdateArchiveNodeName();
                return;
            }
            
            var archiveNode = GetOrCreateArchiveNode();
            
            // Load archived categories
            foreach (var category in archive.ArchivedCategories ?? [])
            {
                var categoryNode = new TreeViewNode { Content = category };
                
                // Load child links
                if (category.Links != null)
                {
                    foreach (var link in category.Links)
                    {
                        categoryNode.Children.Add(new TreeViewNode { Content = link });
                    }
                }
                
                archiveNode.Children.Add(categoryNode);
            }
            
            // Load archived links (not in categories)
            foreach (var link in archive.ArchivedLinks ?? [])
            {
                archiveNode.Children.Add(new TreeViewNode { Content = link });
            }
            
            UpdateArchiveNodeName();
            System.Diagnostics.Debug.WriteLine($"[Archive] Loaded {archive.ArchivedCategories?.Count ?? 0} categories and {archive.ArchivedLinks?.Count ?? 0} links from archive");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Error loading archive: {ex.Message}");
            UpdateArchiveNodeName();
        }
    }
    
    /// <summary>
    /// Saves archived items to JSON file.
    /// </summary>
    private async Task SaveArchiveToJsonAsync()
    {
        try
        {
            var archiveNode = GetOrCreateArchiveNode();
            if (archiveNode == null)
            {
                System.Diagnostics.Debug.WriteLine("[Archive] Archive node not found, skipping save");
                return;
            }
            
            var archivedCategories = new List<CategoryItem>();
            var archivedLinks = new List<LinkItem>();
            
            // Use ToList() to avoid collection modification issues
            var children = archiveNode.Children.ToList();
            
            foreach (var node in children)
            {
                try
                {
                    if (node?.Content is CategoryItem category)
                    {
                        // Collect child links
                        var links = new List<LinkItem>();
                        var childNodes = node.Children.ToList();
                        
                        foreach (var childNode in childNodes)
                        {
                            if (childNode?.Content is LinkItem childLink)
                            {
                                links.Add(childLink);
                            }
                        }
                        
                        category.Links = links.Count > 0 ? links : null;
                        archivedCategories.Add(category);
                    }
                    else if (node?.Content is LinkItem link)
                    {
                        archivedLinks.Add(link);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] Error processing node: {ex.Message}");
                }
            }
            
            var archive = new ArchiveData
            {
                ArchivedCategories = archivedCategories.Count > 0 ? archivedCategories : null,
                ArchivedLinks = archivedLinks.Count > 0 ? archivedLinks : null,
                LastModified = DateTime.Now
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var json = JsonSerializer.Serialize(archive, options);
            var archivePath = Path.Combine(_dataFolder, ArchiveFileName);
            await File.WriteAllTextAsync(archivePath, json);
            
            System.Diagnostics.Debug.WriteLine($"[Archive] Saved {archivedCategories.Count} categories and {archivedLinks.Count} links to archive");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Error saving archive: {ex.Message}");
            StatusText.Text = $"Warning: Could not save archive - {ex.Message}";
        }
    }
    
    /// <summary>
    /// Archives a category (soft delete).
    /// </summary>
    private async Task ArchiveCategoryAsync(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem category)
            return;
        
        // Get the archive node
        var archiveNode = GetOrCreateArchiveNode();
        
        // Store original parent info in metadata
        category.ArchivedDate = DateTime.Now;
        category.OriginalParentPath = categoryNode.Parent != null 
            ? _treeViewService!.GetCategoryPath(categoryNode.Parent)
            : "Root";
        
        // Remove from current location
        if (categoryNode.Parent != null)
        {
            categoryNode.Parent.Children.Remove(categoryNode);
        }
        else
        {
            LinksTreeView.RootNodes.Remove(categoryNode);
        }
        
        // Add to archive (DON'T auto-expand)
        archiveNode.Children.Add(categoryNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        // If this was a root category, delete its JSON file
        bool wasRootCategory = string.IsNullOrEmpty(category.OriginalParentPath) || 
                                category.OriginalParentPath == "Root";
        if (wasRootCategory)
        {
            try
            {
                await _categoryService!.DeleteCategoryAsync(category.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting category file: {ex.Message}");
            }
        }
        else
        {
            // Subcategory - just save the parent
            var rootNode = GetRootCategoryNode(archiveNode);
            if (rootNode != null)
            {
                await _categoryService!.SaveCategoryAsync(rootNode);
            }
        }
        
        StatusText.Text = $"Archived category '{category.Name}' - Use context menu to restore or delete permanently";
    }
    
    /// <summary>
    /// Archives a link (soft delete).
    /// </summary>
    private async Task ArchiveLinkAsync(TreeViewNode linkNode)
    {
        if (linkNode.Content is not LinkItem link)
            return;
        
        // Get the archive node
        var archiveNode = GetOrCreateArchiveNode();
        
        // Store original parent info in metadata
        link.ArchivedDate = DateTime.Now;
        link.OriginalCategoryPath = link.CategoryPath;
        
        // Remove from current location
        if (linkNode.Parent != null)
        {
            var oldParent = linkNode.Parent;
            oldParent.Children.Remove(linkNode);
            
            // Save the parent category
            await UpdateParentCategoriesAndSaveAsync(oldParent);
        }
        
        // Add to archive (DON'T auto-expand)
        archiveNode.Children.Add(linkNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Archived link '{link.Title}' - Use context menu to restore or delete permanently";
        
        if (LinksTreeView.SelectedNode == linkNode)
        {
            ShowWelcome();
        }
    }
    
    /// <summary>
    /// Restores an archived category to its original location.
    /// </summary>
    private async Task RestoreCategoryAsync(TreeViewNode archivedNode)
    {
        if (archivedNode.Content is not CategoryItem category)
            return;
        
        if (string.IsNullOrEmpty(category.OriginalParentPath))
        {
            await ShowErrorDialogAsync(
                "Cannot Restore",
                "Cannot determine original location for this category.");
            return;
        }
        
        // Remove from archive
        archivedNode.Parent?.Children.Remove(archivedNode);
        
        // Clear archive metadata
        var originalPath = category.OriginalParentPath;
        category.ArchivedDate = null;
        category.OriginalParentPath = null;
        category.ModifiedDate = DateTime.Now;
        
        // Restore to original location
        if (originalPath == "Root")
        {
            // Restore as root category
            _treeViewService!.InsertCategoryNode(archivedNode);
            await _categoryService!.SaveCategoryAsync(archivedNode);
        }
        else
        {
            // Find parent category and restore there
            TreeViewNode? parentNode = FindCategoryByPath(originalPath);
            if (parentNode != null)
            {
                _treeViewService!.InsertSubCategoryNode(parentNode, archivedNode);
                var rootNode = GetRootCategoryNode(parentNode);
                if (rootNode != null)
                {
                    await _categoryService!.SaveCategoryAsync(rootNode);
                }
                StatusText.Text = $"Restored category '{category.Name}' to '{originalPath}'";
            }
            else
            {
                // Parent not found, restore to root
                _treeViewService!.InsertCategoryNode(archivedNode);
                await _categoryService!.SaveCategoryAsync(archivedNode);
                StatusText.Text = $"Restored category '{category.Name}' to root (original parent not found)";
            }
        }
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
    }
    
    /// <summary>
    /// Restores an archived link to its original location.
    /// </summary>
    private async Task RestoreLinkAsync(TreeViewNode archivedNode)
    {
        if (archivedNode.Content is not LinkItem link)
            return;
        
        if (string.IsNullOrEmpty(link.OriginalCategoryPath))
        {
            await ShowErrorDialogAsync(
                "Cannot Restore",
                "Cannot determine original location for this link.");
            return;
        }
        
        // Find parent category
        TreeViewNode? parentNode = FindCategoryByPath(link.OriginalCategoryPath);
        if (parentNode == null)
        {
            await ShowErrorDialogAsync(
                "Cannot Restore",
                $"Original category '{link.OriginalCategoryPath}' not found.");
            return;
        }
        
        // Remove from archive
        archivedNode.Parent?.Children.Remove(archivedNode);
        
        // Clear archive metadata
        link.ArchivedDate = null;
        link.OriginalCategoryPath = null;
        link.ModifiedDate = DateTime.Now;
        
        // Restore to original location
        parentNode.Children.Add(archivedNode);
        parentNode.IsExpanded = true;
        
        // Save parent category
        await UpdateParentCategoriesAndSaveAsync(parentNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Restored link '{link.Title}' to '{link.CategoryPath}'";
    }
    
    /// <summary>
    /// Permanently deletes an archived category.
    /// </summary>
    private async Task PermanentlyDeleteCategoryAsync(TreeViewNode archivedNode)
    {
        if (archivedNode.Content is not CategoryItem category)
            return;
        
        bool confirmed = await ShowConfirmAsync(
            "Permanently Delete Category",
            $"Are you sure you want to permanently delete the category '{category.Name}' and all its contents? This action cannot be undone.",
            "Delete Permanently",
            "Cancel");
        
        if (!confirmed)
            return;
        
        // Remove from archive
        archivedNode.Parent?.Children.Remove(archivedNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Permanently deleted category '{category.Name}'";
        
        if (LinksTreeView.SelectedNode == archivedNode)
        {
            ShowWelcome();
        }
    }
    
    /// <summary>
    /// Permanently deletes an archived link.
    /// </summary>
    private async Task PermanentlyDeleteLinkAsync(TreeViewNode archivedNode)
    {
        if (archivedNode.Content is not LinkItem link)
            return;
        
        bool confirmed = await ShowConfirmAsync(
            "Permanently Delete Link",
            $"Are you sure you want to permanently delete the link '{link.Title}'? This action cannot be undone.",
            "Delete Permanently",
            "Cancel");
        
        if (!confirmed)
            return;
        
        // Remove from archive
        archivedNode.Parent?.Children.Remove(archivedNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Permanently deleted link '{link.Title}'";
        
        if (LinksTreeView.SelectedNode == archivedNode)
        {
            ShowWelcome();
        }
    }
    
    /// <summary>
    /// Finds a category node by its full path.
    /// </summary>
    private TreeViewNode? FindCategoryByPath(string categoryPath)
    {
        if (string.IsNullOrEmpty(categoryPath) || categoryPath == "Root")
            return null;
        
        var pathParts = categoryPath.Split(" > ");
        TreeViewNode? currentNode = null;
        
        // Find root category
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem cat && cat.Name == pathParts[0])
            {
                currentNode = rootNode;
                break;
            }
        }
        
        if (currentNode == null)
            return null;
        
        // Navigate through subcategories
        for (int i = 1; i < pathParts.Length; i++)
        {
            bool found = false;
            foreach (var child in currentNode.Children)
            {
                if (child.Content is CategoryItem cat && cat.Name == pathParts[i])
                {
                    currentNode = child;
                    found = true;
                    break;
                }
            }
            
            if (!found)
                return null;
        }
        
        return currentNode;
    }
    
    /// <summary>
    /// Gets archived ratings for a specific item for display in the rating dialog.
    /// Returns a dictionary of rating name to archived info.
    /// </summary>
    public Dictionary<string, Dialogs.ArchivedRatingInfo> GetArchivedRatingsForItem(string itemName)
    {
        var result = new Dictionary<string, Dialogs.ArchivedRatingInfo>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var archiveNode = GetOrCreateArchiveNode();
            
            // Look for a grouping node for this item
            foreach (var groupNode in archiveNode.Children)
            {
                if (groupNode.Content is CategoryItem groupCat)
                {
                    // Check if this grouping node matches the item name
                    // The Keywords contains the full grouping key (path/itemTitle)
                    var keyParts = groupCat.Keywords?.Split('/');
                    var groupItemName = keyParts != null && keyParts.Length > 0 ? keyParts[^1] : "";
                    
                    if (!string.Equals(groupItemName, itemName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Found the grouping node - extract archived ratings
                    foreach (var ratingNode in groupNode.Children)
                    {
                        if (ratingNode.Content is LinkItem link && link.CategoryPath == "ArchivedRating")
                        {
                            try
                            {
                                var ratingData = System.Text.Json.JsonSerializer.Deserialize<ArchivedRatingData>(link.Url);
                                if (ratingData != null)
                                {
                                    result[ratingData.RatingName] = new Dialogs.ArchivedRatingInfo
                                    {
                                        Score = ratingData.Score,
                                        Reason = ratingData.Reason,
                                        ArchivedDate = link.ArchivedDate ?? DateTime.Now
                                    };
                                }
                            }
                            catch
                            {
                                // Ignore parse errors
                            }
                        }
                    }
                    
                    break; // Found the grouping, no need to continue
                }
            }
        }
        catch
        {
            // Return empty dictionary on error
        }
        
        return result;
    }
    
    /// <summary>
    /// Archives old rating values when ratings change.
    /// Creates a node with the format: "ItemName - RatingName (Score)"
    /// Stores full path and rating data for restoration.
    /// </summary>
    public async Task ArchiveRatingChangeAsync(string parentName, string ratingName, RatingValue oldRating)
    {
        try
        {
            var archiveNode = GetOrCreateArchiveNode();
            
            // Find the parent node to get its full path
            TreeViewNode? parentNode = FindItemByName(parentName);
            
            // Build the full path (using "/" separator like import format)
            string categoryPath = "";
            string itemTitle = parentName;
            
            if (parentNode != null)
            {
                if (parentNode.Content is CategoryItem cat)
                {
                    // Get full category path - this is the path TO this category
                    var fullPath = _treeViewService?.GetCategoryPath(parentNode) ?? "";
                    itemTitle = cat.Name;
                    
                    // For storage, we need the PARENT path, not the full path including this category
                    if (fullPath.Contains(" > "))
                    {
                        var lastSeparator = fullPath.LastIndexOf(" > ");
                        categoryPath = fullPath.Substring(0, lastSeparator);
                    }
                    else
                    {
                        // Root category - no parent path
                        categoryPath = "";
                    }
                }
                else if (parentNode.Content is LinkItem link)
                {
                    // For links, we need to find the FULL path including parent links
                    // This is important for catalog entries which are nested under folder links
                    itemTitle = link.Title;
                    
                    // Walk up the tree to build the full path
                    var pathParts = new List<string>();
                    var current = parentNode.Parent;
                    
                    while (current != null)
                    {
                        if (current.Content is CategoryItem parentCat)
                        {
                            pathParts.Insert(0, parentCat.Name);
                        }
                        else if (current.Content is LinkItem parentLink)
                        {
                            pathParts.Insert(0, parentLink.Title);
                        }
                        current = current.Parent;
                    }
                    
                    categoryPath = string.Join(" > ", pathParts);
                    
                    System.Diagnostics.Debug.WriteLine($"[Archive] Link parent path built from tree: '{categoryPath}'");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] WARNING: Parent node not found for '{parentName}'");
            }
            
            // Convert path from " > " to "/" format for storage
            var storagePath = categoryPath.Replace(" > ", "/");
            
            
            // Create descriptive display name for the rating: "RatingName (Score)"
            var ratingDisplayName = $"{ratingName} ({oldRating.Score})";
            
            // Create unique grouping key for this target item (path + title)
            var groupingKey = string.IsNullOrEmpty(storagePath) 
                ? itemTitle 
                : $"{storagePath}/{itemTitle}";
            
            System.Diagnostics.Debug.WriteLine($"[Archive] Creating rating archive:");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Grouping key: '{groupingKey}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Item title: '{itemTitle}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Rating display: '{ratingDisplayName}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Parent path (storage): '{storagePath}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Rating: {oldRating.Rating} = {oldRating.Score}");
            
            // Find or create grouping node for this target item
            var groupingNode = FindOrCreateRatingGroupingNode(archiveNode, itemTitle, groupingKey, storagePath);
            
            // Remove any existing archive entry for the same rating type (keep only the latest)
            RemovePreviousRatingArchive(groupingNode, oldRating.Rating);
            
            // Build description
            var descriptionParts = new List<string>
            {
                $"Rating: {ratingName}",
                $"Score: {oldRating.Score}",
                $"Path: {categoryPath}"
            };
            
            if (!string.IsNullOrEmpty(oldRating.Reason))
            {
                descriptionParts.Add($"Reason: {oldRating.Reason}");
            }
            
            descriptionParts.Add($"Archived: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            var description = string.Join("\n", descriptionParts);
            
            // Create archived rating link node (simpler, just the rating details)
            var ratingDataLink = new LinkItem
            {
                Title = ratingDisplayName,
                Description = description,
                // Store data in URL field as structured data
                Url = System.Text.Json.JsonSerializer.Serialize(new ArchivedRatingData
                {
                    CategoryPath = storagePath,
                    Title = itemTitle,
                    RatingName = oldRating.Rating,
                    Score = oldRating.Score,
                    Reason = oldRating.Reason ?? ""
                }),
                IsDirectory = false,
                CategoryPath = "ArchivedRating", // Marker to identify rating archives
                CreatedDate = oldRating.CreatedDate,
                ModifiedDate = oldRating.ModifiedDate,
                ArchivedDate = DateTime.Now
            };
            
            // Add a rating to the link so the yellow star shows
            ratingDataLink.Ratings.Add(new RatingValue
            {
                Rating = oldRating.Rating,
                Score = oldRating.Score,
                Reason = oldRating.Reason ?? "",
                CreatedDate = oldRating.CreatedDate,
                ModifiedDate = oldRating.ModifiedDate
            });
            
            var ratingNode = new TreeViewNode { Content = ratingDataLink };
            groupingNode.Children.Add(ratingNode);
            groupingNode.IsExpanded = true;
            
            // Update grouping node name to show count
            UpdateRatingGroupingNodeName(groupingNode);
            
            // Update archive count display
            UpdateArchiveNodeName();
            
            // Save archive to JSON
            await SaveArchiveToJsonAsync();
            
            System.Diagnostics.Debug.WriteLine($"[Archive] Rating archived successfully under '{itemTitle}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Error archiving rating: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Finds or creates a grouping node for rating archives of a specific target item.
    /// </summary>
    private TreeViewNode FindOrCreateRatingGroupingNode(TreeViewNode archiveNode, string itemTitle, string groupingKey, string storagePath)
    {
        // Look for existing grouping node with matching key
        foreach (var child in archiveNode.Children)
        {
            if (child.Content is CategoryItem cat && cat.Keywords == groupingKey)
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] Found existing grouping node for '{itemTitle}'");
                return child;
            }
        }
        
        // Create new grouping node
        System.Diagnostics.Debug.WriteLine($"[Archive] Creating new grouping node for '{itemTitle}'");
        
        var groupingCategory = new CategoryItem
        {
            Name = itemTitle,
            Description = $"Archived rating changes for '{itemTitle}'",
            Keywords = groupingKey, // Store the full path key for matching
            Icon = "A", // Red A icon
            ArchivedDate = DateTime.Now,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };
        
        // Store path in OriginalParentPath for reference
        groupingCategory.OriginalParentPath = storagePath;
        
        var groupingNode = new TreeViewNode { Content = groupingCategory };
        archiveNode.Children.Add(groupingNode);
        
        return groupingNode;
    }
    
    /// <summary>
    /// Updates the name of a rating grouping node to show the count of archived ratings.
    /// </summary>
    private void UpdateRatingGroupingNodeName(TreeViewNode groupingNode)
    {
        if (groupingNode.Content is CategoryItem cat)
        {
            // Extract the original item name from the Keywords (it's stored there)
            var keyParts = cat.Keywords?.Split('/');
            var originalName = keyParts != null && keyParts.Length > 0 ? keyParts[^1] : cat.Name;
            
            
            // Remove any existing count suffix
            if (originalName.Contains(" (") && originalName.EndsWith(")"))
            {
                originalName = originalName.Substring(0, originalName.LastIndexOf(" ("));
            }
            
            cat.Name = $"{originalName} ({groupingNode.Children.Count} rating changes)";
        }
    }
    
    /// <summary>
    /// Removes any previous archive entry for the same rating type (keeps only the latest).
    /// This ensures only one archive entry exists per rating type per target item.
    /// </summary>
    private void RemovePreviousRatingArchive(TreeViewNode groupingNode, string ratingName)
    {
        // Find and remove any existing archive entry with the same rating name
        TreeViewNode? existingNode = null;
        
        foreach (var child in groupingNode.Children)
        {
            if (child.Content is LinkItem link && link.CategoryPath == "ArchivedRating")
            {
                try
                {
                    var ratingData = System.Text.Json.JsonSerializer.Deserialize<ArchivedRatingData>(link.Url);
                    if (ratingData != null && ratingData.RatingName == ratingName)
                    {
                        existingNode = child;
                        System.Diagnostics.Debug.WriteLine($"[Archive] Found existing archive for '{ratingName}', will replace");
                        break;
                    }
                }
                catch
                {
                    // Ignore parse errors, continue searching
                }
            }
            else if (child.Content is CategoryItem cat && IsRatingArchive(cat))
            {
                // Old structure - check child link
                if (cat.Links != null && cat.Links.Count > 0)
                {
                    try
                    {
                        var ratingData = System.Text.Json.JsonSerializer.Deserialize<ArchivedRatingData>(cat.Links[0].Url);
                        if (ratingData != null && ratingData.RatingName == ratingName)
                        {
                            existingNode = child;
                            System.Diagnostics.Debug.WriteLine($"[Archive] Found existing archive (old structure) for '{ratingName}', will replace");
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
            }
        }
        
        if (existingNode != null)
        {
            groupingNode.Children.Remove(existingNode);
            System.Diagnostics.Debug.WriteLine($"[Archive] Removed previous archive entry for '{ratingName}'");
        }
    }
    
    /// <summary>
    /// Restores an archived rating value.
    /// Handles both new structure (LinkItem under grouping node) and old structure (CategoryItem).
    /// </summary>
    private async Task RestoreRatingAsync(TreeViewNode archivedNode)
    {
        // Handle both LinkItem (new structure) and CategoryItem (old structure)
        LinkItem? dataLink = null;
        string displayName = "";
        
        if (archivedNode.Content is LinkItem link && link.CategoryPath == "ArchivedRating")
        {
            // New structure: rating is a LinkItem
            dataLink = link;
            displayName = link.Title;
        }
        else if (archivedNode.Content is CategoryItem archivedCategory)
        {
            // Old structure: rating is a CategoryItem with a child link
            displayName = archivedCategory.Name;
            
            if (archivedCategory.Links != null && archivedCategory.Links.Count > 0)
            {
                dataLink = archivedCategory.Links[0];
            }
        }
        
        if (dataLink == null)
        {
            await ShowErrorDialogAsync("Cannot Restore Rating", "Rating data not found in archive.");
            return;
        }
        
        // Check if this is a rating archive
        if (dataLink.CategoryPath != "ArchivedRating")
        {
            await ShowErrorDialogAsync("Cannot Restore Rating", "This is not a rating archive.");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Restoring rating from: {displayName}");
            
            // Parse the stored rating data
            ArchivedRatingData? ratingData;
            try
            {
                ratingData = System.Text.Json.JsonSerializer.Deserialize<ArchivedRatingData>(dataLink.Url);
            }
            catch
            {
                await ShowErrorDialogAsync("Cannot Restore Rating", "Failed to parse rating data.");
                return;
            }
            
            if (ratingData == null)
            {
                await ShowErrorDialogAsync("Cannot Restore Rating", "Rating data is empty.");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[Archive] Parsed rating data:");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Path: '{ratingData.CategoryPath}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Title: '{ratingData.Title}'");
            System.Diagnostics.Debug.WriteLine($"[Archive]   Rating: {ratingData.RatingName} = {ratingData.Score}");
            
            // Convert path from "/" to " > " format for finding
            var searchPath = ratingData.CategoryPath.Replace("/", " > ");
            System.Diagnostics.Debug.WriteLine($"[Archive] Search path after conversion: '{searchPath}'");
            
            // Find the target node
            TreeViewNode? targetNode = null;
            
            if (string.IsNullOrEmpty(searchPath))
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] Empty path - searching root nodes for: '{ratingData.Title}'");
                // Root level item - search by name directly
                targetNode = FindItemByName(ratingData.Title);
                if (targetNode != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] Found by FindItemByName");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] NOT found by FindItemByName");
                    // List root nodes for debugging
                    System.Diagnostics.Debug.WriteLine($"[Archive] Available root nodes:");
                    foreach (var root in LinksTreeView.RootNodes)
                    {
                        if (root.Content is CategoryItem rootCat)
                            System.Diagnostics.Debug.WriteLine($"[Archive]   - Category: '{rootCat.Name}'");
                        else if (root.Content is LinkItem rootLink)
                            System.Diagnostics.Debug.WriteLine($"[Archive]   - Link: '{rootLink.Title}'");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] Has path - searching by path: '{searchPath}'");
                // Has a path - find the category/link at path, then the item
                var pathParts = searchPath.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
                
                // Start at root
                TreeViewNode? currentNode = null;
                
                // Find the first part (root category)
                foreach (var rootNode in LinksTreeView.RootNodes)
                {
                    if (rootNode.Content is CategoryItem cat && cat.Name == pathParts[0])
                    {
                        currentNode = rootNode;
                        System.Diagnostics.Debug.WriteLine($"[Archive] Found root: '{cat.Name}'");
                        break;
                    }
                }
                
                if (currentNode == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] Root node not found: '{pathParts[0]}'");
                }
                else
                {
                    // Navigate through the path (could be categories or links)
                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        var partName = pathParts[i];
                        TreeViewNode? foundChild = null;
                        
                        foreach (var child in currentNode.Children)
                        {
                            if (child.Content is CategoryItem childCat && childCat.Name == partName)
                            {
                                foundChild = child;
                                System.Diagnostics.Debug.WriteLine($"[Archive] Found category in path: '{partName}'");
                                break;
                            }
                            else if (child.Content is LinkItem childLink && childLink.Title == partName)
                            {
                                foundChild = child;
                                System.Diagnostics.Debug.WriteLine($"[Archive] Found link in path: '{partName}'");
                                break;
                            }
                        }
                        
                        if (foundChild != null)
                        {
                            currentNode = foundChild;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Archive] Path part not found: '{partName}'");
                            currentNode = null;
                            break;
                        }
                    }
                }
                
                if (currentNode != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] At path node, searching for target: '{ratingData.Title}'");
                    
                    // Now search for the target item in the current node's children
                    foreach (var child in currentNode.Children)
                    {
                        if (child.Content is CategoryItem targetCat && targetCat.Name == ratingData.Title)
                        {
                            targetNode = child;
                            System.Diagnostics.Debug.WriteLine($"[Archive] Found target category: '{targetCat.Name}'");
                            break;
                        }
                        else if (child.Content is LinkItem targetLink && targetLink.Title == ratingData.Title)
                        {
                            targetNode = child;
                            System.Diagnostics.Debug.WriteLine($"[Archive] Found target link: '{targetLink.Title}'");
                            break;
                        }
                    }
                    
                    if (targetNode == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Archive] Target not found in children. Available:");
                        foreach (var child in currentNode.Children)
                        {
                            if (child.Content is LinkItem l)
                                System.Diagnostics.Debug.WriteLine($"[Archive]   - Link: '{l.Title}'");
                            else if (child.Content is CategoryItem c)
                                System.Diagnostics.Debug.WriteLine($"[Archive]   - Cat: '{c.Name}'");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] Could not navigate to path: '{searchPath}'");
                }
                
                // Fallback: try searching by full path + title
                if (targetNode == null)
                {
                    var fullPath = string.IsNullOrEmpty(searchPath) 
                        ? ratingData.Title 
                        : $"{searchPath} > {ratingData.Title}";
                    System.Diagnostics.Debug.WriteLine($"[Archive] Fallback: trying full path: '{fullPath}'");
                    targetNode = FindCategoryByPath(fullPath);
                    if (targetNode != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Archive] Found by full path fallback");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Archive] NOT found by full path fallback");
                    }
                }
            }
            
            if (targetNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] ERROR: Target not found!");
                System.Diagnostics.Debug.WriteLine($"[Archive]   CategoryPath: '{ratingData.CategoryPath}'");
                System.Diagnostics.Debug.WriteLine($"[Archive]   Title: '{ratingData.Title}'");
                System.Diagnostics.Debug.WriteLine($"[Archive]   SearchPath: '{searchPath}'");
                await ShowErrorDialogAsync(
                    "Cannot Restore Rating",
                    $"Target item not found.\n\nPath: {ratingData.CategoryPath}\nTitle: {ratingData.Title}\n\nThe item may have been moved, renamed, or deleted.");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[Archive] Found target node: {ratingData.Title}");
            
            // Create the rating value to restore
            var restoredRating = new RatingValue
            {
                Rating = ratingData.RatingName,
                Score = ratingData.Score,
                Reason = ratingData.Reason,
                CreatedDate = dataLink.CreatedDate,
                ModifiedDate = DateTime.Now
            };
            
            // Get the rating name without template prefix for archiving current
            var simpleRatingName = ratingData.RatingName.Contains('.')
                ? ratingData.RatingName.Substring(ratingData.RatingName.LastIndexOf('.') + 1)
                : ratingData.RatingName;
            
            // Apply the rating to the target
            if (targetNode.Content is CategoryItem category)
            {
                // Check if there's a current rating to archive first
                var currentRating = category.Ratings.FirstOrDefault(r => r.Rating == ratingData.RatingName);
                if (currentRating != null)
                {
                    category.Ratings.Remove(currentRating);
                    await ArchiveRatingChangeAsync(ratingData.Title, simpleRatingName, currentRating);
                    System.Diagnostics.Debug.WriteLine($"[Archive] Current rating archived for swap");
                }
                
                category.Ratings.Add(restoredRating);
                category.ModifiedDate = DateTime.Now;
                
                var rootNode = GetRootCategoryNode(targetNode);
                await _categoryService!.SaveCategoryAsync(rootNode);
            }
            else if (targetNode.Content is LinkItem targetLinkItem)
            {
                var currentRating = targetLinkItem.Ratings.FirstOrDefault(r => r.Rating == ratingData.RatingName);
                if (currentRating != null)
                {
                    targetLinkItem.Ratings.Remove(currentRating);
                    await ArchiveRatingChangeAsync(ratingData.Title, simpleRatingName, currentRating);
                    System.Diagnostics.Debug.WriteLine($"[Archive] Current rating archived for swap");
                }
                
                targetLinkItem.Ratings.Add(restoredRating);
                targetLinkItem.ModifiedDate = DateTime.Now;
                
                var rootNode = GetRootCategoryNode(targetNode);
                await _categoryService!.SaveCategoryAsync(rootNode);
            }
            
            // Remove this archive node
            var parentNode = archivedNode.Parent;
            parentNode?.Children.Remove(archivedNode);
            
            // If the parent is a grouping node (not the Archive root), update its name or remove if empty
            if (parentNode != null && parentNode.Content is CategoryItem parentCat && !parentCat.IsArchiveNode)
            {
                if (parentNode.Children.Count == 0)
                {
                    // Grouping node is now empty, remove it
                    System.Diagnostics.Debug.WriteLine($"[Archive] Removing empty grouping node: '{parentCat.Name}'");
                    parentNode.Parent?.Children.Remove(parentNode);
                }
                else
                {
                    // Update the grouping node name with new count
                    UpdateRatingGroupingNodeName(parentNode);
                }
            }
            
            // Update archive count display
            UpdateArchiveNodeName();
            
            // Save archive to JSON
            await SaveArchiveToJsonAsync();
            
            StatusText.Text = $"Restored rating '{simpleRatingName}' to '{ratingData.Title}'";
            System.Diagnostics.Debug.WriteLine($"[Archive] Rating restore complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Archive] Error restoring rating: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorDialogAsync("Error Restoring Rating", $"An error occurred:\n\n{ex.Message}");
        }
    }
    
    /// <summary>
    /// Checks if a category is a rating archive based on its stored data (old structure).
    /// </summary>
    private bool IsRatingArchive(CategoryItem category)
    {
        return category.Links != null && 
               category.Links.Count > 0 && 
               category.Links[0].CategoryPath == "ArchivedRating";
    }
    
    /// <summary>
    /// Checks if a node is a rating archive (new or old structure).
    /// </summary>
    private bool IsRatingArchiveNode(TreeViewNode node)
    {
        // New structure: LinkItem with ArchivedRating marker
        if (node.Content is LinkItem link && link.CategoryPath == "ArchivedRating")
            return true;
        
        // Old structure: CategoryItem with child link marked as ArchivedRating
        if (node.Content is CategoryItem cat)
            return IsRatingArchive(cat);
        
        return false;
    }
    
    /// <summary>
    /// Checks if a node is a rating grouping node (parent container for rating archives).
    /// </summary>
    private bool IsRatingGroupingNode(TreeViewNode node)
    {
        if (node.Content is not CategoryItem cat)
            return false;
        
        // A grouping node has Keywords set (the grouping key) and ArchivedDate but is not a rating archive itself
        return cat.ArchivedDate.HasValue && 
               !string.IsNullOrEmpty(cat.Keywords) && 
               !IsRatingArchive(cat);
    }
    
    /// <summary>
    /// Finds a category or link node by name.
    /// </summary>
    private TreeViewNode? FindItemByName(string itemName)
    {
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem cat && cat.Name == itemName)
                return rootNode;
            
            var found = FindItemByNameRecursive(rootNode, itemName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    private TreeViewNode? FindItemByNameRecursive(TreeViewNode node, string itemName)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem cat && cat.Name == itemName)
                return child;
            
            if (child.Content is LinkItem link && link.Title == itemName)
                return child;
            
            var found = FindItemByNameRecursive(child, itemName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    // ========================================
    // ARCHIVE CONTEXT MENU HANDLERS
    // ========================================
    
    /// <summary>
    /// Handler for restoring archived items from context menu.
    /// </summary>
    private async void ArchiveMenu_Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode == null)
            return;
        
        // Check if this is a rating archive (new LinkItem or old CategoryItem structure)
        if (IsRatingArchiveNode(_contextMenuNode))
        {
            await RestoreRatingAsync(_contextMenuNode);
        }
        else if (_contextMenuNode.Content is CategoryItem category)
        {
            // Check if this is a rating grouping node - cannot restore directly
            if (IsRatingGroupingNode(_contextMenuNode))
            {
                await ShowErrorDialogAsync(
                    "Cannot Restore Grouping Node",
                    $"'{category.Name}' is a grouping node containing multiple rating archives.\n\n" +
                    "Expand this node and restore individual ratings, or delete the entire grouping.");
                return;
            }
            
            await RestoreCategoryAsync(_contextMenuNode);
        }
        else if (_contextMenuNode.Content is LinkItem)
        {
            await RestoreLinkAsync(_contextMenuNode);
        }
    }
    
    /// <summary>
    /// Handler for permanently deleting archived items from context menu.
    /// </summary>
    private async void ArchiveMenu_DeletePermanently_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode == null)
            return;
        
        // Check if this is a rating archive (new LinkItem or old CategoryItem structure)
        if (IsRatingArchiveNode(_contextMenuNode))
        {
            await PermanentlyDeleteRatingArchiveAsync(_contextMenuNode);
        }
        else if (_contextMenuNode.Content is CategoryItem category)
        {
            // Check if this is a rating grouping node
            if (IsRatingGroupingNode(_contextMenuNode))
            {
                await PermanentlyDeleteRatingGroupingAsync(_contextMenuNode);
            }
            else
            {
                await PermanentlyDeleteCategoryAsync(_contextMenuNode);
            }
        }
        else if (_contextMenuNode?.Content is LinkItem)
        {
            await PermanentlyDeleteLinkAsync(_contextMenuNode);
        }
    }
    
    /// <summary>
    /// Permanently deletes an archived rating (handles both new LinkItem and old CategoryItem structures).
    /// </summary>
    private async Task PermanentlyDeleteRatingArchiveAsync(TreeViewNode archivedNode)
    {
        // Get display name for confirmation
        string displayName;
        if (archivedNode.Content is LinkItem link)
            displayName = link.Title;
        else if (archivedNode.Content is CategoryItem cat)
            displayName = cat.Name;
        else
            return;
        
        bool confirmed = await ShowConfirmAsync(
            "Permanently Delete Rating Archive",
            $"Are you sure you want to permanently delete the archived rating '{displayName}'? This action cannot be undone.",
            "Delete Permanently",
            "Cancel");
        
        if (!confirmed)
            return;
        
        // Get parent before removal
        var parentNode = archivedNode.Parent;
        
        // Remove from archive
        parentNode?.Children.Remove(archivedNode);
        
        // If the parent is a grouping node (not the Archive root), update its name or remove if empty
        if (parentNode != null && parentNode.Content is CategoryItem parentCat && !parentCat.IsArchiveNode)
        {
            if (parentNode.Children.Count == 0)
            {
                // Grouping node is now empty, remove it
                System.Diagnostics.Debug.WriteLine($"[Archive] Removing empty grouping node: '{parentCat.Name}'");
                parentNode.Parent?.Children.Remove(parentNode);
            }
            else
            {
                // Update the grouping node name with new count
                UpdateRatingGroupingNodeName(parentNode);
            }
        }
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Permanently deleted archived rating '{displayName}'";
        
        if (LinksTreeView.SelectedNode == archivedNode)
        {
            ShowWelcome();
        }
    }
    
    /// <summary>
    /// Permanently deletes a rating grouping node and all its children.
    /// </summary>
    private async Task PermanentlyDeleteRatingGroupingAsync(TreeViewNode groupingNode)
    {
        if (groupingNode.Content is not CategoryItem category)
            return;
        
        var childCount = groupingNode.Children.Count;
        
        bool confirmed = await ShowConfirmAsync(
            "Permanently Delete Rating Grouping",
            $"Are you sure you want to permanently delete '{category.Name}' and all {childCount} archived rating(s) it contains?\n\nThis action cannot be undone.",
            "Delete All",
            "Cancel");
        
        if (!confirmed)
            return;
        
        // Remove from archive
        groupingNode.Parent?.Children.Remove(groupingNode);
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Permanently deleted '{category.Name}' with {childCount} archived rating(s)";
        
        if (LinksTreeView.SelectedNode == groupingNode)
        {
            ShowWelcome();
        }
    }
    
    /// <summary>
    /// Clears archive items based on age filter.
    /// </summary>
    /// <param name="filter">Filter option: "all", "day", "week", "month"</param>
    public async Task ClearArchiveAsync(string filter)
    {
        var archiveNode = GetOrCreateArchiveNode();
        if (archiveNode.Children.Count == 0)
        {
            StatusText.Text = "Archive is already empty.";
            return;
        }
        
        // Calculate cutoff date based on filter
        DateTime? cutoffDate = filter switch
        {
            "day" => DateTime.Now.AddDays(-1),
            "week" => DateTime.Now.AddDays(-7),
            "month" => DateTime.Now.AddDays(-30),
            _ => null // "all" - no cutoff
        };
        
        var filterDescription = filter switch
        {
            "day" => "older than 1 day",
            "week" => "older than 1 week",
            "month" => "older than 1 month",
            _ => "all items"
        };
        
        // Count items to delete
        var nodesToRemove = new List<TreeViewNode>();
        
        foreach (var child in archiveNode.Children.ToList())
        {
            DateTime? archivedDate = null;
            
            if (child.Content is CategoryItem cat)
                archivedDate = cat.ArchivedDate;
            else if (child.Content is LinkItem link)
                archivedDate = link.ArchivedDate;
            
            // If no cutoff (all) or item is older than cutoff
            if (cutoffDate == null || (archivedDate.HasValue && archivedDate.Value < cutoffDate.Value))
            {
                nodesToRemove.Add(child);
            }
        }
        
        if (nodesToRemove.Count == 0)
        {
            StatusText.Text = $"No items {filterDescription} to clear.";
            return;
        }
        
        // Confirm deletion
        bool confirmed = await ShowConfirmAsync(
            "Clear Archive",
            $"Are you sure you want to permanently delete {nodesToRemove.Count} archived item(s) ({filterDescription})?\n\nThis action cannot be undone.",
            "Delete Permanently",
            "Cancel");
        
        if (!confirmed)
            return;
        
        // Remove items
        foreach (var node in nodesToRemove)
        {
            archiveNode.Children.Remove(node);
        }
        
        // Update archive count display
        UpdateArchiveNodeName();
        
        // Save archive to JSON
        await SaveArchiveToJsonAsync();
        
        StatusText.Text = $"Cleared {nodesToRemove.Count} archived item(s) ({filterDescription}).";
        
        // Refresh the archive details view
        if (LinksTreeView.SelectedNode == archiveNode && archiveNode.Content is CategoryItem archiveCategory)
        {
            await _detailsViewService!.ShowCategoryDetailsAsync(
                archiveCategory, 
                archiveNode, 
                onClearArchive: ClearArchiveAsync);
        }
    }
}

/// <summary>
/// Archive data structure for JSON serialization.
/// </summary>
public class ArchiveData
{
    public List<CategoryItem>? ArchivedCategories { get; set; }
    public List<LinkItem>? ArchivedLinks { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Structured data for archived ratings (stored as JSON in Link.Url).
/// Similar to import format for consistency.
/// </summary>
public class ArchivedRatingData
{
    /// <summary>
    /// Path to the category containing the item (using "/" separator).
    /// Empty string for root-level items.
    /// Example: "Project/MyMemories/Dialogs"
    /// </summary>
    public string CategoryPath { get; set; } = "";
    
    /// <summary>
    /// Name/title of the item (category name or link title).
    /// Example: "LinkDialogBuilder.cs"
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Full rating name including template prefix.
    /// Example: "Programming.Complexity"
    /// </summary>
    public string RatingName { get; set; } = "";
    
    /// <summary>
    /// The rating score value.
    /// </summary>
    public int Score { get; set; }
    
    /// <summary>
    /// The rating reason/justification.
    /// </summary>
    public string Reason { get; set; } = "";
}



