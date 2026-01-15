# MVVM Architecture Implementation

## Overview

This document describes the Model-View-ViewModel (MVVM) architecture implemented in the MyMemories application to reduce MainWindow responsibilities and improve code organization, testability, and maintainability.

## Architecture Components

### 1. **ViewModels** (`MyMemories/ViewModels/`)

ViewModels are the bridge between the UI (View) and the business logic (Services/Models). They:
- Expose data and commands for the View to bind to
- Handle UI state management
- Coordinate service calls
- Implement `INotifyPropertyChanged` for data binding

#### ViewModelBase
- **Purpose**: Base class for all ViewModels
- **Key Features**:
  - `INotifyPropertyChanged` implementation
  - `SetProperty<T>()` helper for property updates
  - `OnPropertyChanged()` notification method

#### MainWindowViewModel
- **Purpose**: Manages overall application state and main window operations
- **Responsibilities**:
  - Application initialization
  - Status text management
  - URL loading and navigation
  - Category loading
  - Configuration validation
  - Security (password prompts)
- **Key Properties**:
  - `StatusText`: Current status bar message
  - `IsUrlLoading`: URL loading state
  - `UrlText`: Current URL in address bar
- **Key Commands**:
  - `LoadUrlCommand`: Load URL from text box
  - `RefreshUrlCommand`: Refresh current URL
  - `LoadCategoriesCommand`: Load all categories
  - `ExitApplicationCommand`: Exit application

#### TreeViewViewModel
- **Purpose**: Manages tree view state and operations
- **Responsibilities**:
  - Node selection
  - Node expansion/collapse
  - Context menu state
  - Node refresh operations
- **Key Properties**:
  - `SelectedNode`: Currently selected tree node
  - `ContextMenuNode`: Node with active context menu
  - `HasSelectedNode`: Selection state flag
  - `IsSelectedNodeCategory`: Type check for category
  - `IsSelectedNodeLink`: Type check for link
- **Key Commands**:
  - `SelectNodeCommand`: Handle node selection
  - `ExpandNodeCommand`: Expand a node
  - `CollapseNodeCommand`: Collapse a node
  - `RefreshNodeCommand`: Refresh node data

#### SearchViewModel
- **Purpose**: Manages search functionality and state
- **Responsibilities**:
  - Search text management
  - Search execution
  - Result navigation
  - Search state tracking
- **Key Properties**:
  - `SearchText`: Current search query
  - `IsSearching`: Search in progress flag
  - `SearchResultCount`: Number of results
  - `CurrentSearchIndex`: Current result index
  - `HasSearchResults`: Results availability flag
- **Key Commands**:
  - `SearchCommand`: Perform search
  - `PreviousResultCommand`: Navigate to previous result
  - `NextResultCommand`: Navigate to next result
  - `ClearSearchCommand`: Clear search

### 2. **Commands** (`MyMemories/Commands/`)

Commands encapsulate user actions and make them bindable from XAML.

#### RelayCommand
- **Purpose**: Simple synchronous command implementation
- **Usage**: For quick, non-async operations
- **Example**:
```csharp
public ICommand ClearSearchCommand { get; }
ClearSearchCommand = new RelayCommand(
    execute: ClearSearch,
    canExecute: () => HasSearchResults);
```

#### RelayCommand<T>
- **Purpose**: Parameterized synchronous command
- **Usage**: When command needs parameter data
- **Example**:
```csharp
public ICommand SelectNodeCommand { get; }
SelectNodeCommand = new RelayCommand<TreeViewNode>(
    execute: SelectNode,
    canExecute: node => node != null);
```

#### AsyncRelayCommand
- **Purpose**: Asynchronous command implementation
- **Usage**: For async operations (loading, saving, etc.)
- **Features**:
  - Automatic `IsExecuting` tracking
  - Prevents concurrent execution
  - Proper async/await support
- **Example**:
```csharp
public ICommand LoadUrlCommand { get; }
LoadUrlCommand = new AsyncRelayCommand(
    execute: LoadUrlAsync,
    canExecute: () => !string.IsNullOrWhiteSpace(UrlText));
```

#### AsyncRelayCommand<T>
- **Purpose**: Parameterized asynchronous command
- **Usage**: For async operations with parameters
- **Example**:
```csharp
public ICommand RefreshNodeCommand { get; }
RefreshNodeCommand = new AsyncRelayCommand<TreeViewNode>(
    execute: async node => await RefreshNodeAsync(node),
    canExecute: node => node != null);
```

### 3. **Services** (`MyMemories/Services/`)

Services remain unchanged and provide business logic, data access, and utilities. ViewModels coordinate multiple services to accomplish tasks.

### 4. **Models** (`MyMemories/Models/`)

Models represent data structures and remain unchanged. They are used by both Services and ViewModels.

### 5. **Views** (`MyMemories/`)

Views (XAML + code-behind) are now thin wrappers that:
- Initialize ViewModels
- Set up data bindings
- Handle pure UI concerns (animations, focus, etc.)
- Pass UI elements to services when needed (WebView2, TextBox, etc.)

## MVVM Patterns and Best Practices

### Property Change Notification

```csharp
private string _statusText = "Ready";

public string StatusText
{
    get => _statusText;
    set => SetProperty(ref _statusText, value);
}
```

### Computed Properties

```csharp
public bool ShowGoButton => !IsUrlLoading;

private void OnUrlLoadingChanged()
{
    OnPropertyChanged(nameof(ShowGoButton));
    OnPropertyChanged(nameof(ShowLoadingIndicator));
}
```

### Command Binding

**XAML:**
```xml
<Button Content="Load" Command="{x:Bind ViewModel.LoadUrlCommand}" />
```

**ViewModel:**
```csharp
public ICommand LoadUrlCommand { get; }

private void InitializeCommands()
{
    LoadUrlCommand = new AsyncRelayCommand(LoadUrlAsync, CanLoadUrl);
}

private bool CanLoadUrl() => !string.IsNullOrWhiteSpace(UrlText);

private async Task LoadUrlAsync()
{
    // Implementation
}
```

### Async Command Pattern

```csharp
public ICommand SaveCommand { get; }

SaveCommand = new AsyncRelayCommand(
    execute: SaveAsync,
    canExecute: () => HasChanges && !IsSaving);

private async Task SaveAsync()
{
    IsLoading = true;
    try
    {
        await _service.SaveDataAsync();
        StatusText = "Saved successfully";
    }
    catch (Exception ex)
    {
        StatusText = $"Error: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}
```

### Service Coordination

```csharp
public async Task LoadCategoryAsync(string categoryId)
{
    // Coordinate multiple services
    var category = await _categoryService.LoadAsync(categoryId);
    var links = await _linkService.GetLinksAsync(categoryId);
    var tags = await _tagService.GetTagsAsync(categoryId);
    
    // Update ViewModel state
    CurrentCategory = category;
    Links = new ObservableCollection<LinkItem>(links);
    AvailableTags = new ObservableCollection<TagItem>(tags);
}
```

## Migration Strategy

### Phase 1: Infrastructure (? Complete)
- Create ViewModelBase
- Create Command infrastructure (RelayCommand, AsyncRelayCommand)
- Create core ViewModels (MainWindowViewModel, TreeViewViewModel, SearchViewModel)

### Phase 2: MainWindow Integration (In Progress)
1. Add ViewModel property to MainWindow
2. Initialize ViewModel in constructor
3. Update XAML bindings to use ViewModel
4. Move event handlers to Commands
5. Test functionality

### Phase 3: Additional ViewModels
- CategoryViewModel
- LinkViewModel
- ConfigurationViewModel
- BookmarkViewModel

### Phase 4: Cleanup
- Remove obsolete code from MainWindow.xaml.cs
- Move remaining logic to appropriate ViewModels
- Update documentation

## Benefits of This Architecture

### 1. **Separation of Concerns**
- UI logic separated from business logic
- ViewModels are UI-framework agnostic
- Services remain focused on specific tasks

### 2. **Testability**
- ViewModels can be unit tested without UI
- Commands can be tested independently
- Mock services for isolated testing

### 3. **Maintainability**
- Smaller, focused classes
- Clear responsibilities
- Easier to locate and fix issues

### 4. **Reusability**
- ViewModels can be reused in different views
- Commands are composable
- Services shared across ViewModels

### 5. **Data Binding**
- Automatic UI updates via INotifyPropertyChanged
- Two-way binding support
- Reduced boilerplate code

## Code Examples

### Before (Code-Behind)
```csharp
private void RefreshButton_Click(object sender, RoutedEventArgs e)
{
    var url = UrlTextBox.Text?.Trim();
    if (string.IsNullOrWhiteSpace(url))
    {
        StatusText.Text = "Please enter a URL";
        return;
    }
    
    IsUrlLoading = true;
    await LoadUrlAsync(url);
}
```

### After (MVVM)

**XAML:**
```xml
<TextBox Text="{x:Bind ViewModel.UrlText, Mode=TwoWay}" />
<Button Command="{x:Bind ViewModel.RefreshUrlCommand}" />
<TextBlock Text="{x:Bind ViewModel.StatusText, Mode=OneWay}" />
```

**ViewModel:**
```csharp
public string UrlText { get; set; }
public string StatusText { get; set; }
public ICommand RefreshUrlCommand { get; }

private async Task RefreshUrlAsync()
{
    if (string.IsNullOrWhiteSpace(UrlText))
    {
        StatusText = "Please enter a URL";
        return;
    }
    
    IsUrlLoading = true;
    await LoadUrlAsync(UrlText);
}
```

## Testing Examples

### Unit Testing ViewModels
```csharp
[TestClass]
public class MainWindowViewModelTests
{
    [TestMethod]
    public async Task LoadUrl_WithValidUrl_UpdatesStatusText()
    {
        // Arrange
        var mockService = new Mock<IDetailsViewService>();
        var viewModel = new MainWindowViewModel(...services);
        viewModel.UrlText = "https://example.com";
        
        // Act
        await viewModel.LoadUrlCommand.ExecuteAsync();
        
        // Assert
        Assert.IsTrue(viewModel.StatusText.Contains("Loaded"));
        mockService.Verify(s => s.ShowContentWebAsync(It.IsAny<string>()), Times.Once);
    }
    
    [TestMethod]
    public void UrlText_WhenEmpty_DisablesLoadCommand()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(...services);
        viewModel.UrlText = string.Empty;
        
        // Act & Assert
        Assert.IsFalse(viewModel.LoadUrlCommand.CanExecute(null));
    }
}
```

### Integration Testing
```csharp
[TestClass]
public class TreeViewViewModelIntegrationTests
{
    [TestMethod]
    public async Task SelectNode_LoadsNodeDetails()
    {
        // Arrange
        var services = CreateRealServices(); // Use real services
        var viewModel = new TreeViewViewModel(...services);
        var testNode = CreateTestCategoryNode();
        
        // Act
        viewModel.SelectNodeCommand.Execute(testNode);
        await Task.Delay(100); // Wait for async operations
        
        // Assert
        Assert.AreEqual(testNode, viewModel.SelectedNode);
        Assert.IsTrue(viewModel.IsSelectedNodeCategory);
    }
}
```

## Additional Resources

- [WinUI 3 Data Binding](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/data-binding-overview)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [Community MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)

## Future Enhancements

1. **Dependency Injection**: Use DI container for service/ViewModel creation
2. **Messenger Pattern**: For decoupled communication between ViewModels
3. **State Management**: Centralized application state
4. **Navigation Service**: MVVM-friendly navigation
5. **Validation**: Built-in property validation support
