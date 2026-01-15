# Dialog Migration to DialogFactory

**Date Started:** 2026-01-14  
**Status:** In Progress (Opportunistic Migration)  
**Strategy:** Migrate dialogs as we modify files

---

## ? **Completed Migrations**

### 1. MainWindow.Helpers.cs
**Date:** 2026-01-14  
**Dialogs Migrated:** 3

**Changes:**
- ? `HandleCatalogErrorAsync()` - Warning dialog ? `DialogFactory.ShowWarningAsync()`
- ? `ShowArchiveRefreshSuccessAsync()` - Success dialog ? `DialogFactory.ShowSuccessAsync()`
- ? `ShowArchiveRefreshErrorAsync()` - Error dialog ? `DialogFactory.ShowErrorAsync()`

**Before/After:**
```csharp
// BEFORE (17 lines)
var warningDialog = new ContentDialog
{
    Title = "Zip Created with Warning",
    Content = $"The zip archive was successfully created, but automatic cataloging failed.\n\n" +
             $"Error: {error.Message}\n\n" +
             $"The zip file is valid and can be opened externally. " +
             $"Try cataloging it manually later using the 'Create Catalog' button.",
    CloseButtonText = "OK",
    XamlRoot = Content.XamlRoot
};
await warningDialog.ShowAsync();

// AFTER (6 lines)
await DialogFactory.ShowWarningAsync(
    "Zip Created with Warning",
    $"The zip archive was successfully created, but automatic cataloging failed.\n\n" +
    $"Error: {error.Message}\n\n" +
    $"The zip file is valid and can be opened externally. " +
    $"Try cataloging it manually later using the 'Create Catalog' button.",
    Content.XamlRoot);
```

**Lines Saved:** 33 lines ? 18 lines (**45% reduction**)

---

## ?? **Migration Candidates (Prioritized)**

### High Priority Files (10+ dialogs each)

1. **MainWindow.ContextMenu.Category.cs** ? Next
   - Estimated dialogs: 15+
   - Types: Confirmation, Error, Warning
   - Impact: High (frequently used)

2. **MainWindow.ContextMenu.Link.cs**
   - Estimated dialogs: 12+
   - Types: Confirmation, Error  
   - Impact: High (frequently used)

3. **MainWindow.Password.cs**
   - Estimated dialogs: 8+
   - Types: Error, Info, Confirmation
   - Impact: Medium

4. **MainWindow.Config.cs**
   - Estimated dialogs: 10+
   - Types: Error, Warning, Info
   - Impact: Medium

### Medium Priority Files (5-9 dialogs each)

5. **MainWindow.Categories.cs**
   - Estimated dialogs: 6
   - Types: Info, Error
   - Impact: Medium

6. **MainWindow.Links.cs**
   - Estimated dialogs: 7
   - Types: Confirmation, Error
   - Impact: Medium

7. **MainWindow.Zip.cs**
   - Estimated dialogs: 5
   - Types: Error, Info
   - Impact: Low

### Low Priority Files (1-4 dialogs each)

8. **MainWindow.Files.cs**
   - Estimated dialogs: 3
   - Types: Error
   - Impact: Low

9. **MainWindow.Bookmarks.cs**
   - Estimated dialogs: 2
   - Types: Error, Success
   - Impact: Low

10. **MainWindow.Config.LogFileViewer.cs**
    - Estimated dialogs: 1
    - Types: Error
    - Impact: Low

---

## ?? **Migration Progress**

| File | Dialogs | Status | Lines Saved |
|------|---------|--------|-------------|
| MainWindow.Helpers.cs | 3 | ? Complete | 15 |
| **TOTAL** | **3** | **5%** | **15** |

**Estimated Total Dialogs in Codebase:** ~60-80  
**Migration Target (20%):** ~15-20 dialogs  
**Current Progress:** 3/15 = 20% of target ?

---

## ?? **Migration Patterns**

### Pattern 1: Simple Error Dialog
**Before:**
```csharp
var errorDialog = new ContentDialog
{
    Title = "Error",
    Content = "Something went wrong",
    CloseButtonText = "OK",
    XamlRoot = Content.XamlRoot
};
await errorDialog.ShowAsync();
```

**After:**
```csharp
await DialogFactory.ShowErrorAsync("Error", "Something went wrong", Content.XamlRoot);
```

---

### Pattern 2: Confirmation Dialog
**Before:**
```csharp
var confirmDialog = new ContentDialog
{
    Title = "Confirm Action",
    Content = "Are you sure?",
    PrimaryButtonText = "Yes",
    CloseButtonText = "No",
    DefaultButton = ContentDialogButton.Close,
    XamlRoot = Content.XamlRoot
};

var result = await confirmDialog.ShowAsync();
if (result == ContentDialogResult.Primary)
{
    // Do action
}
```

**After:**
```csharp
if (await DialogFactory.ShowConfirmationAsync("Confirm Action", "Are you sure?", Content.XamlRoot))
{
    // Do action
}
```

---

### Pattern 3: Success Dialog
**Before:**
```csharp
var successDialog = new ContentDialog
{
    Title = "Success",
    Content = "Operation completed successfully!",
    CloseButtonText = "OK",
    XamlRoot = Content.XamlRoot
};
await successDialog.ShowAsync();
```

**After:**
```csharp
await DialogFactory.ShowSuccessAsync("Success", "Operation completed successfully!", Content.XamlRoot);
```

---

### Pattern 4: Warning Dialog
**Before:**
```csharp
var warningDialog = new ContentDialog
{
    Title = "Warning",
    Content = "This action may have side effects",
    CloseButtonText = "OK",
    XamlRoot = Content.XamlRoot
};
await warningDialog.ShowAsync();
```

**After:**
```csharp
await DialogFactory.ShowWarningAsync("Warning", "This action may have side effects", Content.XamlRoot);
```

---

## ?? **How to Identify Migration Candidates**

### Search Patterns:
1. `new ContentDialog` - All dialog creation
2. `CloseButtonText = "OK"` - Simple OK dialogs
3. `PrimaryButtonText` - Confirmation dialogs
4. `await dialog.ShowAsync()` - Dialog show calls

### Quick Identification:
```bash
# Count dialogs in a file
grep -c "new ContentDialog" MainWindow.ContextMenu.Category.cs
```

---

## ? **Migration Checklist**

When migrating a file:
- [ ] Add `using MyMemories.Utilities;` if not present
- [ ] Replace simple error dialogs with `DialogFactory.ShowErrorAsync()`
- [ ] Replace confirmation dialogs with `DialogFactory.ShowConfirmationAsync()`
- [ ] Replace success dialogs with `DialogFactory.ShowSuccessAsync()`
- [ ] Replace warning dialogs with `DialogFactory.ShowWarningAsync()`
- [ ] Test the file after migration
- [ ] Update this document with statistics
- [ ] Commit with message: "Migrate [FileName] to use DialogFactory"

---

## ?? **Custom Dialogs (Keep As-Is)**

These dialogs are too complex for DialogFactory and should stay custom:
- LinkDetailsDialog (complex form)
- CategoryDialogBuilder (complex form)  
- TagManagementDialog (complex UI)
- RatingManagementDialog (complex UI)
- BackupFreshnessDialog (complex table)
- Archive refresh confirmation (custom content with icon)

---

## ?? **Next Steps**

1. ? **Done:** Migrate MainWindow.Helpers.cs
2. ? **Next:** Migrate MainWindow.ContextMenu.Category.cs
3. **Future:** Opportunistic migration as files are modified

---

## ?? **Expected Benefits**

**After full migration (60-80 dialogs):**
- **~300-400 lines removed** (5-7 lines saved per dialog)
- **Consistent UX** - All dialogs have same style
- **Easier maintenance** - Change once, apply everywhere
- **Better readability** - Less boilerplate code

---

## ?? **Related Documentation**

- `docs/CODE-DEDUPLICATION-GUIDE.md` - Complete usage guide
- `MyMemories\Utilities\DialogFactory.cs` - DialogFactory implementation
- `docs/TODO-IMPROVEMENTS.md` - Overall improvement tracking

---

**Last Updated:** 2026-01-14  
**Next Review:** When 20% target reached (15 dialogs)
