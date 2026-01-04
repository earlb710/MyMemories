using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

/// <summary>
/// Service for managing category data persistence and operations.
/// </summary>
public class CategoryService
{
    private readonly string _dataFolder;
    private readonly JsonSerializerOptions _jsonOptions;

    public CategoryService(string dataFolder)
    {
        _dataFolder = dataFolder;
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        // Ensure data folder exists
        Directory.CreateDirectory(_dataFolder);
    }

    /// <summary>
    /// Loads all categories from JSON files.
    /// </summary>
    public async Task<List<TreeViewNode>> LoadAllCategoriesAsync()
    {
        var categories = new List<TreeViewNode>();

        if (!Directory.Exists(_dataFolder))
        {
            return categories;
        }

        var jsonFiles = Directory.GetFiles(_dataFolder, "*.json");

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(jsonFile);
                var categoryData = JsonSerializer.Deserialize<CategoryData>(json);

                if (categoryData != null)
                {
                    var categoryNode = CreateCategoryNode(categoryData);
                    categories.Add(categoryNode);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading {Path.GetFileName(jsonFile)}: {ex.Message}", ex);
            }
        }

        return categories;
    }

    /// <summary>
    /// Saves a category to a JSON file.
    /// </summary>
    public async Task SaveCategoryAsync(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem category)
        {
            throw new ArgumentException("Node must contain a CategoryItem", nameof(categoryNode));
        }

        var categoryData = ConvertNodeToCategoryData(categoryNode);

        var json = JsonSerializer.Serialize(categoryData, _jsonOptions);
        var fileName = SanitizeFileName(category.Name) + ".json";
        var filePath = Path.Combine(_dataFolder, fileName);
        
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Deletes a category file.
    /// </summary>
    public Task DeleteCategoryAsync(string categoryName)
    {
        var fileName = SanitizeFileName(categoryName) + ".json";
        var filePath = Path.Combine(_dataFolder, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Recursively converts a TreeViewNode to CategoryData including all subcategories and links.
    /// </summary>
    private CategoryData ConvertNodeToCategoryData(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is not CategoryItem category)
        {
            throw new ArgumentException("Node must contain a CategoryItem");
        }

        var categoryData = new CategoryData
        {
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            Links = new List<LinkData>(),
            SubCategories = new List<CategoryData>()
        };

        // Process all children
        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Add link
                categoryData.Links.Add(new LinkData
                {
                    Title = link.Title,
                    Url = link.Url,
                    Description = link.Description,
                    IsDirectory = link.IsDirectory,
                    CategoryPath = link.CategoryPath
                });
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively add subcategory
                var subCategoryData = ConvertNodeToCategoryData(child);
                categoryData.SubCategories.Add(subCategoryData);
            }
        }

        return categoryData;
    }

    /// <summary>
    /// Creates a TreeViewNode from CategoryData recursively.
    /// </summary>
    private TreeViewNode CreateCategoryNode(CategoryData categoryData)
    {
        var categoryNode = new TreeViewNode
        {
            Content = new CategoryItem
            {
                Name = categoryData.Name,
                Description = categoryData.Description,
                Icon = categoryData.Icon
            }
        };

        // Add subcategories first (they should appear before links)
        foreach (var subCategoryData in categoryData.SubCategories)
        {
            var subCategoryNode = CreateCategoryNode(subCategoryData);
            categoryNode.Children.Add(subCategoryNode);
        }

        // Then add links
        foreach (var linkData in categoryData.Links)
        {
            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = linkData.Title,
                    Url = linkData.Url,
                    Description = linkData.Description,
                    IsDirectory = linkData.IsDirectory,
                    CategoryPath = linkData.CategoryPath
                }
            };
            categoryNode.Children.Add(linkNode);
        }

        return categoryNode;
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}