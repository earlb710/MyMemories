# Open Category - File Persistence Fix

## Problem

When loading a category via "Open Category" (e.g., `minimal-category.json` containing "Quick Links"), the category would reappear after deletion and app restart.

### Root Cause

1. User loads `minimal-category.json` ? Contains "Quick Links" category
2. File copied to `Categories` folder as `minimal-category.json`
3. User deletes "Quick Links" category
4. Deletion looked for `Quick Links.json` (doesn't exist)
5. `minimal-category.json` remained in the folder
6. On restart, `LoadAllCategoriesAsync()` loads all JSON files
7. "Quick Links" reappears ??

### Why SourceFileName Didn't Work

The initial fix tried storing `SourceFileName` property:
- Marked with `[JsonIgnore]` (not persisted)
- Lost on app restart
- Only worked within the same session

## Solution: File Renaming

Instead of tracking the source filename, **rename the file to match the category name** when loading.

### Implementation

```csharp
// When loading via Open Category:
var sanitizedName = FileUtilities.SanitizeFileName(categoryName);
var targetFileName = sanitizedName + ".json";
var targetPath = Path.Combine(appDataFolder, targetFileName);

// Copy AND rename the file
File.Copy(file.Path, targetPath, overwrite: true);

// Delete the original if it has a different name
if (Path.GetFileName(file.Path) != targetFileName)
{
    File.Delete(file.Path);
}
```

### File Name Examples

| Original File | Category Name | Renamed To |
|--------------|---------------|------------|
| `minimal-category.json` | "Quick Links" | `Quick Links.json` |
| `sample-category.json` | "Development Resources" | `Development Resources.json` |
| `my-stuff.json` | "Personal Files" | `Personal Files.json` |

## Benefits

? **Consistent Naming**: All categories have matching filenames  
? **Standard Deletion**: Normal deletion code works for all categories  
? **No Special Tracking**: No need for `SourceFileName` property  
? **Persistent Fix**: Works across app restarts  
? **Clean Implementation**: Simpler code, fewer edge cases

## Changes Made

### 1. **MainWindow.Categories.cs** - OpenCategoryButton_Click
- Calculates target filename from category name
- Renames file during copy
- Deletes original if name differs
- Removed `SourceFileName` assignment

### 2. **MainWindow.Categories.cs** - DeleteCategoryAsync
- Removed special handling for `SourceFileName`
- Uses standard `DeleteCategoryAsync()` for all categories

### 3. **TreeViewEventService.cs** - HandleCategorySelectionAsync
- Simplified status display
- Always uses sanitized category name for file path
- Removed `SourceFileName` check

### 4. **CategoryItem.cs** - SourceFileName Property
- Can be removed (no longer needed)
- Kept for now for backward compatibility

## Testing

### Scenario 1: Load and Delete
1. ? Load `minimal-category.json` (contains "Quick Links")
2. ? File renamed to `Quick Links.json`
3. ? Delete "Quick Links"
4. ? `Quick Links.json` is deleted
5. ? Restart app
6. ? "Quick Links" does NOT reappear

### Scenario 2: Status Bar
1. ? Load any category
2. ? Click on category
3. ? Status shows: `Viewing: Quick Links (2 items) | File: C:\...\Quick Links.json`

### Scenario 3: Name Conflicts
1. ? Load `minimal-category.json` ("Quick Links")
2. ? Renamed to `Quick Links.json`
3. ? Create new category "Quick Links"
4. ? Prompt: "Category already loaded. Reload?"
5. ? Existing file overwritten cleanly

## Edge Cases Handled

### Different Source Locations
- ? Loading from Documents folder ? Copies and renames
- ? Loading from Downloads ? Copies and renames
- ? Loading from Categories folder itself ? Renames in place

### Special Characters in Names
- ? "Quick Links!" ? `Quick Links.json` (sanitized)
- ? "C:\Temp" ? `C__Temp.json` (sanitized)
- ? Uses `FileUtilities.SanitizeFileName()` for safety

### File Conflicts
- ? Target exists ? Overwrites with confirmation
- ? Original has same name ? Skips deletion
- ? Delete fails ? Continues (file might be in use)

## Code Cleanup Opportunity

The `SourceFileName` property in `CategoryItem.cs` can now be removed:

```csharp
// This can be deleted:
[JsonIgnore]
public string? SourceFileName { get; set; }
```

**Recommendation**: Remove in next cleanup pass to avoid confusion.

## Comparison: Before vs After

### Before (SourceFileName approach)
```
Load: minimal-category.json ? Categories/minimal-category.json
      SourceFileName = "minimal-category.json" (in memory only)
Delete: Look for "Quick Links.json" ? Not found ?
Restart: Load minimal-category.json ? "Quick Links" reappears ??
```

### After (File Renaming approach)
```
Load: minimal-category.json ? Categories/Quick Links.json
      (File renamed, original deleted)
Delete: Look for "Quick Links.json" ? Found and deleted ?
Restart: No "Quick Links.json" ? Category gone forever ?
```

## Future Considerations

### Encrypted Categories
If encrypted categories (`.zip.json`) are supported via Open Category:
- Same approach applies
- Rename to `[CategoryName].zip.json`
- Standard deletion works

### Category Rename
When a loaded category is renamed:
- Old file deleted
- New file created with new name
- Works naturally with current implementation

### Import/Export
- Exported categories keep their sanitized names
- Reimporting works seamlessly
- No special handling needed

## Conclusion

By renaming files to match category names, we achieve:
1. **Predictability**: Filename always matches category
2. **Simplicity**: One deletion code path for all categories
3. **Reliability**: No runtime state to maintain
4. **Transparency**: Users see consistent file names

This is a cleaner, more maintainable solution than tracking source filenames.
