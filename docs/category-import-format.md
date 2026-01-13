# MyMemories Category Import Format

This document describes the JSON import format for batch operations on MyMemories categories. The import format extends the standard category JSON format with operation directives to support add, update, and delete operations.

## Overview

The import format allows you to:
- **Add** new categories, links, and subcategories
- **Update** existing categories and links (by ID or path)
- **Delete** categories, links, tags, and ratings
- **Perform batch operations** across multiple categories in a single file

---

## File Structure

The import file is a JSON array of operation objects. Each operation specifies:
1. The **operation type** (Add, Update, or Delete)
2. The **target** (Category, Link, SubCategory, Tag, Rating)
3. The **data** or **identifiers** for the operation

```json
{
  "Version": "1.0",
  "Description": "Optional description of this import file",
  "ImportDate": "2024-12-21T10:00:00",
  "Operations": [
    {
      "Operation": "Add|Update|Delete",
      "Target": "Category|Link|SubCategory|Tag|Rating",
      "Data": { /* object data */ },
      "Identifier": { /* for updates/deletes */ }
    }
  ]
}
```

---

## Import Root Object

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Version` | string | Yes | Import format version (currently "1.0") |
| `Description` | string | No | Description of this import batch |
| `ImportDate` | datetime | No | When this import file was created |
| `Operations` | Operation[] | Yes | Array of operations to perform |

---

## Operation Object

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Operation` | string | Yes | `Add`, `Update`, or `Delete` |
| `Target` | string | Yes | `Category`, `Link`, `SubCategory`, `Tag`, `Rating` |
| `Data` | object | Conditional | Required for Add/Update operations |
| `Identifier` | object | Conditional | Required for Update/Delete operations |
| `Options` | object | No | Additional operation options |

---

## Target Types

### 1. Category Operations

#### Add Category
Adds a new root category or finds existing one to merge into.

```json
{
  "Operation": "Add",
  "Target": "Category",
  "Data": {
    "Name": "New Category",
    "Description": "Category description",
    "Icon": "??",
    "Keywords": "searchable keywords",
    "TagIds": ["tag-id-1", "tag-id-2"],
    "SortOrder": "NameAscending",
    "IsBookmarkCategory": false,
    "IsBookmarkLookup": false,
    "IsAuditLoggingEnabled": false,
    "PasswordProtection": "None"
  },
  "Options": {
    "MergeIfExists": true,
    "UpdateExisting": false
  }
}
```

**Options:**
- `MergeIfExists` (bool): If true and category exists, merge new items into it. Default: false.
- `UpdateExisting` (bool): If true and category exists, update its properties. Default: false.

#### Update Category
Updates properties of an existing category.

```json
{
  "Operation": "Update",
  "Target": "Category",
  "Identifier": {
    "Name": "Existing Category"
  },
  "Data": {
    "Description": "Updated description",
    "Icon": "??",
    "Keywords": "new keywords",
    "TagIds": ["tag-id-3"],
    "IsAuditLoggingEnabled": true
  },
  "Options": {
    "MergeTagIds": true,
    "PreserveTimestamps": false
  }
}
```

**Options:**
- `MergeTagIds` (bool): If true, add new TagIds to existing ones instead of replacing. Default: false.
- `PreserveTimestamps` (bool): If true, don't update ModifiedDate. Default: false.

#### Delete Category
Deletes a category and all its contents.

```json
{
  "Operation": "Delete",
  "Target": "Category",
  "Identifier": {
    "Name": "Category to Delete"
  },
  "Options": {
    "Recursive": true,
    "BackupBeforeDelete": true
  }
}
```

**Options:**
- `Recursive` (bool): Delete subcategories and links. Default: true.
- `BackupBeforeDelete` (bool): Create backup before deleting. Default: true.

---

### 2. SubCategory Operations

#### Add SubCategory
Adds a subcategory to an existing category.

```json
{
  "Operation": "Add",
  "Target": "SubCategory",
  "Identifier": {
    "ParentCategoryPath": "Parent Category"
  },
  "Data": {
    "Name": "New SubCategory",
    "Description": "Subcategory description",
    "Icon": "??",
    "SortOrder": "NameAscending"
  }
}
```

#### Update SubCategory
Updates a subcategory.

```json
{
  "Operation": "Update",
  "Target": "SubCategory",
  "Identifier": {
    "CategoryPath": "Parent Category/SubCategory Name"
  },
  "Data": {
    "Description": "Updated description",
    "Icon": "??",
    "TagIds": ["tag-id-1"]
  }
}
```

#### Delete SubCategory
Deletes a subcategory.

```json
{
  "Operation": "Delete",
  "Target": "SubCategory",
  "Identifier": {
    "CategoryPath": "Parent Category/SubCategory Name"
  },
  "Options": {
    "Recursive": true
  }
}
```

---

### 3. Link Operations

#### Add Link
Adds a new link to a category.

```json
{
  "Operation": "Add",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category Name"
  },
  "Data": {
    "Title": "New Link",
    "Url": "https://example.com",
    "Description": "Link description",
    "Keywords": "keywords",
    "TagIds": ["tag-id-1"],
    "IsDirectory": false,
    "FolderType": "LinkOnly"
  },
  "Options": {
    "SkipIfExists": true,
    "UpdateIfExists": false
  }
}
```

**Options:**
- `SkipIfExists` (bool): Skip if link with same title exists. Default: true.
- `UpdateIfExists` (bool): Update existing link with same title. Default: false.

#### Update Link
Updates an existing link.

```json
{
  "Operation": "Update",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Existing Link Title"
  },
  "Data": {
    "Url": "https://newurl.com",
    "Description": "Updated description",
    "Keywords": "new keywords",
    "TagIds": ["tag-id-2", "tag-id-3"]
  },
  "Options": {
    "MergeTagIds": true,
    "UpdateUrlStatus": true
  }
}
```

**Options:**
- `MergeTagIds` (bool): Add new TagIds to existing ones. Default: false.
- `UpdateUrlStatus` (bool): Re-check URL status after update. Default: false.

#### Delete Link
Deletes a link from a category.

```json
{
  "Operation": "Delete",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link to Delete"
  },
  "Options": {
    "DeleteCatalogEntries": true
  }
}
```

**Options:**
- `DeleteCatalogEntries` (bool): Also delete catalog entries if this is a directory. Default: true.

---

### 4. Tag Operations

#### Add Tags to Items
Adds tags to categories or links.

```json
{
  "Operation": "Add",
  "Target": "Tag",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "TagIds": ["tag-new-1", "tag-new-2"]
  },
  "Options": {
    "CreateTagsIfMissing": true
  }
}
```

**Options:**
- `CreateTagsIfMissing` (bool): Create tag definitions if they don't exist. Default: false.

#### Delete Tags from Items
Removes tags from categories or links.

```json
{
  "Operation": "Delete",
  "Target": "Tag",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "TagIds": ["tag-to-remove-1", "tag-to-remove-2"]
  }
}
```

---

### 5. Rating Operations

#### Add/Update Ratings
Adds or updates ratings for categories or links.

```json
{
  "Operation": "Update",
  "Target": "Rating",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "Ratings": [
      {
        "Name": "Quality",
        "Value": 5
      },
      {
        "Name": "Usefulness",
        "Value": 4
      }
    ]
  },
  "Options": {
    "MergeRatings": true
  }
}
```

**Options:**
- `MergeRatings` (bool): Merge with existing ratings instead of replacing. Default: false.

#### Delete Ratings
Removes ratings from categories or links.

```json
{
  "Operation": "Delete",
  "Target": "Rating",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "RatingNames": ["Quality", "Usefulness"]
  }
}
```

---

## Identifier Object

The `Identifier` object specifies which item to update or delete:

| Property | Type | Context | Description |
|----------|------|---------|-------------|
| `Name` | string | Category | Category name |
| `CategoryPath` | string | SubCategory, Link | Full path (e.g., "Parent/Child") |
| `ParentCategoryPath` | string | SubCategory | Parent category path for adds |
| `Title` | string | Link | Link title |

---

## Options Object

Common options available for operations:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MergeIfExists` | bool | false | Merge instead of replace (Add) |
| `UpdateExisting` | bool | false | Update if exists (Add) |
| `SkipIfExists` | bool | true | Skip if exists (Add) |
| `UpdateIfExists` | bool | false | Update if exists (Add) |
| `MergeTagIds` | bool | false | Merge tag IDs (Update) |
| `MergeRatings` | bool | false | Merge ratings (Update) |
| `PreserveTimestamps` | bool | false | Don't update timestamps |
| `Recursive` | bool | true | Include children (Delete) |
| `BackupBeforeDelete` | bool | true | Create backup (Delete) |
| `DeleteCatalogEntries` | bool | true | Delete catalogs (Delete) |
| `CreateTagsIfMissing` | bool | false | Auto-create tags (Tag ops) |
| `UpdateUrlStatus` | bool | false | Re-check URLs (Link update) |

---

## Data Validation

### Required Fields by Target

**Category/SubCategory:**
- `Name` (required)
- `Icon` (defaults to "??")
- `CreatedDate` (auto-generated if missing)
- `ModifiedDate` (auto-updated)

**Link:**
- `Title` (required)
- `Url` (required)
- `CategoryPath` (required for context)
- `IsDirectory` (defaults to false)
- `CreatedDate` (auto-generated if missing)
- `ModifiedDate` (auto-updated)

### Path Separator

Use forward slash `/` as the path separator in `CategoryPath`:
- ? `"Parent/Child/GrandChild"`
- ? `"Parent\\Child\\GrandChild"`

---

## Import Behavior

### Transaction Handling
- Operations are processed **sequentially** in the order they appear
- If an operation fails, subsequent operations continue (non-transactional)
- A summary report is generated showing success/failure for each operation

### Conflict Resolution
- **Add with existing item:**
  - If `SkipIfExists`: Skip the operation
  - If `UpdateIfExists`: Update the existing item
  - If `MergeIfExists`: Merge children (categories) or properties
  
- **Update non-existent item:**
  - Log a warning and skip the operation
  
- **Delete non-existent item:**
  - Log a warning and continue

### Auto-Generated Fields
The following fields are auto-generated if not provided:
- `CreatedDate`: Current timestamp for new items
- `ModifiedDate`: Current timestamp for all operations
- `CategoryPath`: Constructed from parent hierarchy
- `Icon`: Defaults to "??" for categories

---

## Import Results

After import, a result object is returned:

```json
{
  "Success": true,
  "TotalOperations": 15,
  "Successful": 13,
  "Failed": 2,
  "Skipped": 0,
  "OperationResults": [
    {
      "Operation": "Add",
      "Target": "Link",
      "Status": "Success",
      "Message": "Added link 'Example' to 'Category Name'",
      "Identifier": {
        "CategoryPath": "Category Name",
        "Title": "Example"
      }
    },
    {
      "Operation": "Update",
      "Target": "Category",
      "Status": "Failed",
      "Message": "Category 'Non-Existent' not found",
      "Identifier": {
        "Name": "Non-Existent"
      }
    }
  ],
  "ImportDuration": "00:00:02.5",
  "CategoriesModified": ["Category Name", "Another Category"]
}
```

---

## Best Practices

### 1. Order Operations Logically
```json
{
  "Operations": [
    {"Operation": "Add", "Target": "Category"},
    {"Operation": "Add", "Target": "SubCategory"},
    {"Operation": "Add", "Target": "Link"}
  ]
}
```

### 2. Use Descriptive Descriptions
```json
{
  "Description": "Import Q4 2024 bookmarks from Chrome",
  "ImportDate": "2024-12-21T10:00:00"
}
```

### 3. Backup Before Large Imports
Set `BackupBeforeDelete: true` for delete operations, or manually backup categories before importing.

### 4. Test with Small Batches
Start with a small import file to test the structure before running large batches.

### 5. Use MergeIfExists for Incremental Updates
```json
{
  "Operation": "Add",
  "Target": "Link",
  "Options": {
    "UpdateIfExists": true
  }
}
```

---

## Error Handling

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "Category not found" | Invalid category name | Verify category exists or add it first |
| "Link title already exists" | Duplicate link title | Use `UpdateIfExists` option |
| "Invalid CategoryPath" | Wrong path format | Use forward slash separator |
| "Missing required field" | Data incomplete | Add required fields to Data object |
| "Tag not found" | Tag ID doesn't exist | Use `CreateTagsIfMissing` option |

### Validation Errors

Import files are validated before processing:
- ? Valid JSON structure
- ? Version compatibility
- ? Required fields present
- ? Valid enum values
- ? Path format correctness

---

## Security Considerations

### Password-Protected Categories
- Import operations respect category password protection
- You must unlock password-protected categories before importing
- Operations on password-protected categories will fail if not unlocked

### Audit Logging
- Import operations are logged if audit logging is enabled for the target category
- Each operation is logged individually with timestamps

---

## See Also

- [Category JSON Format](category-json-format.md) - Standard category format
- [Example Import Files](examples/import-examples/) - Sample import files
- [Tag Management](tags.md) - Working with tags
- [Rating System](ratings.md) - Rating definitions

