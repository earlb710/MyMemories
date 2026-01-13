# Category Import Quick Reference

## Basic Import Structure

```json
{
  "Version": "1.0",
  "Description": "Brief description of this import",
  "ImportDate": "2024-12-21T10:00:00",
  "Operations": [
    // Array of operations
  ]
}
```

## Operation Template

```json
{
  "Operation": "Add|Update|Delete",
  "Target": "Category|SubCategory|Link|Tag|Rating",
  "Identifier": { /* for Update/Delete */ },
  "Data": { /* for Add/Update */ },
  "Options": { /* operation options */ }
}
```

---

## Quick Operation Examples

### Add Category
```json
{
  "Operation": "Add",
  "Target": "Category",
  "Data": {
    "Name": "Category Name",
    "Description": "Description",
    "Icon": "??"
  }
}
```

### Add SubCategory
```json
{
  "Operation": "Add",
  "Target": "SubCategory",
  "Identifier": {
    "ParentCategoryPath": "Parent Category"
  },
  "Data": {
    "Name": "Sub Category Name",
    "Icon": "??"
  }
}
```

### Add Link
```json
{
  "Operation": "Add",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category/SubCategory"
  },
  "Data": {
    "Title": "Link Title",
    "Url": "https://example.com",
    "Description": "Link description"
  }
}
```

### Update Category
```json
{
  "Operation": "Update",
  "Target": "Category",
  "Identifier": {
    "Name": "Existing Category"
  },
  "Data": {
    "Description": "Updated description",
    "TagIds": ["tag-1", "tag-2"]
  }
}
```

### Update Link
```json
{
  "Operation": "Update",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "Url": "https://new-url.com",
    "Description": "Updated description"
  }
}
```

### Delete Link
```json
{
  "Operation": "Delete",
  "Target": "Link",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link to Delete"
  }
}
```

### Add Tags
```json
{
  "Operation": "Add",
  "Target": "Tag",
  "Identifier": {
    "CategoryPath": "Category Name",
    "Title": "Link Title"
  },
  "Data": {
    "TagIds": ["tag-1", "tag-2"]
  }
}
```

### Update Ratings
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
      {"Name": "Quality", "Value": 5},
      {"Name": "Usefulness", "Value": 4}
    ]
  }
}
```

---

## Common Options

### Add Options
```json
"Options": {
  "SkipIfExists": true,
  "UpdateIfExists": false,
  "MergeIfExists": false
}
```

### Update Options
```json
"Options": {
  "MergeTagIds": true,
  "MergeRatings": false,
  "PreserveTimestamps": false,
  "UpdateUrlStatus": true
}
```

### Delete Options
```json
"Options": {
  "Recursive": true,
  "BackupBeforeDelete": true,
  "DeleteCatalogEntries": true
}
```

---

## Identifiers

### Category
```json
"Identifier": {
  "Name": "Category Name"
}
```

### SubCategory
```json
"Identifier": {
  "CategoryPath": "Parent/SubCategory"
}
```

### Link
```json
"Identifier": {
  "CategoryPath": "Category/SubCategory",
  "Title": "Link Title"
}
```

---

## Common Enumerations

### SortOption
- `NameAscending` / `NameDescending`
- `DateAscending` / `DateDescending`
- `SizeAscending` / `SizeDescending`

### PasswordProtectionType
- `None`
- `GlobalPassword`
- `OwnPassword`

### FolderLinkType
- `LinkOnly`
- `CatalogueFiles`
- `FilteredCatalogue`

---

## Best Practices

? **DO:**
- Order operations logically (categories ? subcategories ? links)
- Use descriptive descriptions for import files
- Test with small batches first
- Backup before large imports
- Use `MergeIfExists` for incremental updates

? **DON'T:**
- Mix forward and backward slashes in paths
- Delete without backups (`BackupBeforeDelete: false`)
- Skip validation of import structure
- Run untested imports on production data

---

## Path Format

? Correct: `"Category/SubCategory/SubSubCategory"`
? Wrong: `"Category\\SubCategory\\SubSubCategory"`

Always use forward slashes `/` as path separators.

---

## Minimal Working Examples

### Minimal Add Link
```json
{
  "Version": "1.0",
  "Operations": [
    {
      "Operation": "Add",
      "Target": "Link",
      "Identifier": {"CategoryPath": "My Category"},
      "Data": {
        "Title": "Example",
        "Url": "https://example.com"
      }
    }
  ]
}
```

### Minimal Update Category
```json
{
  "Version": "1.0",
  "Operations": [
    {
      "Operation": "Update",
      "Target": "Category",
      "Identifier": {"Name": "My Category"},
      "Data": {"Description": "New description"}
    }
  ]
}
```

### Minimal Delete
```json
{
  "Version": "1.0",
  "Operations": [
    {
      "Operation": "Delete",
      "Target": "Link",
      "Identifier": {
        "CategoryPath": "My Category",
        "Title": "Old Link"
      }
    }
  ]
}
```

---

## Error Handling

Operations continue even if one fails. Check the result:

```json
{
  "Success": false,
  "TotalOperations": 10,
  "Successful": 8,
  "Failed": 2,
  "OperationResults": [
    {
      "Operation": "Update",
      "Status": "Failed",
      "Message": "Category 'XYZ' not found"
    }
  ]
}
```

---

## See Also

- [Complete Import Format Documentation](category-import-format.md)
- [Import Examples](examples/import-examples-README.md)
- [Standard Category Format](category-json-format.md)
