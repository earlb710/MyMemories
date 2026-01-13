# MyMemories Import Examples

This directory contains example import files demonstrating the category import format for MyMemories.

## Overview

The import format allows you to perform batch operations on categories, links, and subcategories using JSON files. Each import file contains a series of operations that can add, update, or delete items.

## Example Files

### 1. `import-add-resources.json`
**Purpose**: Add new categories, subcategories, and links

**Operations**:
- Creates a new "Learning Resources" category
- Adds two subcategories: "Video Courses" and "Interactive Platforms"
- Adds multiple learning platform links (Pluralsight, Udemy, YouTube, etc.)
- Updates an existing link with additional tags
- Adds tags and ratings to existing items

**Use Case**: Expanding your collection with new educational resources

---

### 2. `import-bulk-update.json`
**Purpose**: Update existing categories and links with new information

**Operations**:
- Updates category description and keywords
- Updates multiple link descriptions to include version information
- Adds tags to existing links using `MergeTagIds` option
- Updates ratings for documentation links
- Refreshes URL status for web links

**Use Case**: Maintaining and updating existing category information

---

### 3. `import-cleanup.json`
**Purpose**: Remove outdated links and clean up old data

**Operations**:
- Deletes broken or obsolete links
- Removes outdated tags from items
- Deletes unused ratings
- Removes entire subcategories with all their contents
- Creates backups before deleting (using `BackupBeforeDelete` option)

**Use Case**: Regular maintenance to keep categories clean and current

---

### 4. `import-mixed-operations.json`
**Purpose**: Comprehensive example showing all operation types

**Operations**:
- **Add**: New "Quick Links" category with frequently used websites
- **Add**: New "Docker Resources" subcategory with container documentation
- **Update**: Enable audit logging for a category
- **Update**: Enable auto-refresh for a catalog
- **Delete**: Remove old, outdated links
- **Tag Operations**: Add tags to categories and create new tag definitions
- **Rating Operations**: Rate items for usefulness and frequency

**Use Case**: Real-world scenario combining multiple operation types in a single import

---

## How to Use These Examples

### 1. Review the Example
Open the JSON file and review the operations to understand what will be changed.

### 2. Customize for Your Data
Modify the example to match your actual category names, paths, and data:
- Change `"CategoryPath"` values to match your categories
- Update link titles and URLs
- Adjust tag IDs to match your tag system

### 3. Test with Small Batches
Start with a small subset of operations to test:
```json
{
  "Version": "1.0",
  "Operations": [
    // Include only 1-2 operations for testing
  ]
}
```

### 4. Backup Before Import
Always backup your categories before importing:
- Use the backup feature in MyMemories
- Or manually copy category JSON files from the data directory

### 5. Run the Import
Use the import feature in MyMemories to process the file:
1. Go to **File > Import > Category Operations**
2. Select your import JSON file
3. Review the preview of operations
4. Confirm and execute the import

### 6. Review the Results
Check the import results dialog:
- **Successful**: Operations completed without issues
- **Failed**: Operations that encountered errors (with error messages)
- **Skipped**: Operations skipped due to conditions (e.g., `SkipIfExists`)

---

## Common Patterns

### Adding Multiple Links to a Category
```json
{
  "Operations": [
    {
      "Operation": "Add",
      "Target": "Link",
      "Identifier": {"CategoryPath": "Category Name"},
      "Data": {"Title": "Link 1", "Url": "https://example1.com"}
    },
    {
      "Operation": "Add",
      "Target": "Link",
      "Identifier": {"CategoryPath": "Category Name"},
      "Data": {"Title": "Link 2", "Url": "https://example2.com"}
    }
  ]
}
```

### Updating Tags Across Multiple Items
```json
{
  "Operations": [
    {
      "Operation": "Add",
      "Target": "Tag",
      "Identifier": {"CategoryPath": "Category", "Title": "Link 1"},
      "Data": {"TagIds": ["tag-new"]},
      "Options": {"CreateTagsIfMissing": true}
    },
    {
      "Operation": "Add",
      "Target": "Tag",
      "Identifier": {"CategoryPath": "Category", "Title": "Link 2"},
      "Data": {"TagIds": ["tag-new"]},
      "Options": {"CreateTagsIfMissing": true}
    }
  ]
}
```

### Batch Rating Updates
```json
{
  "Operations": [
    {
      "Operation": "Update",
      "Target": "Rating",
      "Identifier": {"CategoryPath": "Category", "Title": "Link 1"},
      "Data": {
        "Ratings": [
          {"Name": "Quality", "Value": 5},
          {"Name": "Usefulness", "Value": 4}
        ]
      }
    }
  ]
}
```

---

## Best Practices from Examples

### 1. Use Descriptive Import Descriptions
```json
{
  "Description": "Import Q4 2024 bookmarks from Chrome",
  "ImportDate": "2024-12-21T10:00:00"
}
```

### 2. Order Operations Logically
- Add categories before subcategories
- Add subcategories before links
- Add items before updating or deleting them

### 3. Use Options Wisely
- `SkipIfExists: true` - Prevents duplicate entries
- `MergeTagIds: true` - Adds to existing tags instead of replacing
- `UpdateUrlStatus: true` - Checks URL accessibility after update
- `BackupBeforeDelete: true` - Creates safety backup

### 4. Handle Errors Gracefully
- Import continues even if individual operations fail
- Check the results for failed operations
- Fix issues and re-run failed operations

### 5. Test Before Production
- Start with read-only operations (if possible)
- Test with a copy of your data
- Use small batches for testing

---

## Validation Before Import

The import system validates:
- ? JSON syntax and structure
- ? Required fields presence
- ? Valid enum values (SortOption, FolderLinkType, etc.)
- ? Path format (forward slashes)
- ? Category and link existence (for updates/deletes)

---

## Error Recovery

If an import fails:

1. **Check the Results**
   - Review the `OperationResults` for error messages
   - Identify which operations failed and why

2. **Fix the Issues**
   - Correct invalid paths or names
   - Ensure referenced items exist
   - Add missing required fields

3. **Re-run Failed Operations**
   - Extract failed operations into a new import file
   - Fix the issues
   - Run the corrected import

4. **Restore from Backup** (if needed)
   - Use the backup created before import
   - Or restore from category JSON files

---

## Advanced Scenarios

### Migrating from Another System
Create an import file that:
1. Adds new categories for organization
2. Imports bookmarks from export file
3. Adds tags for categorization
4. Rates items based on usage frequency

### Regular Maintenance
Schedule regular imports that:
1. Update descriptions with current information
2. Check and update URL statuses
3. Remove broken or outdated links
4. Add new tags for better organization

### Bulk Metadata Updates
Update multiple items with:
1. Consistent tag schemas
2. Rating criteria
3. Updated keywords
4. Revised descriptions

---

## Related Documentation

- [Category Import Format](../category-import-format.md) - Complete format specification
- [Category JSON Format](../category-json-format.md) - Standard category format
- [Tag Management](../tags.md) - Working with tags
- [Rating System](../ratings.md) - Rating definitions

---

## Support

For questions or issues with the import format:
1. Check the documentation
2. Review example files
3. Test with small batches
4. Check error messages in results

Remember to always backup your data before performing imports!
