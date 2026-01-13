# MyMemories Import System - Documentation Summary

## Overview

The MyMemories import system provides a powerful way to perform batch operations on categories, links, and metadata using JSON files. This system extends the standard category JSON format with operation directives.

## Documentation Files

### Core Documentation

| File | Purpose | Audience |
|------|---------|----------|
| **[category-import-format.md](category-import-format.md)** | Complete format specification | Developers, Power Users |
| **[category-import-quick-reference.md](category-import-quick-reference.md)** | Quick syntax reference | All Users |
| **[category-json-format.md](category-json-format.md)** | Standard category format | Reference |

### Examples

| File | Purpose | Use Case |
|------|---------|----------|
| **[import-add-resources.json](examples/import-add-resources.json)** | Add new items | Expanding collection |
| **[import-bulk-update.json](examples/import-bulk-update.json)** | Update existing items | Maintenance |
| **[import-cleanup.json](examples/import-cleanup.json)** | Delete old items | Cleanup |
| **[import-mixed-operations.json](examples/import-mixed-operations.json)** | All operation types | Real-world scenario |
| **[import-examples-README.md](examples/import-examples-README.md)** | Examples guide | Learning |

---

## Key Features

### ? Supported Operations

| Operation | Category | SubCategory | Link | Tags | Ratings |
|-----------|----------|-------------|------|------|---------|
| **Add** | ? | ? | ? | ? | ? |
| **Update** | ? | ? | ? | ? | ? |
| **Delete** | ? | ? | ? | ? | ? |

### ?? Operation Options

- **Merge Mode**: Combine new data with existing instead of replacing
- **Skip/Update**: Handle duplicates intelligently
- **Backup**: Automatic backup before destructive operations
- **Validation**: Pre-import validation of all operations
- **Batch Processing**: Process multiple operations in sequence

### ?? Result Reporting

- Success/failure count for each operation
- Detailed error messages
- Modified categories list
- Import duration tracking

---

## Getting Started

### 1. Choose Your Starting Point

**New User?** Start with:
1. [category-import-quick-reference.md](category-import-quick-reference.md) - Learn basic syntax
2. [examples/import-add-resources.json](examples/import-add-resources.json) - See simple examples

**Power User?** Go to:
1. [category-import-format.md](category-import-format.md) - Complete specification
2. [examples/import-mixed-operations.json](examples/import-mixed-operations.json) - Complex scenarios

### 2. Create Your Import File

Use the examples as templates:

```json
{
  "Version": "1.0",
  "Description": "My import description",
  "ImportDate": "2024-12-21T10:00:00",
  "Operations": [
    // Your operations here
  ]
}
```

### 3. Test with Small Batch

Start with 1-2 operations to verify:
- Paths are correct
- Category names match
- Tags exist (or use `CreateTagsIfMissing`)

### 4. Backup Your Data

Always backup before importing:
- Use MyMemories backup feature
- Or copy category JSON files manually

### 5. Run the Import

1. Open MyMemories
2. Go to **File > Import > Category Operations**
3. Select your JSON file
4. Review the preview
5. Execute and review results

---

## Common Use Cases

### ?? Browser Bookmark Import
```json
{
  "Operations": [
    {"Operation": "Add", "Target": "Category", "Data": {"Name": "Bookmarks"}},
    {"Operation": "Add", "Target": "Link", /* ... */}
  ]
}
```

### ?? Bulk URL Updates
```json
{
  "Operations": [
    {"Operation": "Update", "Target": "Link", 
     "Options": {"UpdateUrlStatus": true}}
  ]
}
```

### ??? Tag Management
```json
{
  "Operations": [
    {"Operation": "Add", "Target": "Tag",
     "Options": {"CreateTagsIfMissing": true}}
  ]
}
```

### ?? Cleanup Operations
```json
{
  "Operations": [
    {"Operation": "Delete", "Target": "Link",
     "Options": {"BackupBeforeDelete": true}}
  ]
}
```

### ? Rating Updates
```json
{
  "Operations": [
    {"Operation": "Update", "Target": "Rating",
     "Data": {"Ratings": [{"Name": "Quality", "Value": 5}]}}
  ]
}
```

---

## Decision Tree

### "Should I use Add, Update, or Delete?"

```
Do you want to create something new?
?? YES ? Use "Add"
?  ?? Already exists?
?     ?? Skip it ? Option: SkipIfExists: true
?     ?? Update it ? Option: UpdateIfExists: true
?     ?? Merge it ? Option: MergeIfExists: true
?
?? NO ? Modify existing data?
?  ?? YES ? Use "Update"
?  ?  ?? Add to existing?
?  ?     ?? YES ? Option: MergeTagIds/MergeRatings: true
?  ?
?  ?? NO ? Remove data?
?     ?? YES ? Use "Delete"
?        ?? Create backup? ? Option: BackupBeforeDelete: true
```

---

## Format Comparison

### Standard Category Format
```json
{
  "Name": "Category",
  "Links": [
    {"Title": "Link", "Url": "..."}
  ]
}
```

### Import Format
```json
{
  "Version": "1.0",
  "Operations": [
    {
      "Operation": "Add",
      "Target": "Link",
      "Identifier": {"CategoryPath": "Category"},
      "Data": {"Title": "Link", "Url": "..."}
    }
  ]
}
```

**Key Difference**: Import format includes operation directives and identifiers for targeting specific items.

---

## Operation Sequencing

### ? Correct Order
```json
{
  "Operations": [
    {"Operation": "Add", "Target": "Category"},
    {"Operation": "Add", "Target": "SubCategory"},
    {"Operation": "Add", "Target": "Link"}
  ]
}
```

### ? Wrong Order
```json
{
  "Operations": [
    {"Operation": "Add", "Target": "Link"},  // Will fail if category doesn't exist
    {"Operation": "Add", "Target": "Category"}
  ]
}
```

---

## Advanced Features

### 1. Conditional Operations
Use options to control behavior:
```json
"Options": {
  "SkipIfExists": true,    // Skip if already exists
  "UpdateIfExists": true,   // Update if already exists
  "MergeIfExists": true     // Merge with existing
}
```

### 2. Metadata Preservation
```json
"Options": {
  "PreserveTimestamps": true,  // Don't update dates
  "MergeTagIds": true,         // Add to existing tags
  "MergeRatings": true         // Merge with existing ratings
}
```

### 3. Safety Features
```json
"Options": {
  "BackupBeforeDelete": true,   // Auto-backup
  "Recursive": false            // Don't delete children
}
```

### 4. Auto-Creation
```json
"Options": {
  "CreateTagsIfMissing": true,  // Create tag definitions
  "UpdateUrlStatus": true       // Check URL after update
}
```

---

## Validation Rules

The import system validates:

| Check | Description |
|-------|-------------|
| ? JSON Syntax | Valid JSON structure |
| ? Version | Compatible format version |
| ? Required Fields | All required fields present |
| ? Enum Values | Valid SortOption, FolderType, etc. |
| ? Path Format | Forward slashes, no invalid chars |
| ? Item Existence | Referenced items exist (for update/delete) |

---

## Error Recovery Workflow

1. **Import Fails**
   ```
   Review ? OperationResults ? Identify failed operations
   ```

2. **Fix Issues**
   ```
   Correct paths/names ? Add missing fields ? Update references
   ```

3. **Extract Failed Operations**
   ```json
   {
     "Operations": [
       // Copy failed operations here
     ]
   }
   ```

4. **Re-run**
   ```
   Test fixed operations ? Run import ? Verify results
   ```

---

## Performance Tips

### ? Optimize Large Imports

1. **Batch Operations**: Group similar operations together
2. **Sequential Processing**: Dependencies are resolved in order
3. **Skip Checks**: Use `SkipIfExists` to avoid duplicate checks
4. **Minimal Options**: Only use options when needed

### ? Avoid

1. **Redundant Operations**: Don't update unchanged values
2. **Deep Recursion**: Limit nesting depth for deletes
3. **Excessive URL Checks**: Set `UpdateUrlStatus: false` for bulk imports
4. **Large Transactions**: Break very large imports into smaller files

---

## Security & Safety

### ?? Security

- Password-protected categories must be unlocked before import
- Import operations respect audit logging settings
- All operations are logged if audit logging is enabled

### ??? Safety

- Automatic backups before deletes (when enabled)
- Validation before execution
- Non-transactional (continue on failure)
- Detailed result reporting

---

## Next Steps

### For Beginners
1. ? Read [Quick Reference](category-import-quick-reference.md)
2. ? Try [Add Resources Example](examples/import-add-resources.json)
3. ? Create your first import file
4. ? Test with 1-2 operations

### For Advanced Users
1. ? Study [Complete Format](category-import-format.md)
2. ? Review [Mixed Operations Example](examples/import-mixed-operations.json)
3. ? Create complex workflows
4. ? Automate regular imports

---

## Support Resources

| Resource | Location |
|----------|----------|
| Format Spec | [category-import-format.md](category-import-format.md) |
| Quick Reference | [category-import-quick-reference.md](category-import-quick-reference.md) |
| Examples | [examples/](examples/) directory |
| Standard Format | [category-json-format.md](category-json-format.md) |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024-12-21 | Initial release with full Add/Update/Delete support |

---

## Feedback

For questions, issues, or suggestions about the import format:
1. Review the documentation
2. Check example files
3. Test with small batches
4. Examine error messages in results

Remember: **Always backup your data before importing!** ??
