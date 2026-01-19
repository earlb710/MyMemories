using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyMemories;

/// <summary>
/// Represents a saved search with multiple conditions combined with AND/OR/NOT operators.
/// </summary>
public class SavedSearch
{
    /// <summary>
    /// Unique identifier for the search.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Display name for the saved search.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this search finds.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon for the search (emoji or glyph).
    /// </summary>
    public string Icon { get; set; } = "\U0001F50E"; // ?? Right-Pointing Magnifying Glass
    
    /// <summary>
    /// List of category names to include in the search.
    /// If empty, searches all categories.
    /// </summary>
    public List<string> IncludedCategories { get; set; } = new();
    
    /// <summary>
    /// The search conditions that make up this saved search.
    /// </summary>
    public List<SearchConditionGroup> ConditionGroups { get; set; } = new();
    
    /// <summary>
    /// Date when this search was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Date when this search was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Date when this search was last executed.
    /// </summary>
    public DateTime? LastExecutedDate { get; set; }
    
    /// <summary>
    /// Number of results from the last execution.
    /// </summary>
    public int? LastResultCount { get; set; }
    
    public override string ToString() => $"{Icon} {Name}";
}

/// <summary>
/// A group of search conditions combined with AND/OR logic.
/// Groups are combined with AND, conditions within a group are combined with OR.
/// </summary>
public class SearchConditionGroup
{
    /// <summary>
    /// How this group combines with previous groups (AND, OR).
    /// </summary>
    public LogicalOperator GroupOperator { get; set; } = LogicalOperator.And;
    
    /// <summary>
    /// Whether to negate this entire group (NOT).
    /// </summary>
    public bool IsNegated { get; set; }
    
    /// <summary>
    /// The conditions within this group.
    /// </summary>
    public List<SearchCondition> Conditions { get; set; } = new();
}

/// <summary>
/// A single search condition.
/// </summary>
public class SearchCondition
{
    /// <summary>
    /// The field to search in.
    /// </summary>
    public SearchField Field { get; set; } = SearchField.Any;
    
    /// <summary>
    /// The comparison operator.
    /// </summary>
    public SearchOperator Operator { get; set; } = SearchOperator.Contains;
    
    /// <summary>
    /// The value to search for.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Secondary value for range comparisons.
    /// </summary>
    public string? SecondaryValue { get; set; }
    
    /// <summary>
    /// How this condition combines with the previous condition in the same group.
    /// </summary>
    public LogicalOperator ConditionOperator { get; set; } = LogicalOperator.And;
    
    /// <summary>
    /// Whether to negate this condition (NOT).
    /// </summary>
    public bool IsNegated { get; set; }
}

/// <summary>
/// Logical operators for combining conditions.
/// </summary>
public enum LogicalOperator
{
    And,
    Or
}

/// <summary>
/// Fields that can be searched.
/// </summary>
public enum SearchField
{
    /// <summary>Searches all text fields.</summary>
    Any,
    
    /// <summary>Searches category/link names.</summary>
    Name,
    
    /// <summary>Searches descriptions.</summary>
    Description,
    
    /// <summary>Searches keywords.</summary>
    Keywords,
    
    /// <summary>Searches URLs.</summary>
    Url,
    
    /// <summary>Searches by tag name.</summary>
    Tag,
    
    /// <summary>Searches by rating name.</summary>
    Rating,
    
    /// <summary>Searches by rating score.</summary>
    RatingScore,
    
    /// <summary>Searches by date created.</summary>
    DateCreated,
    
    /// <summary>Searches by date modified.</summary>
    DateModified,
    
    /// <summary>Searches by item type (Category, Link, File, Directory, URL).</summary>
    ItemType,
    
    /// <summary>Searches by file extension.</summary>
    FileExtension,
    
    /// <summary>Searches by category path.</summary>
    CategoryPath
}

/// <summary>
/// Operators for search comparisons.
/// </summary>
public enum SearchOperator
{
    /// <summary>Field contains the value (case-insensitive).</summary>
    Contains,
    
    /// <summary>Field does not contain the value.</summary>
    NotContains,
    
    /// <summary>Field exactly equals the value.</summary>
    Equals,
    
    /// <summary>Field does not equal the value.</summary>
    NotEquals,
    
    /// <summary>Field starts with the value.</summary>
    StartsWith,
    
    /// <summary>Field ends with the value.</summary>
    EndsWith,
    
    /// <summary>Field matches regex pattern.</summary>
    MatchesRegex,
    
    /// <summary>For numeric/date comparisons - greater than.</summary>
    GreaterThan,
    
    /// <summary>For numeric/date comparisons - less than.</summary>
    LessThan,
    
    /// <summary>For numeric/date comparisons - between two values.</summary>
    Between,
    
    /// <summary>Field has any value (is not empty).</summary>
    HasValue,
    
    /// <summary>Field has no value (is empty).</summary>
    IsEmpty
}

/// <summary>
/// Item types for type-based filtering.
/// </summary>
public enum SearchItemType
{
    Category,
    Link,
    File,
    Directory,
    Url
}

/// <summary>
/// Result of executing a saved search.
/// </summary>
public class SearchExecutionResult
{
    /// <summary>
    /// The saved search that was executed.
    /// </summary>
    public SavedSearch Search { get; set; } = null!;
    
    /// <summary>
    /// The matching items found.
    /// </summary>
    public List<SearchResultItem> Results { get; set; } = new();
    
    /// <summary>
    /// When the search was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Time taken to execute the search.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// A single item in the search results.
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// The type of item (Category or Link).
    /// </summary>
    public string ItemType { get; set; } = string.Empty;
    
    /// <summary>
    /// The name/title of the item.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of the item.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The icon of the item.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
    
    /// <summary>
    /// The category path where this item is located.
    /// </summary>
    public string CategoryPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to the original CategoryItem if this is a category.
    /// </summary>
    [JsonIgnore]
    public CategoryItem? Category { get; set; }
    
    /// <summary>
    /// Reference to the original LinkItem if this is a link.
    /// </summary>
    [JsonIgnore]
    public LinkItem? Link { get; set; }
    
    /// <summary>
    /// Reference to the tree node for navigation.
    /// </summary>
    [JsonIgnore]
    public Microsoft.UI.Xaml.Controls.TreeViewNode? Node { get; set; }
}

/// <summary>
/// Data structure for persisting saved searches.
/// </summary>
public class SavedSearchesData
{
    /// <summary>
    /// List of saved searches.
    /// </summary>
    public List<SavedSearch> Searches { get; set; } = new();
    
    /// <summary>
    /// Last modified date.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;
}
