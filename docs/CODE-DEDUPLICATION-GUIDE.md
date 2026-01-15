# Code Deduplication Utilities - Usage Guide

**Created:** 2026-01-14

## Overview

Three new utility classes have been created to eliminate duplicate code patterns across MyMemories:

1. **DialogFactory** - Centralized dialog creation
2. **TreeViewTraversalUtilities** - Centralized tree operations
3. **ErrorMessageFormatter** - Consistent error messages

---

## 1. DialogFactory

### Purpose
Eliminates repeated dialog creation code and ensures consistent dialog styling across the application.

### Common Use Cases

#### Confirmation Dialogs

**Before:**
```csharp
var confirmDialog = new ContentDialog
{
    Title = "Delete Category",
    Content = "Are you sure you want to delete this category?",
    PrimaryButtonText = "Yes",
    CloseButtonText = "No",
    DefaultButton = ContentDialogButton.Close,
    XamlRoot = Content.XamlRoot
};

var result = await confirmDialog.ShowAsync();
if (result == ContentDialogResult.Primary)
{
    // Delete logic
}
```

**After:**
```csharp
if (await DialogFactory.ShowConfirmationAsync(
    "Delete Category",
    "Are you sure you want to delete this category?",
    Content.XamlRoot))
{
    // Delete logic
}
```

**Savings:** 8 lines ? 1 line (7 lines saved)

---

#### Error Dialogs

**Before:**
```csharp
var errorDialog = new ContentDialog
{
    Title = "Error",
    Content = "Failed to load the file.",
    CloseButtonText = "OK",
    XamlRoot = Content.XamlRoot
};
await errorDialog.ShowAsync();
```

**After:**
```csharp
await DialogFactory.ShowErrorAsync(
    "Load Failed",
    "Failed to load the file.",
    Content.XamlRoot);
```

**Savings:** 6 lines ? 1 line (5 lines saved)

---

#### Text Input Dialogs

**Before:**
```csharp
var textBox = new TextBox
{
    PlaceholderText = "Enter name",
    Text = ""
};

var dialog = new ContentDialog
{
    Title = "Rename Item",
    Content = new StackPanel { /* ... */ },
    PrimaryButtonText = "OK",
    CloseButtonText = "Cancel",
    XamlRoot = Content.XamlRoot
};

var result = await dialog.ShowAsync();
if (result == ContentDialogResult.Primary)
{
    var name = textBox.Text;
}
```

**After:**
```csharp
var name = await DialogFactory.ShowTextInputAsync(
    "Rename Item",
    "Enter a new name:",
    "Enter name",
    "",
    Content.XamlRoot);

if (name != null) // User clicked OK
{
    // Use name
}
```

**Savings:** 15+ lines ? 2 lines (13+ lines saved)

---

#### Password Dialogs

**Before:**
```csharp
var passwordBox = new PasswordBox { PlaceholderText = "Enter password" };
var dialog = new ContentDialog { /* setup */ };
// ... more code
```

**After:**
```csharp
var password = await DialogFactory.ShowPasswordDialogAsync(
    "Enter Password",
    "This category is protected by a password.",
    Content.XamlRoot);
```

---

### Available Dialog Types

| Method | Purpose | Returns |
|--------|---------|---------|
| `ShowConfirmationAsync()` | Yes/No questions | `Task<bool>` |
| `ShowErrorAsync()` | Error messages | `Task` |
| `ShowInfoAsync()` | Information | `Task` |
| `ShowWarningAsync()` | Warnings | `Task` |
| `ShowSuccessAsync()` | Success messages | `Task` |
| `ShowTextInputAsync()` | Get text from user | `Task<string?>` |
| `ShowPasswordDialogAsync()` | Get password | `Task<string?>` |
| `CreateCustomDialog()` | Complex custom dialogs | `ContentDialog` |
| `CreateProgressDialog()` | Show progress | `ContentDialog` |

---

## 2. TreeViewTraversalUtilities

### Purpose
Eliminates duplicate tree traversal logic and provides efficient, tested algorithms.

### Common Use Cases

#### Find Nodes by Content Type

**Before:**
```csharp
// Duplicate code in multiple places
private void CollectAutoRefreshCatalogs(TreeViewNode node, List<(LinkItem, TreeViewNode)> list)
{
    if (node.Content is LinkItem link)
    {
        if (link.AutoRefreshCatalog)
            list.Add((link, node));
    }
    
    foreach (var child in node.Children)
    {
        CollectAutoRefreshCatalogs(child, list);
    }
}
```

**After:**
```csharp
var catalogNodes = TreeViewTraversalUtilities.FindAllNodes(
    rootNode,
    node => node.Content is LinkItem link && link.AutoRefreshCatalog);
```

**Savings:** 10+ lines ? 1 line (recursive function eliminated)

---

#### Execute Action on All Nodes

**Before:**
```csharp
private void UpdateStatusRecursively(TreeViewNode node)
{
    if (node.Content is LinkItem link)
    {
        link.UpdateStatus();
    }
    
    foreach (var child in node.Children)
    {
        UpdateStatusRecursively(child);
    }
}
```

**After:**
```csharp
TreeViewTraversalUtilities.ForEachOfType<LinkItem>(
    rootNode,
    (node, link) => link.UpdateStatus());
```

**Savings:** 8 lines ? 1 line + no custom recursive function needed

---

#### Find Specific Node

**Before:**
```csharp
private TreeViewNode? FindCategoryNode(TreeViewNode root, string name)
{
    if (root.Content is CategoryItem cat && cat.Name == name)
        return root;
    
    foreach (var child in root.Children)
    {
        var found = FindCategoryNode(child, name);
        if (found != null) return found;
    }
    
    return null;
}
```

**After:**
```csharp
var categoryNode = TreeViewTraversalUtilities.FindNode(
    rootNode,
    node => node.Content is CategoryItem cat && cat.Name == name);
```

**Savings:** 10 lines ? 1 line

---

### Available Methods

#### Traversal
- `TraverseDepthFirst(node, action)` - Visit all nodes depth-first
- `TraverseBreadthFirst(node, action)` - Visit all nodes breadth-first

#### Finding
- `FindNode(node, predicate)` - Find first matching node
- `FindAllNodes(node, predicate)` - Find all matching nodes
- `FilterByContentType<T>(node)` - Get all nodes of specific type

#### Analysis
- `GetAncestors(node)` - Get all parent nodes
- `GetDescendants(node)` - Get all child nodes
- `GetDepth(node)` - Get node depth level
- `GetRoot(node)` - Get root node
- `CountNodes(node)` - Count total nodes
- `GetLeafNodes(node)` - Get nodes with no children
- `ContainsDescendant(ancestor, target)` - Check containment

#### Utilities
- `ForEachOfType<T>(node, action)` - Execute action on typed nodes
- `ValidateTreeStructure(node)` - Check for circular references

---

## 3. ErrorMessageFormatter

### Purpose
Provides consistent, user-friendly error messages with helpful context.

### Common Use Cases

#### File Operation Errors

**Before:**
```csharp
catch (Exception ex)
{
    StatusText.Text = $"Error: {ex.Message}";
}
```

**After:**
```csharp
catch (Exception ex)
{
    var message = ErrorMessageFormatter.FormatFileError(
        "load", filePath, ex);
    await DialogFactory.ShowErrorAsync("Load Failed", message, Content.XamlRoot);
}
```

**Benefits:**
- User-friendly explanation
- File name and location shown
- Context-specific suggestions
- Consistent formatting

**Example Output:**
```
Failed to load file.

File: MyDocument.txt
Location: C:\Users\Earl\Documents

The file is currently in use by another program. Close it and try again.
```

---

#### Validation Errors

**Before:**
```csharp
if (string.IsNullOrEmpty(name))
{
    await ShowErrorAsync("Name is required");
}
if (name.Length > 100)
{
    await ShowErrorAsync("Name is too long");
}
```

**After:**
```csharp
var errors = new List<string>();
if (string.IsNullOrEmpty(name)) errors.Add("Name is required");
if (name.Length > 100) errors.Add("Name must be 100 characters or less");

if (errors.Any())
{
    var message = ErrorMessageFormatter.FormatValidationErrors(
        "category name", errors.ToArray());
    await DialogFactory.ShowErrorAsync("Validation Failed", message, Content.XamlRoot);
}
```

**Example Output:**
```
Validation failed for category name:

• Name is required
• Name must be 100 characters or less
```

---

#### Network Errors

**Before:**
```csharp
catch (HttpRequestException ex)
{
    StatusText.Text = "Network error";
}
```

**After:**
```csharp
catch (HttpRequestException ex)
{
    var message = ErrorMessageFormatter.FormatNetworkError(
        "fetch web page", url, ex);
    await DialogFactory.ShowErrorAsync("Network Error", message, Content.XamlRoot);
}
```

**Example Output:**
```
Failed to fetch web page.

URL: https://example.com

The server returned an error. The page may not exist or the server may be down.
```

---

### Available Formatters

| Method | Use Case | Output |
|--------|----------|--------|
| `FormatFileError()` | File operations | File name, location, helpful suggestions |
| `FormatDirectoryError()` | Folder operations | Folder details, permissions info |
| `FormatValidationErrors()` | Input validation | Bulleted list of issues |
| `FormatNetworkError()` | HTTP/URL operations | URL, connection suggestions |
| `FormatImportExportError()` | JSON/data import | File name, line number, format help |
| `FormatSecurityError()` | Password/auth | Security context |
| `FormatPartialSuccess()` | Batch operations | Success/failure counts |
| `FormatTimeoutError()` | Long operations | Timeout duration, suggestions |
| `FormatOutOfMemoryError()` | Memory issues | Memory-saving suggestions |

---

## Migration Strategy

### Phase 1: New Code (Immediate)
- Use these utilities in all new code immediately
- No need to migrate existing code yet

### Phase 2: Opportunistic Refactoring
- When modifying existing code, replace old patterns with utilities
- Update nearby similar code in the same file

### Phase 3: Systematic Refactoring (Future)
- Dedicate time to systematically update all dialog creation
- Update all tree traversal code
- Update all error handling

---

## Example: Before & After Complete Scenario

### Deleting a Category with Confirmation

**Before (35 lines):**
```csharp
private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
{
    if (_contextMenuNode == null) return;
    
    if (!(_contextMenuNode.Content is CategoryItem category)) return;
    
    // Show confirmation
    var confirmDialog = new ContentDialog
    {
        Title = "Delete Category",
        Content = $"Are you sure you want to delete '{category.Name}'?",
        PrimaryButtonText = "Delete",
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Close,
        XamlRoot = Content.XamlRoot
    };
    
    var result = await confirmDialog.ShowAsync();
    if (result != ContentDialogResult.Primary) return;
    
    try
    {
        await _categoryService.DeleteCategoryAsync(category);
        LinksTreeView.RootNodes.Remove(_contextMenuNode);
        StatusText.Text = $"Deleted category: {category.Name}";
    }
    catch (Exception ex)
    {
        var errorDialog = new ContentDialog
        {
            Title = "Error",
            Content = $"Failed to delete category: {ex.Message}",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await errorDialog.ShowAsync();
    }
}
```

**After (18 lines):**
```csharp
private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
{
    if (_contextMenuNode?.Content is not CategoryItem category) return;
    
    if (!await DialogFactory.ShowConfirmationAsync(
        "Delete Category",
        $"Are you sure you want to delete '{category.Name}'?",
        Content.XamlRoot,
        "Delete"))
        return;
    
    try
    {
        await _categoryService.DeleteCategoryAsync(category);
        LinksTreeView.RootNodes.Remove(_contextMenuNode);
        StatusText.Text = ErrorMessageFormatter.FormatSuccess("delete category");
    }
    catch (Exception ex)
    {
        var message = ErrorMessageFormatter.FormatError("delete category", ex);
        await DialogFactory.ShowErrorAsync("Delete Failed", message, Content.XamlRoot);
    }
}
```

**Savings:**
- **17 lines removed** (49% reduction)
- **Consistent error handling**
- **Easier to read and maintain**
- **No repeated dialog setup code**

---

## Best Practices

### ? DO:

1. **Use DialogFactory for all new dialogs**
   ```csharp
   await DialogFactory.ShowErrorAsync(...);
   ```

2. **Use TreeViewTraversalUtilities for tree operations**
   ```csharp
   TreeViewTraversalUtilities.FindAllNodes(root, predicate);
   ```

3. **Use ErrorMessageFormatter for consistent messages**
   ```csharp
   ErrorMessageFormatter.FormatFileError(...);
   ```

4. **Combine utilities for powerful patterns**
   ```csharp
   try
   {
       var nodes = TreeViewTraversalUtilities.FilterByContentType<LinkItem>(root);
       // Process nodes...
   }
   catch (Exception ex)
   {
       var msg = ErrorMessageFormatter.FormatDataError("process links", "tree nodes", ex);
       await DialogFactory.ShowErrorAsync("Processing Failed", msg, xamlRoot);
   }
   ```

### ? DON'T:

1. **Don't create custom dialogs for standard cases**
   ```csharp
   // BAD
   var dialog = new ContentDialog { ... };
   
   // GOOD
   await DialogFactory.ShowConfirmationAsync(...);
   ```

2. **Don't write custom tree traversal**
   ```csharp
   // BAD
   void MyTraversal(TreeViewNode node) { /* recursive */ }
   
   // GOOD
   TreeViewTraversalUtilities.TraverseDepthFirst(node, n => { /* action */ });
   ```

3. **Don't use raw exception messages**
   ```csharp
   // BAD
   StatusText.Text = ex.Message;
   
   // GOOD
   var msg = ErrorMessageFormatter.FormatError("operation", ex);
   ```

---

## Metrics

### Expected Impact

Based on code analysis, these utilities should:

- **Reduce dialog code by 70-80%** (~500+ lines across codebase)
- **Eliminate 5-7 duplicate tree traversal functions**
- **Improve error message consistency** (currently inconsistent)
- **Reduce maintenance burden** (change once vs. many places)

### Files with Highest Savings Potential

1. `MainWindow.ContextMenu.*.cs` - Many dialogs
2. `MainWindow.TreeView.cs` - Tree traversal
3. `CategoryService.cs` - Error handling
4. `MainWindow.Import.cs` - Dialogs + error messages

---

## Related Documentation

- `docs/TODO-IMPROVEMENTS.md` - Full improvement backlog
- `docs/EXCEPTION-HANDLING-SYSTEM.md` - Global error handling
- `MyMemories\Utilities\DialogFactory.cs` - Dialog utilities
- `MyMemories\Utilities\TreeViewTraversalUtilities.cs` - Tree operations
- `MyMemories\Utilities\ErrorMessageFormatter.cs` - Error formatting

---

**Questions or Issues?** These are foundational utilities - report bugs immediately and we'll fix them centrally!
