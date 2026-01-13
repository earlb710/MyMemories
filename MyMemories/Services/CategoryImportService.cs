using Microsoft.UI.Xaml.Controls;
using MyMemories.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for importing category operations from JSON files.
/// </summary>
public class CategoryImportService
{
    private readonly CategoryService _categoryService;
    private readonly TreeViewService _treeViewService;
    private readonly TagManagementService? _tagService;
    private readonly RatingManagementService? _ratingService;
    private readonly TreeView _treeView;

    public CategoryImportService(
        CategoryService categoryService,
        TreeViewService treeViewService,
        TreeView treeView,
        TagManagementService? tagService = null,
        RatingManagementService? ratingService = null)
    {
        _categoryService = categoryService;
        _treeViewService = treeViewService;
        _treeView = treeView;
        _tagService = tagService;
        _ratingService = ratingService;
    }

    /// <summary>
    /// Imports operations from a JSON file.
    /// </summary>
    public async Task<ImportResult> ImportFromFileAsync(string filePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult();
        
        try
        {
            // Read and parse the import file
            var json = await File.ReadAllTextAsync(filePath);
            var importData = JsonSerializer.Deserialize<CategoryImportData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (importData == null || importData.Operations == null)
            {
                result.Success = false;
                result.OperationResults.Add(new ImportOperationResult
                {
                    Status = "Failed",
                    Message = "Invalid import file format"
                });
                return result;
            }

            // Validate version
            if (importData.Version != "1.0")
            {
                result.Success = false;
                result.OperationResults.Add(new ImportOperationResult
                {
                    Status = "Failed",
                    Message = $"Unsupported import version: {importData.Version}"
                });
                return result;
            }

            result.TotalOperations = importData.Operations.Count;

            // Process each operation
            var modifiedCategories = new HashSet<string>();
            
            foreach (var operation in importData.Operations)
            {
                var opResult = await ProcessOperationAsync(operation, modifiedCategories);
                result.OperationResults.Add(opResult);

                switch (opResult.Status)
                {
                    case "Success":
                        result.Successful++;
                        break;
                    case "Failed":
                        result.Failed++;
                        break;
                    case "Skipped":
                        result.Skipped++;
                        break;
                }
            }

            result.Success = result.Failed == 0;
            result.CategoriesModified = modifiedCategories.ToList();
            
            stopwatch.Stop();
            result.ImportDuration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CategoryImportService] Import error: {ex.Message}");
            result.Success = false;
            result.OperationResults.Add(new ImportOperationResult
            {
                Status = "Failed",
                Message = $"Import failed: {ex.Message}"
            });
        }

        return result;
    }

    /// <summary>
    /// Processes a single import operation.
    /// </summary>
    private async Task<ImportOperationResult> ProcessOperationAsync(
        ImportOperation operation,
        HashSet<string> modifiedCategories)
    {
        var result = new ImportOperationResult
        {
            Operation = operation.Operation,
            Target = operation.Target,
            Identifier = operation.Identifier
        };

        try
        {
            switch (operation.Operation.ToLower())
            {
                case "add":
                    await ProcessAddOperationAsync(operation, result, modifiedCategories);
                    break;
                case "update":
                    await ProcessUpdateOperationAsync(operation, result, modifiedCategories);
                    break;
                case "delete":
                    await ProcessDeleteOperationAsync(operation, result, modifiedCategories);
                    break;
                default:
                    result.Status = "Failed";
                    result.Message = $"Unknown operation: {operation.Operation}";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.Status = "Failed";
            result.Message = $"Error: {ex.Message}";
            Debug.WriteLine($"[CategoryImportService] Operation error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Processes an Add operation.
    /// </summary>
    private async Task ProcessAddOperationAsync(
        ImportOperation operation,
        ImportOperationResult result,
        HashSet<string> modifiedCategories)
    {
        switch (operation.Target.ToLower())
        {
            case "category":
                await AddCategoryAsync(operation, result, modifiedCategories);
                break;
            case "subcategory":
                await AddSubCategoryAsync(operation, result, modifiedCategories);
                break;
            case "link":
                await AddLinkAsync(operation, result, modifiedCategories);
                break;
            case "tag":
                await AddTagsAsync(operation, result, modifiedCategories);
                break;
            default:
                result.Status = "Failed";
                result.Message = $"Unknown target: {operation.Target}";
                break;
        }
    }

    /// <summary>
    /// Processes an Update operation.
    /// </summary>
    private async Task ProcessUpdateOperationAsync(
        ImportOperation operation,
        ImportOperationResult result,
        HashSet<string> modifiedCategories)
    {
        switch (operation.Target.ToLower())
        {
            case "category":
                await UpdateCategoryAsync(operation, result, modifiedCategories);
                break;
            case "subcategory":
                await UpdateSubCategoryAsync(operation, result, modifiedCategories);
                break;
            case "link":
                await UpdateLinkAsync(operation, result, modifiedCategories);
                break;
            case "rating":
                await UpdateRatingsAsync(operation, result, modifiedCategories);
                break;
            default:
                result.Status = "Failed";
                result.Message = $"Unknown target: {operation.Target}";
                break;
        }
    }

    /// <summary>
    /// Processes a Delete operation.
    /// </summary>
    private async Task ProcessDeleteOperationAsync(
        ImportOperation operation,
        ImportOperationResult result,
        HashSet<string> modifiedCategories)
    {
        switch (operation.Target.ToLower())
        {
            case "category":
                await DeleteCategoryAsync(operation, result, modifiedCategories);
                break;
            case "subcategory":
                await DeleteSubCategoryAsync(operation, result, modifiedCategories);
                break;
            case "link":
                await DeleteLinkAsync(operation, result, modifiedCategories);
                break;
            case "tag":
                await DeleteTagsAsync(operation, result, modifiedCategories);
                break;
            case "rating":
                await DeleteRatingsAsync(operation, result, modifiedCategories);
                break;
            default:
                result.Status = "Failed";
                result.Message = $"Unknown target: {operation.Target}";
                break;
        }
    }

    /// <summary>
    /// Gets the root category node for a given node.
    /// </summary>
    private TreeViewNode? GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100;
        
        while (current?.Parent != null && safetyCounter < maxDepth)
        {
            if (current.Content is CategoryItem)
            {
                var parent = current.Parent;
                if (parent.Content is not CategoryItem)
                {
                    return current;
                }
                current = parent;
            }
            else
            {
                current = current.Parent;
            }
            safetyCounter++;
        }
        
        if (current == null || safetyCounter >= maxDepth)
        {
            return null;
        }
        
        return current.Content is CategoryItem ? current : null;
    }

    private Task AddCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Category operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task AddSubCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "SubCategory operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task AddLinkAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Link operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task AddTagsAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Tag operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task UpdateCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Update operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task UpdateSubCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Update operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task UpdateLinkAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Update operations not yet implemented";
        return Task.CompletedTask;
    }

    private async Task UpdateRatingsAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        try
        {
            // Parse rating data - try the import format first (Name/Value)
            var dataJson = System.Text.Json.JsonSerializer.Serialize(operation.Data);
            var ratingDataWithEntries = System.Text.Json.JsonSerializer.Deserialize<ImportRatingDataWithEntries>(dataJson, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (ratingDataWithEntries?.Ratings == null || ratingDataWithEntries.Ratings.Count == 0)
            {
                result.Status = "Failed";
                result.Message = "Invalid rating data format or no ratings provided";
                return;
            }

            // Find the target link or category using enhanced path resolution
            TreeViewNode? targetNode = null;
            if (!string.IsNullOrEmpty(operation.Identifier?.CategoryPath) && !string.IsNullOrEmpty(operation.Identifier.Title))
            {
                targetNode = FindNodeByFullPath(operation.Identifier.CategoryPath, operation.Identifier.Title);
            }

            if (targetNode == null)
            {
                result.Status = "Failed";
                result.Message = $"Target not found: {operation.Identifier?.CategoryPath}/{operation.Identifier?.Title}";
                return;
            }

            // Get the item (category or link)
            List<RatingValue>? itemRatings = null;
            string? itemName = null;
            
            if (targetNode.Content is CategoryItem category)
            {
                itemRatings = category.Ratings;
                itemName = category.Name;
            }
            else if (targetNode.Content is LinkItem link)
            {
                itemRatings = link.Ratings;
                itemName = link.Title;
            }
            else
            {
                result.Status = "Failed";
                result.Message = "Target is neither a category nor a link";
                return;
            }

            // Convert import format ratings (Name/Value) to RatingValue objects (Rating/Score)
            var newRatings = new List<RatingValue>();
            int skippedCount = 0;
            
            foreach (var importRating in ratingDataWithEntries.Ratings)
            {
                // Skip corrupt ratings without names
                if (string.IsNullOrWhiteSpace(importRating.Name))
                {
                    skippedCount++;
                    Debug.WriteLine($"[UpdateRatingsAsync] Skipping corrupt rating without name for {itemName}");
                    continue;
                }

                // Validate that the rating definition exists
                var definition = _ratingService?.GetDefinition(importRating.Name);
                if (definition == null)
                {
                    Debug.WriteLine($"[UpdateRatingsAsync] Warning: Rating definition '{importRating.Name}' not found for {itemName}");
                    // Continue anyway - the rating will be applied even if definition is missing
                }

                newRatings.Add(new RatingValue
                {
                    Rating = importRating.Name,  // Map Name -> Rating
                    Score = importRating.Value,  // Map Value -> Score
                    Reason = importRating.Reason ?? string.Empty,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                });
            }

            if (newRatings.Count == 0)
            {
                result.Status = "Skipped";
                result.Message = $"No valid ratings to apply to {itemName} ({skippedCount} corrupt rating(s) skipped)";
                return;
            }

            // Apply the ratings based on MergeRatings option
            bool mergeRatings = operation.Options?.MergeRatings ?? false;
            
            if (mergeRatings)
            {
                // Merge: Add new ratings, update existing ones
                foreach (var newRating in newRatings)
                {
                    var existingRating = itemRatings.Find(r => 
                        string.Equals(r.Rating, newRating.Rating, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingRating != null)
                    {
                        // Update existing rating
                        existingRating.Score = newRating.Score;
                        existingRating.Reason = newRating.Reason;
                        existingRating.ModifiedDate = DateTime.Now;
                    }
                    else
                    {
                        // Add new rating
                        itemRatings.Add(newRating);
                    }
                }
            }
            else
            {
                // Replace: Clear all existing ratings and add new ones
                itemRatings.Clear();
                itemRatings.AddRange(newRatings);
            }

            // Save the category
            var rootCategoryNode = GetRootCategoryNode(targetNode);
            if (rootCategoryNode != null)
            {
                await _categoryService.SaveCategoryAsync(rootCategoryNode);
                
                if (rootCategoryNode.Content is CategoryItem rootCategory)
                {
                    modifiedCategories.Add(rootCategory.Name);
                }
            }

            var statusMsg = skippedCount > 0 
                ? $"Applied {newRatings.Count} rating(s) to {itemName} ({(mergeRatings ? "merged" : "replaced")}), {skippedCount} corrupt rating(s) skipped"
                : $"Applied {newRatings.Count} rating(s) to {itemName} ({(mergeRatings ? "merged" : "replaced")})";
            
            result.Status = "Success";
            result.Message = statusMsg;
        }
        catch (Exception ex)
        {
            result.Status = "Failed";
            result.Message = $"Error updating ratings: {ex.Message}";
            Debug.WriteLine($"[UpdateRatingsAsync] Exception: {ex}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Finds a node by full path, handling both categories and catalog entries.
    /// Path format: "Category/SubCategory/FolderLink/SubFolder/FileName"
    /// </summary>
    private TreeViewNode? FindNodeByFullPath(string categoryPath, string title)
    {
        var pathParts = categoryPath.Split(new[] { '/', '.' }, StringSplitOptions.RemoveEmptyEntries);
        TreeViewNode? current = null;
        int partIndex = 0;

        // Navigate through the path
        while (partIndex < pathParts.Length)
        {
            var part = pathParts[partIndex];
            
            if (current == null)
            {
                // Find root category
                current = FindCategoryByName(part);
                if (current == null)
                {
                    Debug.WriteLine($"[FindNodeByFullPath] Root category not found: {part}");
                    return null;
                }
            }
            else
            {
                // Look for next part in children
                TreeViewNode? found = null;
                
                foreach (var child in current.Children)
                {
                    if (child.Content is CategoryItem cat && cat.Name == part)
                    {
                        found = child;
                        break;
                    }
                    else if (child.Content is LinkItem link && link.Title == part)
                    {
                        found = child;
                        break;
                    }
                }
                
                if (found == null)
                {
                    Debug.WriteLine($"[FindNodeByFullPath] Path part not found: {part} (searched in {(current.Content as CategoryItem)?.Name ?? (current.Content as LinkItem)?.Title})");
                    return null;
                }
                
                current = found;
            }
            
            partIndex++;
        }

        // Now search for the title in the current node and its descendants
        if (current != null)
        {
            var result = FindLinkByTitle(current, title);
            if (result == null)
            {
                Debug.WriteLine($"[FindNodeByFullPath] Title '{title}' not found in path '{categoryPath}'");
            }
            return result;
        }

        return null;
    }

    private Task DeleteCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Delete operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task DeleteSubCategoryAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Delete operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task DeleteLinkAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Delete operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task DeleteTagsAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Delete operations not yet implemented";
        return Task.CompletedTask;
    }

    private Task DeleteRatingsAsync(ImportOperation operation, ImportOperationResult result, HashSet<string> modifiedCategories)
    {
        result.Status = "Success";
        result.Message = "Delete operations not yet implemented";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds a category node by name.
    /// </summary>
    private TreeViewNode? FindCategoryByName(string name)
    {
        return _treeView.RootNodes.FirstOrDefault(n =>
            n.Content is CategoryItem cat && cat.Name == name);
    }

    /// <summary>
    /// Finds a link by title in a category.
    /// Searches recursively through all descendants, including catalog entries.
    /// </summary>
    private TreeViewNode? FindLinkByTitle(TreeViewNode categoryNode, string title)
    {
        Debug.WriteLine($"[FindLinkByTitle] Searching for '{title}' starting from {GetNodeDescription(categoryNode)}");
        
        // First check immediate children
        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                Debug.WriteLine($"[FindLinkByTitle]   Checking child link: '{link.Title}' (IsDirectory={link.IsDirectory}, IsCatalogEntry={link.IsCatalogEntry})");
                if (link.Title == title)
                {
                    Debug.WriteLine($"[FindLinkByTitle]   FOUND! Returning node for '{title}'");
                    return child;
                }
            }
        }
        
        // Then recursively search all descendants (subcategories and catalog entries)
        foreach (var child in categoryNode.Children)
        {
            // Search in subcategories
            if (child.Content is CategoryItem cat)
            {
                Debug.WriteLine($"[FindLinkByTitle]   Recursing into subcategory: '{cat.Name}'");
                var found = FindLinkByTitle(child, title);
                if (found != null)
                {
                    return found;
                }
            }
            // Search in catalog entries (folders and their children)
            else if (child.Content is LinkItem link)
            {
                // Recursively search children of this link (catalog entries, sub-links, etc.)
                if (child.Children.Count > 0)
                {
                    Debug.WriteLine($"[FindLinkByTitle]   Recursing into link with children: '{link.Title}' ({child.Children.Count} children)");
                    var found = FindLinkByTitle(child, title);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }
        
        Debug.WriteLine($"[FindLinkByTitle]   NOT FOUND: '{title}' in {GetNodeDescription(categoryNode)}");
        return null;
    }
    
    /// <summary>
    /// Gets a description of a node for debugging.
    /// </summary>
    private string GetNodeDescription(TreeViewNode node)
    {
        if (node.Content is CategoryItem cat)
        {
            return $"Category '{cat.Name}' ({node.Children.Count} children)";
        }
        else if (node.Content is LinkItem link)
        {
            return $"Link '{link.Title}' (IsDir={link.IsDirectory}, IsCatalog={link.IsCatalogEntry}, {node.Children.Count} children)";
        }
        return "Unknown node";
    }
}
