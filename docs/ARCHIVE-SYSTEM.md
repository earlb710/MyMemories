# Archive System - MyMemories

**Version:** 1.0  
**Last Updated:** 2026-01-15

## ?? Overview

The Archive System provides a **soft-delete** mechanism for categories and links in MyMemories. Instead of permanently deleting items, they are moved to a special "Archived" node where they can be restored or permanently deleted later.

---

## ? Features

### ??? Archive Node
- Located at the bottom of the TreeView
- Visual separator (divider line) above it
- **Red "A" icon** for easy identification
- **Shows item count in brackets**: `Archived (5)` or `Archived (0)`
- **Does not auto-expand** when items are added
- **Persisted to JSON** (`Archive.json`)

### ?? Soft Delete
- **Categories**: "Remove Category" moves to archive
- **Links**: "Remove Link" moves to archive
- Original location stored for restoration
- Archived items retain all metadata

### ?? Persistence
- Archive stored in: `%APPDATA%\MyMemories\Archive.json`
- Auto-loads on startup
- Auto-saves on archive/restore/delete operations

---

## ?? How It Works

### Archiving an Item

**Category:**
```
1. Right-click category ? "Remove Category"
2. Category moves to "Archived" node
3. Original parent path saved in metadata
4. JSON file deleted (if root category)
5. Archive count updated: "Archived (n)"
6. Archive.json saved
```

**Link:**
```
1. Right-click link ? "Remove Link"
2. Link moves to "Archived" node
3. Original category path saved in metadata
4. Parent category saved
5. Archive count updated: "Archived (n)"
6. Archive.json saved
```

### Restoring an Item

**From Archive Context Menu:**
```
1. Right-click archived item
2. Select "Restore"
3. Item returns to original location
4. Metadata cleared
5. Archive count updated
6. Archive.json saved
```

**Category Restoration:**
- If parent exists: Restored to original location
- If parent not found: Restored to root
- JSON file recreated (if was root category)

**Link Restoration:**
- Requires original category to exist
- Shows error if category not found
- Parent category saved after restoration

### Permanently Deleting

**From Archive Context Menu:**
```
1. Right-click archived item
2. Select "Delete Permanently"
3. Confirmation dialog appears
4. Item removed forever (cannot undo)
5. Archive count updated
6. Archive.json saved
```

---

## ?? Archive Data Structure

### Archive.json Format

```json
{
  "ArchivedCategories": [
    {
      "Name": "Old Project",
      "Description": "Archived project files",
      "Icon": "??",
      "ArchivedDate": "2026-01-15T15:30:00",
      "OriginalParentPath": "Root",
      "Links": [
        {
          "Title": "Project Document",
          "Url": "C:\\Documents\\project.docx",
          "ArchivedDate": "2026-01-15T15:30:00",
          "OriginalCategoryPath": "Old Project"
        }
      ]
    }
  ],
  "ArchivedLinks": [
    {
      "Title": "Standalone Link",
      "Url": "https://example.com",
      "ArchivedDate": "2026-01-15T15:31:00",
      "OriginalCategoryPath": "Work > Projects"
    }
  ],
  "LastModified": "2026-01-15T15:31:00"
}
```

### Key Properties

#### CategoryItem
- `ArchivedDate`: When archived (DateTime?)
- `OriginalParentPath`: Parent path or "Root" (string?)
- `Links`: Child links for serialization (List<LinkItem>?)
- `IsArchiveNode`: Flag for Archive node (bool, JsonIgnore)

#### LinkItem
- `ArchivedDate`: When archived (DateTime?)
- `OriginalCategoryPath`: Original category path (string?)

---

## ?? Implementation Details

### Files Involved

```
MyMemories/
??? MainWindow.Archive.cs       # Archive logic
??? Models/
?   ??? CategoryItem.cs         # Archive metadata
?   ??? LinkItem.cs             # Archive metadata
??? Converters/
?   ??? ArchiveNodeColorConverter.cs  # Red icon styling
??? MainWindow.xaml             # Archive context menu
```

### Key Methods

#### MainWindow.Archive.cs

```csharp
// Archive operations
Task ArchiveCategoryAsync(TreeViewNode)
Task ArchiveLinkAsync(TreeViewNode)

// Restore operations
Task RestoreCategoryAsync(TreeViewNode)
Task RestoreLinkAsync(TreeViewNode)

// Permanent delete
Task PermanentlyDeleteCategoryAsync(TreeViewNode)
Task PermanentlyDeleteLinkAsync(TreeViewNode)

// Persistence
Task LoadArchiveFromJsonAsync()
Task SaveArchiveToJsonAsync()

// UI updates
void UpdateArchiveNodeName()

// Utilities
TreeViewNode? FindCategoryByPath(string)
TreeViewNode GetOrCreateArchiveNode()
```

### Context Menu Handlers

```csharp
// MainWindow.xaml.cs
private async void ArchiveMenu_Restore_Click(object sender, RoutedEventArgs e)
private async void ArchiveMenu_DeletePermanently_Click(object sender, RoutedEventArgs e)
```

---

## ??? User Interface

### Archive Context Menu

```
???????????????????????????????
? ? Restore                   ?
???????????????????????????????
? ??? Delete Permanently (Red) ?
???????????????????????????????
```

### TreeView Structure

```
?? My Documents
?? Work Projects
?? Personal
????????????????????? (Divider)
??? Archived (3)
  ?? ?? Old Project (archived category)
  ?? ?? Old Link (archived link)
  ?? ?? Completed (archived category)
```

### Archive Node Display

| State | Display |
|-------|---------|
| Empty | `Archived (0)` |
| 1 item | `Archived (1)` |
| Multiple | `Archived (5)` |

---

## ?? Configuration

### Archive Node Properties

```csharp
// In LoadAllCategoriesAsync()
var archivedCategory = new CategoryItem
{
    Name = "Archived",  // Updated dynamically with count
    Description = "Archived categories and links",
    Icon = "A",         // Red styled via ArchiveNodeColorConverter
    IsArchiveNode = true
};
```

### Archive File Location

```csharp
private const string ArchiveFileName = "Archive.json";
var archivePath = Path.Combine(_dataFolder, ArchiveFileName);
// Typically: C:\Users\[User]\AppData\Roaming\MyMemories\Archive.json
```

---

## ?? Styling

### Red Archive Icon

**Converter:** `ArchiveNodeColorConverter.cs`

```csharp
public object Convert(object value, Type targetType, object parameter, string language)
{
    if (value is string name && name.StartsWith("Archived"))
    {
        return new SolidColorBrush(Colors.Red);
    }
    return new SolidColorBrush(Colors.Black);
}
```

**Applied in:** `MainWindow.xaml` (Category icon TextBlock)

---

## ?? Error Handling

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Cannot restore | Original category deleted | Restore to root |
| Archive not loading | Corrupted JSON | Creates empty archive |
| Count not updating | Node refresh failed | Manual refresh triggered |

### Diagnostics

**Debug Output:**
```csharp
System.Diagnostics.Debug.WriteLine($"[Archive] Loaded {n} categories and {m} links");
System.Diagnostics.Debug.WriteLine($"[Archive] Saved {n} categories and {m} links");
System.Diagnostics.Debug.WriteLine($"[Archive] Error loading archive: {ex.Message}");
```

---

## ?? Technical Specifications

### Performance

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Archive item | O(1) | Simple add to collection |
| Restore item | O(n) | FindCategoryByPath linear search |
| Load archive | O(n) | Deserialize + tree build |
| Save archive | O(n) | Serialize tree structure |
| Update count | O(1) | Simple count + refresh |

### Memory

- Archive data kept in memory
- Lazy-loaded on startup
- Minimal overhead (metadata only)

### File I/O

- **Read**: On app startup
- **Write**: After each archive operation
- **Format**: JSON (indented, pretty-printed)

---

## ?? Security Considerations

### Password-Protected Categories

- Archived categories retain password hash
- Password required for restoration (if applicable)
- JSON stores hash, not plaintext

### Data Integrity

- Original location always stored
- Validation on restoration
- Confirmation required for permanent delete

---

## ?? Future Enhancements

### Potential Improvements

1. **Archive Search**
   - Search within archived items
   - Filter by date archived

2. **Bulk Operations**
   - "Restore All"
   - "Clear Archive" (delete all)
   - "Archive Selected"

3. **Auto-Cleanup**
   - Delete items older than X days
   - Scheduled cleanup

4. **Archive Statistics**
   - Total size of archived items
   - Date archived histogram
   - Most frequently archived items

5. **Export/Import**
   - Export archive to separate file
   - Import archive from backup

---

## ?? Status Messages

### Archive Operations

```
? "Archived category 'Project' - Use context menu to restore or delete permanently"
? "Archived link 'Document' - Use context menu to restore or delete permanently"
```

### Restore Operations

```
? "Restored category 'Project' to 'Work > Active'"
? "Restored link 'Document' to 'Work > Projects'"
? "Cannot Restore: Original category 'Deleted' not found"
```

### Delete Operations

```
? "Permanently deleted category 'Old Project'"
? "Permanently deleted link 'Outdated Link'"
```

---

## ?? Testing Checklist

### Manual Test Cases

- [ ] Archive root category ? Moves to archive, count updates
- [ ] Archive subcategory ? Moves to archive, parent saves
- [ ] Archive link ? Moves to archive, parent saves
- [ ] Restore category to root ? Appears in root
- [ ] Restore category to parent ? Appears in correct location
- [ ] Restore link ? Appears in original category
- [ ] Permanently delete category ? Removed forever
- [ ] Permanently delete link ? Removed forever
- [ ] Archive count shows (0) when empty
- [ ] Archive count updates on all operations
- [ ] Archive persists after app restart
- [ ] Archive does not auto-expand

---

## ?? Related Documentation

- [Category JSON Format](category-json-format.md)
- [Fonts and Icons Reference](FONTS-AND-ICONS-REFERENCE.md)
- [Exception Handling System](EXCEPTION-HANDLING-SYSTEM.md)

---

## ?? Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-15 | Initial archive system implementation |

---

**Note:** The archive system provides a safety net for accidental deletions while keeping the interface clean and organized. Users can confidently remove items knowing they can be restored later.
