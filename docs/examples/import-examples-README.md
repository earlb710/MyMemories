# MyMemories JSON Examples

This directory contains example JSON files demonstrating various features and use cases for MyMemories.

## Category Examples

### `minimal-category.json`
**Purpose:** Simplest possible category with just links  
**Features:**
- Basic category structure
- Simple links with URLs
- No subcategories, tags, or ratings
- Use this as a starting template

### `sample-category.json`
**Purpose:** Comprehensive example showing most features  
**Features:**
- Nested subcategories
- Tags on categories and links
- Sub-links (hierarchical links)
- URL status tracking
- Bookmark category configuration
- Audit logging enabled
- Mixed content (URLs, local files)

### `password-protected-category.json`
**Purpose:** Demonstrates password protection  
**Features:**
- Own password protection (not global)
- Password hash storage
- Sensitive file paths (local directories)
- Audit logging for security
- Descending date sort order

### `complete-category-example.json` ? NEW
**Purpose:** Shows ALL current features including ratings  
**Features:**
- **Ratings on categories** (Quality, Organization, etc.)
- **Ratings on links** with reasons
- **Ratings on subcategories**
- **Ratings on catalog entries**
- Backup directories configuration
- Cataloged directory with file filters
- Auto-refresh catalog
- Complete URL status information
- Multiple tag assignments
- All optional fields demonstrated

## Import Examples

### `import-template.json`
**Purpose:** Complete template with all import operations  
**Features:**
- Add, update, move, and delete operations
- All available update flags explained
- Comments documenting each section
- Examples of conditional operations

### `import-ratings-from-mymemories.json`
**Purpose:** Bulk rating assignment example  
**Features:**
- Real-world rating assignments
- Multiple links rated at once
- MergeRatings flag demonstration

### `import-ratings-with-reasons.json`
**Purpose:** Detailed ratings with explanations  
**Features:**
- Ratings with Reason field
- Code quality assessments
- Detailed justifications

### `import-bulk-update.json`
**Purpose:** Mass update operations  
**Features:**
- Updating multiple items
- Tag management
- Description updates

### `import-cleanup.json`
**Purpose:** Maintenance and cleanup operations  
**Features:**
- Removing outdated items
- Tag cleanup
- Moving items to new categories

## Key Differences from Older Examples

### ? Updated Features (December 2024)
- **Ratings Support:** Categories, links, and catalog entries can now have ratings
- **Emoji Icons:** Using actual emojis (??????) instead of placeholder "??"
- **CategoryPath Removed from Links:** No longer needed in JSON (auto-calculated)
- **Backup Directories:** New [MANUAL] and [AUTO] prefix support
- **Enhanced Catalog:** FileFilters, AutoRefreshCatalog, CatalogSortOrder fields

### ?? Deprecated/Changed
- **`CategoryPath` in LinkData:** Still supported but optional (auto-calculated from tree structure)
- **Icon placeholder "??":** Replace with actual emoji or omit for default ??

## Using These Examples

### Loading a Category
1. Use **File ? Open Category...** or the **Open** toolbar button
2. Select any `.json` file
3. File will be copied to: `%LOCALAPPDATA%\MyMemories\Categories`
4. Category appears in tree with all content

### Creating from Template
1. Copy `minimal-category.json`
2. Edit Name, Icon, and Links
3. Save as `YourCategoryName.json`
4. Load via Open Category

### Import Operations
1. Create category operations JSON
2. Use **File ? Import Category Operations...**
3. Review changes in preview
4. Apply operations

## Field Reference

### Required Fields
- `Name` - Category name (must be unique)
- `CreatedDate` - ISO 8601 datetime
- `ModifiedDate` - ISO 8601 datetime

### Common Optional Fields
- `Description` - Category description text
- `Icon` - Emoji or icon character
- `Keywords` - Space-separated search keywords
- `TagIds` - Array of tag IDs (e.g., ["tag-work", "tag-important"])
- `Ratings` - Array of rating objects with Rating, Score, Reason
- `BackupDirectories` - Paths with [MANUAL] or [AUTO] prefix

### Link Fields
- `Title` - Link display name (required)
- `Url` - URL or file path (required)
- `Description` - Link description
- `Keywords` - Search keywords
- `TagIds` - Array of tag IDs
- `Ratings` - Array of rating objects
- `IsDirectory` - true for folder links
- `SubLinks` - Array of nested links
- `UrlStatus` - "Accessible", "Inaccessible", "Warning", etc.
- `UrlLastChecked` - Last check timestamp
- `FileSize` - File size in bytes

### Catalog Fields (for `IsDirectory: true`)
- `FolderType` - "None", "LinkOnly", "CatalogueFiles"
- `FileFilters` - Semicolon-separated patterns (e.g., "*.cs;*.json")
- `AutoRefreshCatalog` - true to auto-update
- `CatalogSortOrder` - Sort order for entries
- `LastCatalogUpdate` - Last catalog update time
- `CatalogEntries` - Array of cataloged files

## Version History

- **v1.3** (Dec 2024) - Added ratings support, emoji icons, complete example
- **v1.2** (Nov 2024) - Added backup directories, catalog enhancements
- **v1.1** (Oct 2024) - Added URL status tracking
- **v1.0** (Sep 2024) - Initial examples

## Need Help?

- See `category-json-format.md` for complete schema documentation
- Check `IMPORT-SYSTEM-README.md` for import operations guide
- Review `category-import-quick-reference.md` for quick tips


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
