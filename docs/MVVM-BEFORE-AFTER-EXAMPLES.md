# MVVM Before/After Comparison

This document shows concrete examples of how code changes from the old pattern to the new MVVM pattern.

## Example 1: URL Loading

### ? Before (Code-Behind)

**MainWindow.xaml:**
```xml
<TextBox x:Name="UrlTextBox" KeyDown="UrlTextBox_KeyDown" />
<Button x:Name="RefreshUrlButton" Click="RefreshUrlButton_Click">
    <StackPanel x:Name="GoIconPanel" Visibility="Visible">
        <FontIcon Glyph="&#xE72A;" />
    </StackPanel>
    <StackPanel x:Name="LoadingPanel" Visibility="Collapsed">
        <ProgressRing IsActive="True" />
    </StackPanel>
</Button>
<TextBlock x:Name="StatusText" Text="Ready" />
```

**MainWindow.xaml.cs:**
```csharp
private bool _isUrlLoading;

public bool IsUrlLoading
{
    get => _isUrlLoading;
    set
    {
        if (_isUrlLoading != value)
        {
            _isUrlLoading = value;
            UpdateLoadingUI();
            OnPropertyChanged();
        }
    }
}

private void UpdateLoadingUI()
{
    if (GoIconPanel != null && LoadingPanel != null)
    {
        GoIconPanel.Visibility = _isUrlLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingPanel.Visibility = _isUrlLoading ? Visibility.Visible : Visibility.Collapsed;
        RefreshUrlButton.IsEnabled = !_isUrlLoading;
    }
}

private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter)
    {
        LoadUrlFromTextBox();
        e.Handled = true;
    }
}

private void RefreshUrlButton_Click(object sender, RoutedEventArgs e)
{
    LoadUrlFromTextBox();
}

private async void LoadUrlFromTextBox()
{
    var urlText = UrlTextBox.Text?.Trim();
    
    if (string.IsNullOrWhiteSpace(urlText))
    {
        StatusText.Text = "Please enter a URL";
        return;
    }

    try
    {
        if (!urlText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !urlText.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !urlText.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            urlText = "https://" + urlText;
        }

        if (Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri))
        {
            IsUrlLoading = true;
            await _detailsViewService!.ShowContentWebAsync(urlText);
            StatusText.Text = $"Loaded: {uri}";
        }
        else
        {
            StatusText.Text = "Invalid URL format";
        }
    }
    catch (Exception ex)
    {
        IsUrlLoading = false;
        StatusText.Text = $"Error loading URL: {ex.Message}";
    }
}
```

**Problems:**
- ? UI manipulation in code-behind
- ? Direct access to UI elements
- ? Mixed business logic and UI logic
- ? Hard to test
- ? Event handler approach
- ? Manual visibility management

### ? After (MVVM)

**MainWindow.xaml:**
```xml
<TextBox Text="{x:Bind ViewModel.UrlText, Mode=TwoWay}" KeyDown="UrlTextBox_KeyDown" />
<Button Command="{x:Bind ViewModel.RefreshUrlCommand}"
        IsEnabled="{x:Bind ViewModel.IsRefreshButtonEnabled, Mode=OneWay}">
    <StackPanel Visibility="{x:Bind ViewModel.ShowGoButton, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
        <FontIcon Glyph="&#xE72A;" />
    </StackPanel>
    <StackPanel Visibility="{x:Bind ViewModel.ShowLoadingIndicator, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
        <ProgressRing IsActive="True" />
    </StackPanel>
</Button>
<TextBlock Text="{x:Bind ViewModel.StatusText, Mode=OneWay}" />
```

**MainWindow.xaml.cs:**
```csharp
public MainWindowViewModel ViewModel => _viewModel!;

private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter && ViewModel.LoadUrlCommand.CanExecute(null))
    {
        ViewModel.LoadUrlCommand.Execute(null);
        e.Handled = true;
    }
}
```

**MainWindowViewModel.cs:**
```csharp
private string _urlText = string.Empty;
private bool _isUrlLoading;
private string _statusText = "Ready";

public string UrlText
{
    get => _urlText;
    set => SetProperty(ref _urlText, value, OnUrlTextChanged);
}

public bool IsUrlLoading
{
    get => _isUrlLoading;
    set => SetProperty(ref _isUrlLoading, value, OnUrlLoadingChanged);
}

public string StatusText
{
    get => _statusText;
    set => SetProperty(ref _statusText, value);
}

public bool ShowGoButton => !IsUrlLoading;
public bool ShowLoadingIndicator => IsUrlLoading;
public bool IsRefreshButtonEnabled => !IsUrlLoading;

public ICommand LoadUrlCommand { get; private set; } = null!;
public ICommand RefreshUrlCommand { get; private set; } = null!;

private void InitializeCommands()
{
    LoadUrlCommand = new AsyncRelayCommand(LoadUrlAsync, () => !string.IsNullOrWhiteSpace(UrlText));
    RefreshUrlCommand = new AsyncRelayCommand(LoadUrlAsync, () => !IsUrlLoading);
}

private async Task LoadUrlAsync()
{
    if (string.IsNullOrWhiteSpace(UrlText))
    {
        StatusText = "Please enter a URL";
        return;
    }

    try
    {
        var urlText = UrlText.Trim();
        
        if (!urlText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !urlText.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !urlText.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            urlText = "https://" + urlText;
        }

        if (Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri))
        {
            IsUrlLoading = true;
            await _detailsViewService.ShowContentWebAsync(urlText);
            StatusText = $"Loaded: {uri}";
        }
        else
        {
            StatusText = "Invalid URL format";
        }
    }
    catch (Exception ex)
    {
        IsUrlLoading = false;
        StatusText = $"Error loading URL: {ex.Message}";
    }
}

private void OnUrlTextChanged()
{
    ((AsyncRelayCommand)LoadUrlCommand).RaiseCanExecuteChanged();
}

private void OnUrlLoadingChanged()
{
    OnPropertyChanged(nameof(ShowGoButton));
    OnPropertyChanged(nameof(ShowLoadingIndicator));
    OnPropertyChanged(nameof(IsRefreshButtonEnabled));
    ((AsyncRelayCommand)RefreshUrlCommand).RaiseCanExecuteChanged();
}
```

**Benefits:**
- ? Automatic UI updates via bindings
- ? No direct UI element manipulation
- ? Business logic separated
- ? Testable ViewModel
- ? Command pattern
- ? Computed properties for visibility

---

## Example 2: Tree Node Selection

### ? Before (Code-Behind)

**MainWindow.xaml:**
```xml
<TreeView x:Name="LinksTreeView" ItemInvoked="LinksTreeView_ItemInvoked" />
```

**MainWindow.xaml.cs:**
```csharp
private TreeViewNode? _selectedNode;
private TreeViewNode? _contextMenuNode;

private void LinksTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
{
    var node = args.InvokedItem as TreeViewNode;
    if (node == null)
        return;
        
    _selectedNode = node;
    
    if (node.Content is LinkItem link)
    {
        _linkSelectionService?.HandleNodeSelection(node);
        UpdateDetailsView(link);
    }
    else if (node.Content is CategoryItem category)
    {
        UpdateCategoryView(category);
    }
}

private void UpdateDetailsView(LinkItem link)
{
    // Direct UI manipulation
    DetailsPanel.Visibility = Visibility.Visible;
    // ... more UI updates
}
```

**Problems:**
- ? Event-driven approach
- ? Direct state management
- ? Scattered selection logic
- ? Hard to track state changes

### ? After (MVVM)

**MainWindow.xaml:**
```xml
<TreeView ItemsSource="{x:Bind TreeViewModel.RootNodes, Mode=OneWay}"
          SelectedItem="{x:Bind TreeViewModel.SelectedNode, Mode=TwoWay}" />
```

**TreeViewViewModel.cs:**
```csharp
private TreeViewNode? _selectedNode;
private TreeViewNode? _contextMenuNode;

public TreeViewNode? SelectedNode
{
    get => _selectedNode;
    set => SetProperty(ref _selectedNode, value, OnSelectedNodeChanged);
}

public TreeViewNode? ContextMenuNode
{
    get => _contextMenuNode;
    set => SetProperty(ref _contextMenuNode, value);
}

public bool HasSelectedNode => SelectedNode != null;
public bool IsSelectedNodeCategory => SelectedNode?.Content is CategoryItem;
public bool IsSelectedNodeLink => SelectedNode?.Content is LinkItem;

private void OnSelectedNodeChanged()
{
    OnPropertyChanged(nameof(HasSelectedNode));
    OnPropertyChanged(nameof(IsSelectedNodeCategory));
    OnPropertyChanged(nameof(IsSelectedNodeLink));

    if (SelectedNode != null)
    {
        _linkSelectionService.HandleNodeSelection(SelectedNode);
    }
}
```

**Benefits:**
- ? Two-way data binding
- ? Automatic property updates
- ? Computed properties
- ? Clear state tracking
- ? Centralized logic

---

## Example 3: Search Functionality

### ? Before (Code-Behind)

**MainWindow.xaml:**
```xml
<TextBox x:Name="SearchTextBox" TextChanged="SearchTextBox_TextChanged" />
<Button x:Name="SearchButton" Click="SearchButton_Click" Content="Search" />
<Button x:Name="PrevButton" Click="PrevButton_Click" Content="Previous" />
<Button x:Name="NextButton" Click="NextButton_Click" Content="Next" />
<TextBlock x:Name="SearchStatus" />
```

**MainWindow.xaml.cs:**
```csharp
private List<SearchResult> _searchResults = new();
private int _currentSearchIndex = -1;
private string _lastSearchText = string.Empty;

private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
{
    UpdateSearchButtonState();
}

private void UpdateSearchButtonState()
{
    SearchButton.IsEnabled = !string.IsNullOrWhiteSpace(SearchTextBox.Text);
    PrevButton.IsEnabled = _currentSearchIndex > 0;
    NextButton.IsEnabled = _currentSearchIndex < _searchResults.Count - 1;
}

private async void SearchButton_Click(object sender, RoutedEventArgs e)
{
    var searchText = SearchTextBox.Text?.Trim();
    if (string.IsNullOrWhiteSpace(searchText))
        return;
        
    SearchButton.IsEnabled = false;
    SearchStatus.Text = "Searching...";
    
    try
    {
        _searchResults = await PerformSearchAsync(searchText);
        _currentSearchIndex = _searchResults.Count > 0 ? 0 : -1;
        _lastSearchText = searchText;
        
        UpdateSearchStatus();
        UpdateSearchButtonState();
    }
    finally
    {
        SearchButton.IsEnabled = true;
    }
}

private void PrevButton_Click(object sender, RoutedEventArgs e)
{
    if (_currentSearchIndex > 0)
    {
        _currentSearchIndex--;
        NavigateToResult(_currentSearchIndex);
        UpdateSearchStatus();
        UpdateSearchButtonState();
    }
}

private void NextButton_Click(object sender, RoutedEventArgs e)
{
    if (_currentSearchIndex < _searchResults.Count - 1)
    {
        _currentSearchIndex++;
        NavigateToResult(_currentSearchIndex);
        UpdateSearchStatus();
        UpdateSearchButtonState();
    }
}

private void UpdateSearchStatus()
{
    if (_searchResults.Count == 0)
    {
        SearchStatus.Text = "No results found";
    }
    else
    {
        SearchStatus.Text = $"Result {_currentSearchIndex + 1} of {_searchResults.Count}";
    }
}
```

**Problems:**
- ? Multiple event handlers
- ? Manual state synchronization
- ? Repetitive button state updates
- ? Hard to test
- ? Error-prone state management

### ? After (MVVM)

**MainWindow.xaml:**
```xml
<TextBox Text="{x:Bind SearchViewModel.SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
<Button Command="{x:Bind SearchViewModel.SearchCommand}" Content="Search" />
<Button Command="{x:Bind SearchViewModel.PreviousResultCommand}" Content="Previous" />
<Button Command="{x:Bind SearchViewModel.NextResultCommand}" Content="Next" />
<TextBlock Text="{x:Bind SearchViewModel.SearchStatus, Mode=OneWay}" />
```

**SearchViewModel.cs:**
```csharp
private string _searchText = string.Empty;
private bool _isSearching;
private int _searchResultCount;
private int _currentSearchIndex = -1;
private string _searchStatus = string.Empty;

public string SearchText
{
    get => _searchText;
    set => SetProperty(ref _searchText, value, OnSearchTextChanged);
}

public bool IsSearching
{
    get => _isSearching;
    set => SetProperty(ref _isSearching, value);
}

public int SearchResultCount
{
    get => _searchResultCount;
    set => SetProperty(ref _searchResultCount, value, OnSearchResultCountChanged);
}

public int CurrentSearchIndex
{
    get => _currentSearchIndex;
    set => SetProperty(ref _currentSearchIndex, value, OnCurrentSearchIndexChanged);
}

public string SearchStatus
{
    get => _searchStatus;
    set => SetProperty(ref _searchStatus, value);
}

public bool HasSearchResults => SearchResultCount > 0;
public bool CanNavigateToPreviousResult => HasSearchResults && CurrentSearchIndex > 0;
public bool CanNavigateToNextResult => HasSearchResults && CurrentSearchIndex < SearchResultCount - 1;

public ICommand SearchCommand { get; private set; } = null!;
public ICommand PreviousResultCommand { get; private set; } = null!;
public ICommand NextResultCommand { get; private set; } = null!;

private void InitializeCommands()
{
    SearchCommand = new AsyncRelayCommand(
        PerformSearchAsync,
        () => !string.IsNullOrWhiteSpace(SearchText) && !IsSearching);
    
    PreviousResultCommand = new RelayCommand(
        NavigateToPreviousResult,
        () => CanNavigateToPreviousResult);
    
    NextResultCommand = new RelayCommand(
        NavigateToNextResult,
        () => CanNavigateToNextResult);
}

private async Task PerformSearchAsync()
{
    IsSearching = true;
    SearchStatus = "Searching...";
    
    try
    {
        var results = await _textSearchService.SearchAsync(SearchText);
        SearchResultCount = results.Count;
        CurrentSearchIndex = SearchResultCount > 0 ? 0 : -1;
        UpdateSearchStatus();
    }
    finally
    {
        IsSearching = false;
    }
}

private void NavigateToPreviousResult()
{
    if (CanNavigateToPreviousResult)
    {
        CurrentSearchIndex--;
        NavigateToCurrentResult();
    }
}

private void NavigateToNextResult()
{
    if (CanNavigateToNextResult)
    {
        CurrentSearchIndex++;
        NavigateToCurrentResult();
    }
}

private void UpdateSearchStatus()
{
    SearchStatus = SearchResultCount == 0
        ? "No results found"
        : $"Result {CurrentSearchIndex + 1} of {SearchResultCount}";
}

private void OnSearchTextChanged()
{
    ((AsyncRelayCommand)SearchCommand).RaiseCanExecuteChanged();
}

private void OnSearchResultCountChanged()
{
    OnPropertyChanged(nameof(HasSearchResults));
    UpdateNavigationCommands();
}

private void OnCurrentSearchIndexChanged()
{
    UpdateNavigationCommands();
}

private void UpdateNavigationCommands()
{
    OnPropertyChanged(nameof(CanNavigateToPreviousResult));
    OnPropertyChanged(nameof(CanNavigateToNextResult));
    
    ((RelayCommand)PreviousResultCommand).RaiseCanExecuteChanged();
    ((RelayCommand)NextResultCommand).RaiseCanExecuteChanged();
}
```

**Benefits:**
- ? Single command for each action
- ? Automatic state management
- ? Commands handle their own CanExecute
- ? Fully testable
- ? Clean, maintainable code

---

## Summary

| Aspect | Before (Code-Behind) | After (MVVM) |
|--------|---------------------|--------------|
| **Lines of Code** | ~150 lines per feature | ~100 lines per feature |
| **Testability** | Requires UI automation | Unit testable |
| **Reusability** | Tied to specific UI | Reusable across views |
| **Maintainability** | Hard to locate logic | Clear separation |
| **State Management** | Manual, error-prone | Automatic, reliable |
| **Data Binding** | Manual sync | Automatic sync |
| **Event Handling** | Multiple handlers | Single commands |
| **UI Updates** | Manual manipulation | Declarative bindings |

## Testing Comparison

### Before - Hard to Test
```csharp
// Cannot test without UI
// Requires UI automation framework
// Brittle, slow tests
```

### After - Easy to Test
```csharp
[TestMethod]
public async Task Search_WithValidText_FindsResults()
{
    // Arrange
    var vm = new SearchViewModel(mockService);
    vm.SearchText = "test";
    
    // Act
    await ((AsyncRelayCommand)vm.SearchCommand).ExecuteAsync();
    
    // Assert
    Assert.IsTrue(vm.SearchResultCount > 0);
    Assert.AreEqual("Result 1 of 5", vm.SearchStatus);
}
```

## Migration Impact

- **Code Reduction**: 30-40% less code in MainWindow
- **Testability**: 100% ViewModel coverage possible
- **Maintainability**: Easier to locate and fix issues
- **Performance**: Better (less manual UI manipulation)
- **Development Speed**: Faster once initial setup complete
