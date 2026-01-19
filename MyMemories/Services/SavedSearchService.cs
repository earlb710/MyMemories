using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for managing saved searches.
/// </summary>
public class SavedSearchService
{
    private readonly string _dataFolder;
    private const string SearchesFileName = "SavedSearches.json";
    private List<SavedSearch> _searches = new();
    
    private static SavedSearchService? _instance;
    public static SavedSearchService? Instance => _instance;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    public SavedSearchService(string dataFolder)
    {
        _dataFolder = dataFolder;
        _instance = this;
    }
    
    /// <summary>
    /// Gets all saved searches.
    /// </summary>
    public IReadOnlyList<SavedSearch> Searches => _searches.AsReadOnly();
    
    /// <summary>
    /// Loads saved searches from JSON file.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var filePath = Path.Combine(_dataFolder, SearchesFileName);
            if (!File.Exists(filePath))
            {
                _searches = new List<SavedSearch>();
                return;
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<SavedSearchesData>(json, _jsonOptions);
            _searches = data?.Searches ?? new List<SavedSearch>();
            
            System.Diagnostics.Debug.WriteLine($"[SavedSearchService] Loaded {_searches.Count} saved searches");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("SavedSearchService.LoadAsync", "Error loading saved searches", ex);
            _searches = new List<SavedSearch>();
        }
    }
    
    /// <summary>
    /// Saves all searches to JSON file.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var data = new SavedSearchesData
            {
                Searches = _searches,
                LastModified = DateTime.Now
            };
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var filePath = Path.Combine(_dataFolder, SearchesFileName);
            
            Directory.CreateDirectory(_dataFolder);
            await File.WriteAllTextAsync(filePath, json);
            
            System.Diagnostics.Debug.WriteLine($"[SavedSearchService] Saved {_searches.Count} searches");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("SavedSearchService.SaveAsync", "Error saving searches", ex);
        }
    }
    
    
    /// <summary>
    /// Adds a new saved search.
    /// </summary>
    public async Task AddSearchAsync(SavedSearch search)
    {
        search.CreatedDate = DateTime.Now;
        search.ModifiedDate = DateTime.Now;
        _searches.Add(search);
        await SaveAsync();
    }
    
    /// <summary>
    /// Updates an existing saved search.
    /// </summary>
    public async Task UpdateSearchAsync(SavedSearch search)
    {
        var existing = _searches.FirstOrDefault(s => s.Id == search.Id);
        if (existing != null)
        {
            var index = _searches.IndexOf(existing);
            search.ModifiedDate = DateTime.Now;
            _searches[index] = search;
            await SaveAsync();
        }
    }
    
    /// <summary>
    /// Deletes a saved search.
    /// </summary>
    public async Task DeleteSearchAsync(string searchId)
    {
        var search = _searches.FirstOrDefault(s => s.Id == searchId);
        if (search != null)
        {
            _searches.Remove(search);
            await SaveAsync();
        }
    }
    
    /// <summary>
    /// Gets a saved search by ID.
    /// </summary>
    public SavedSearch? GetSearch(string searchId)
    {
        return _searches.FirstOrDefault(s => s.Id == searchId);
    }
    
    /// <summary>
    /// Executes a saved search across all categories.
    /// </summary>
    public async Task<SearchExecutionResult> ExecuteSearchAsync(SavedSearch search, TreeView treeView)
    {
        var startTime = DateTime.Now;
        var results = new List<SearchResultItem>();
        
        // Traverse all nodes in the tree
        foreach (var rootNode in treeView.RootNodes)
        {
            SearchNodeRecursive(rootNode, search, results, string.Empty, isRootLevel: true);
        }
        
        
        var executionTime = DateTime.Now - startTime;
        
        // Update search statistics
        search.LastExecutedDate = DateTime.Now;
        search.LastResultCount = results.Count;
        await UpdateSearchAsync(search);
        
        return new SearchExecutionResult
        {
            Search = search,
            Results = results,
            ExecutedAt = DateTime.Now,
            ExecutionTime = executionTime
        };
    }
    
    
    private void SearchNodeRecursive(TreeViewNode node, SavedSearch search, List<SearchResultItem> results, string parentPath, bool isRootLevel = false)
    {
        if (node.Content is CategoryItem category)
        {
            // Skip special system nodes
            if (category.IsArchiveNode || category.IsSearchesNode)
                return;
            
            // Skip divider nodes
            if (category.Name.StartsWith("———"))
                return;
            
            // Check if this root category is in the included categories
            if (isRootLevel && search.IncludedCategories.Count > 0)
            {
                if (!search.IncludedCategories.Contains(category.Name))
                    return; // Skip this category tree entirely
            }
            
            var currentPath = string.IsNullOrEmpty(parentPath) 
                ? category.Name 
                : $"{parentPath} > {category.Name}";
            
            // Check if category matches
            if (MatchesSearch(category, null, search, currentPath))
            {
                results.Add(new SearchResultItem
                {
                    ItemType = "Category",
                    Name = category.Name,
                    Description = category.Description,
                    Icon = category.Icon ?? "\U0001F4C1",
                    CategoryPath = currentPath,
                    Category = category,
                    Node = node
                });
            }
            
            // Search children
            foreach (var child in node.Children)
            {
                SearchNodeRecursive(child, search, results, currentPath, isRootLevel: false);
            }
        }
        else if (node.Content is LinkItem link)
        {
            // Skip saved search nodes when searching
            if (link.IsSavedSearch)
                return;
            
            // Check if link matches
            if (MatchesSearch(null, link, search, parentPath))
            {
                results.Add(new SearchResultItem
                {
                    ItemType = link.IsDirectory ? "Directory" : (link.Url?.StartsWith("http") == true ? "URL" : "File"),
                    Name = link.Title,
                    Description = link.Description,
                    Icon = link.IconWithoutBadge,
                    CategoryPath = parentPath,
                    Link = link,
                    Node = node
                });
            }
            
            // Also search through children (catalog entries, sub-links, etc.)
            if (node.Children.Count > 0)
            {
                var childPath = $"{parentPath} > {link.Title}";
                foreach (var child in node.Children)
                {
                    SearchNodeRecursive(child, search, results, childPath, isRootLevel: false);
                }
            }
        }
    }
    
    /// <summary>
    /// Checks if an item matches the saved search criteria.
    /// </summary>
    private bool MatchesSearch(CategoryItem? category, LinkItem? link, SavedSearch search, string categoryPath)
    {
        if (search.ConditionGroups.Count == 0)
            return false;
        
        bool? overallResult = null;
        
        foreach (var group in search.ConditionGroups)
        {
            var groupResult = EvaluateConditionGroup(category, link, group, categoryPath);
            
            if (group.IsNegated)
                groupResult = !groupResult;
            
            if (overallResult == null)
            {
                overallResult = groupResult;
            }
            else
            {
                overallResult = group.GroupOperator == LogicalOperator.And
                    ? overallResult.Value && groupResult
                    : overallResult.Value || groupResult;
            }
        }
        
        return overallResult ?? false;
    }
    
    private bool EvaluateConditionGroup(CategoryItem? category, LinkItem? link, SearchConditionGroup group, string categoryPath)
    {
        if (group.Conditions.Count == 0)
            return true;
        
        bool? groupResult = null;
        
        foreach (var condition in group.Conditions)
        {
            var conditionResult = EvaluateCondition(category, link, condition, categoryPath);
            
            if (condition.IsNegated)
                conditionResult = !conditionResult;
            
            if (groupResult == null)
            {
                groupResult = conditionResult;
            }
            else
            {
                groupResult = condition.ConditionOperator == LogicalOperator.And
                    ? groupResult.Value && conditionResult
                    : groupResult.Value || conditionResult;
            }
        }
        
        return groupResult ?? false;
    }
    
    private bool EvaluateCondition(CategoryItem? category, LinkItem? link, SearchCondition condition, string categoryPath)
    {
        var fieldValue = GetFieldValue(category, link, condition.Field, categoryPath);
        
        return condition.Operator switch
        {
            SearchOperator.Contains => ContainsIgnoreCase(fieldValue, condition.Value),
            SearchOperator.NotContains => !ContainsIgnoreCase(fieldValue, condition.Value),
            SearchOperator.Equals => string.Equals(fieldValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            SearchOperator.NotEquals => !string.Equals(fieldValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            SearchOperator.StartsWith => fieldValue?.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase) == true,
            SearchOperator.EndsWith => fieldValue?.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase) == true,
            SearchOperator.MatchesRegex => MatchesRegex(fieldValue, condition.Value),
            SearchOperator.HasValue => !string.IsNullOrEmpty(fieldValue),
            SearchOperator.IsEmpty => string.IsNullOrEmpty(fieldValue),
            SearchOperator.GreaterThan => CompareValues(fieldValue, condition.Value) > 0,
            SearchOperator.LessThan => CompareValues(fieldValue, condition.Value) < 0,
            SearchOperator.Between => IsBetween(fieldValue, condition.Value, condition.SecondaryValue),
            _ => false
        };
    }
    
    private string? GetFieldValue(CategoryItem? category, LinkItem? link, SearchField field, string categoryPath)
    {
        return field switch
        {
            SearchField.Any => GetAnyFieldValue(category, link, categoryPath),
            SearchField.Name => category?.Name ?? link?.Title,
            SearchField.Description => category?.Description ?? link?.Description,
            SearchField.Keywords => category?.Keywords ?? link?.Keywords,
            SearchField.Url => link?.Url,
            SearchField.Tag => string.Join(",", category?.Tags ?? link?.TagIds ?? new List<string>()),
            SearchField.Rating => string.Join(",", category?.Ratings?.Select(r => r.Rating) ?? link?.Ratings?.Select(r => r.Rating) ?? Enumerable.Empty<string>()),
            SearchField.RatingScore => (category?.AverageRating ?? link?.AverageRating)?.ToString("F1"),
            SearchField.DateCreated => (category?.CreatedDate ?? link?.CreatedDate)?.ToString("yyyy-MM-dd"),
            SearchField.DateModified => (category?.ModifiedDate ?? link?.ModifiedDate)?.ToString("yyyy-MM-dd"),
            SearchField.ItemType => category != null ? "Category" : (link?.IsDirectory == true ? "Directory" : (link?.Url?.StartsWith("http") == true ? "URL" : "File")),
            SearchField.FileExtension => GetFileExtension(link?.Url),
            SearchField.CategoryPath => categoryPath,
            _ => null
        };
    }
    
    private string GetAnyFieldValue(CategoryItem? category, LinkItem? link, string categoryPath)
    {
        var values = new List<string?>();
        
        if (category != null)
        {
            values.AddRange(new[] { category.Name, category.Description, category.Keywords });
            values.AddRange(category.Tags);
            values.AddRange(category.Ratings.Select(r => r.Rating));
        }
        
        if (link != null)
        {
            values.AddRange(new[] { link.Title, link.Description, link.Keywords, link.Url });
            values.AddRange(link.TagIds);
            values.AddRange(link.Ratings.Select(r => r.Rating));
        }
        
        values.Add(categoryPath);
        
        return string.Join(" ", values.Where(v => !string.IsNullOrEmpty(v)));
    }
    
    private static string? GetFileExtension(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        
        try
        {
            return Path.GetExtension(path)?.TrimStart('.');
        }
        catch
        {
            return null;
        }
    }
    
    private static bool ContainsIgnoreCase(string? source, string? value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return false;
        
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool MatchesRegex(string? source, string? pattern)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(pattern))
            return false;
        
        try
        {
            return Regex.IsMatch(source, pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
    
    private static int CompareValues(string? value1, string? value2)
    {
        // Try numeric comparison first
        if (double.TryParse(value1, out var num1) && double.TryParse(value2, out var num2))
        {
            return num1.CompareTo(num2);
        }
        
        // Try date comparison
        if (DateTime.TryParse(value1, out var date1) && DateTime.TryParse(value2, out var date2))
        {
            return date1.CompareTo(date2);
        }
        
        // Fall back to string comparison
        return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsBetween(string? value, string? min, string? max)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(min) || string.IsNullOrEmpty(max))
            return false;
        
        return CompareValues(value, min) >= 0 && CompareValues(value, max) <= 0;
    }
}
