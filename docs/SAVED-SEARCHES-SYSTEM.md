# Saved Searches System - MyMemories

**Version:** 1.0  
**Last Updated:** 2026-01-17

## ?? Overview

The Saved Searches system provides a way to create, save, and re-execute complex search queries in MyMemories. Searches can include multiple conditions combined with AND/OR/NOT logic, allowing for powerful filtering of categories and links.

---

## ? Features

### ?? Searches Node
- Located in the TreeView below all categories
- Has its own visual divider (separator line) above it
- **Blue "??" icon** for easy identification
- **Shows search count in brackets**: `Searches (5)` or `Searches (0)`
- **Persisted to JSON** (`SavedSearches.json`)

### ?? Search Conditions
- **Multiple Fields**: Search by Name, Description, Keywords, URL, Tags, Ratings, Dates, Item Type, File Extension, Category Path
- **Comparison Operators**: Contains, Equals, Starts With, Ends With, Regex, Greater Than, Less Than, Between, Has Value, Is Empty
- **Logical Operators**: AND, OR, NOT
- **Condition Groups**: Group conditions for complex logic

### ?? Persistence
- Searches stored in: `%LOCALAPPDATA%\MyMemories\Categories\SavedSearches.json`
- Auto-loads on startup
- Auto-saves on create/update/delete operations

---

## ?? How It Works

### Creating a Saved Search

```
1. Click "Searches" node in TreeView
2. Click "Add Search" button in details panel
3. Enter search name and description
4. Add condition groups and conditions
5. Click "Create"
6. Search appears under Searches node
```

### Search Condition Groups

- **Within a group**: Conditions are combined with AND or OR
- **Between groups**: Groups are combined with AND or OR
- **NOT modifier**: Can negate individual conditions or entire groups

Example:
```
Group 1 (AND):
  - Name Contains "important"
  - Tags Contains "work"

OR

Group 2 (NOT):
  - Item Type Equals "URL"
```

This finds items with "important" in the name AND "work" tag, OR items that are NOT URLs.

### Executing a Saved Search

```
1. Double-click a saved search
   OR
2. Select saved search and click "Run Search"
   OR
3. Right-click saved search ? "Run Search"
4. Results displayed in details panel
5. Click a result to navigate to the item
```

### Editing a Saved Search

```
1. Right-click saved search ? "Edit Search"
   OR
2. Select saved search and click "Edit"
3. Modify conditions
4. Click "Save"
```

### Deleting a Saved Search

```
1. Right-click saved search ? "Delete Search"
   OR
2. Select saved search and click "Delete"
3. Confirm deletion
```

---

## ?? Search Data Structure

### SavedSearches.json Format

```json
{
  "Searches": [
    {
      "Id": "unique-guid",
      "Name": "Important Documents",
      "Description": "Find all important documents",
      "Icon": "??",
      "ConditionGroups": [
        {
          "GroupOperator": "And",
          "IsNegated": false,
          "Conditions": [
            {
              "Field": "Name",
              "Operator": "Contains",
              "Value": "important",
              "ConditionOperator": "And",
              "IsNegated": false
            }
          ]
        }
      ],
      "CreatedDate": "2026-01-17T10:30:00",
      "ModifiedDate": "2026-01-17T10:30:00",
      "LastExecutedDate": "2026-01-17T11:00:00",
      "LastResultCount": 42
    }
  ],
  "LastModified": "2026-01-17T11:00:00"
}
```

---

## ?? Available Search Fields

| Field | Description | Example |
|-------|-------------|---------|
| `Any` | Searches all text fields | "project" |
| `Name` | Category/Link name | "Photos" |
| `Description` | Item description | "vacation" |
| `Keywords` | Keywords field | "travel" |
| `Url` | Link URL | "github.com" |
| `Tag` | Tag names | "Important" |
| `Rating` | Rating names | "Quality" |
| `RatingScore` | Rating score value | "8" |
| `DateCreated` | Creation date | "2026-01" |
| `DateModified` | Modified date | "2026-01-17" |
| `ItemType` | Category/Link/File/Directory/URL | "File" |
| `FileExtension` | File extension | "pdf" |
| `CategoryPath` | Full category path | "Work > Projects" |

---

## ?? Available Operators

| Operator | Description | Needs Value |
|----------|-------------|-------------|
| `Contains` | Text contains value (case-insensitive) | Yes |
| `NotContains` | Text does not contain value | Yes |
| `Equals` | Exact match (case-insensitive) | Yes |
| `NotEquals` | Not exact match | Yes |
| `StartsWith` | Text starts with value | Yes |
| `EndsWith` | Text ends with value | Yes |
| `MatchesRegex` | Matches regex pattern | Yes |
| `GreaterThan` | Numeric/date comparison | Yes |
| `LessThan` | Numeric/date comparison | Yes |
| `Between` | Value in range | Yes (two values) |
| `HasValue` | Field is not empty | No |
| `IsEmpty` | Field is empty | No |

---

## ??? UI Components

### Searches Node in TreeView
- Displayed after regular categories
- Has visual divider above it
- Blue colored icon and text
- Shows count of saved searches

### Saved Search Details Panel
When a saved search is selected:
- **Run Search** button - Execute the search
- **Edit** button - Modify the search
- **Delete** button - Remove the search
- Statistics (created, modified, last run)
- Visual display of search conditions

### Search Results Panel
After executing a search:
- Result count and execution time
- List of matching items with icons
- Click to navigate to item
- Item type badges (Category/File/URL/Directory)

---

## ?? Implementation Files

| File | Purpose |
|------|---------|
| `Models/SearchModels.cs` | SavedSearch, SearchCondition, SearchResultItem classes |
| `Services/SavedSearchService.cs` | Load, save, execute searches |
| `Dialogs/SavedSearchDialog.cs` | Create/edit search dialog UI |
| `MainWindow.Searches.cs` | UI integration and event handlers |
| `Services/DetailsViewService.cs` | Search results display |

---

## ??? Technical Notes

### Search Execution
- Searches traverse all nodes in the TreeView
- Skips Archive and Searches system nodes
- Skips divider nodes
- Evaluates conditions recursively
- Supports nested condition groups

### Performance
- Search results cached per execution
- Lazy evaluation of conditions
- Early termination on condition failures

### Integration Points
- Selection handling in `MainWindow.TreeView.cs`
- Context menu in `MainWindow.ContextMenu.Configuration.cs`
- Double-tap handling for search execution
- Color converter for blue Searches node display
