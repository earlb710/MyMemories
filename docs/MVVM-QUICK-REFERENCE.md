# MVVM Quick Reference

## Creating a New ViewModel

```csharp
using MyMemories.ViewModels;
using MyMemories.Commands;
using System.Windows.Input;

namespace MyMemories.ViewModels;

public class MyFeatureViewModel : ViewModelBase
{
    private readonly IMyService _service;
    private string _myProperty = string.Empty;
    
    public MyFeatureViewModel(IMyService service)
    {
        _service = service;
        InitializeCommands();
    }
    
    // Property with change notification
    public string MyProperty
    {
        get => _myProperty;
        set => SetProperty(ref _myProperty, value);
    }
    
    // Computed property
    public bool IsValid => !string.IsNullOrEmpty(MyProperty);
    
    // Command
    public ICommand MyCommand { get; private set; } = null!;
    
    private void InitializeCommands()
    {
        MyCommand = new AsyncRelayCommand(
            ExecuteMyCommandAsync,
            () => IsValid);
    }
    
    private async Task ExecuteMyCommandAsync()
    {
        // Implementation
        await _service.DoSomethingAsync();
        
        // Update dependent properties
        OnPropertyChanged(nameof(IsValid));
    }
}
```

## Binding in XAML

### One-Way Binding (View ? ViewModel)
```xml
<TextBlock Text="{x:Bind ViewModel.StatusText, Mode=OneWay}" />
```

### Two-Way Binding (View ? ViewModel)
```xml
<TextBox Text="{x:Bind ViewModel.SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

### Command Binding
```xml
<Button Content="Save" Command="{x:Bind ViewModel.SaveCommand}" />
```

### Visibility Binding
```xml
<ProgressRing Visibility="{x:Bind ViewModel.IsLoading, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}" />
```

### List Binding
```xml
<ListView ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}"
          SelectedItem="{x:Bind ViewModel.SelectedItem, Mode=TwoWay}">
```

## Command Patterns

### Simple Synchronous Command
```csharp
public ICommand ClearCommand { get; }

ClearCommand = new RelayCommand(
    execute: Clear,
    canExecute: () => HasData);

private void Clear()
{
    Data = string.Empty;
}
```

### Async Command
```csharp
public ICommand LoadCommand { get; }

LoadCommand = new AsyncRelayCommand(
    execute: LoadAsync,
    canExecute: () => !IsLoading);

private async Task LoadAsync()
{
    IsLoading = true;
    try
    {
        Data = await _service.LoadDataAsync();
    }
    finally
    {
        IsLoading = false;
    }
}
```

### Command with Parameter
```csharp
public ICommand DeleteCommand { get; }

DeleteCommand = new RelayCommand<ItemModel>(
    execute: DeleteItem,
    canExecute: item => item != null && item.CanDelete);

private void DeleteItem(ItemModel? item)
{
    if (item != null)
    {
        Items.Remove(item);
    }
}
```

### Async Command with Parameter
```csharp
public ICommand SaveItemCommand { get; }

SaveItemCommand = new AsyncRelayCommand<ItemModel>(
    execute: async item => await SaveItemAsync(item),
    canExecute: item => item != null && item.IsValid);

private async Task SaveItemAsync(ItemModel? item)
{
    if (item != null)
    {
        await _service.SaveAsync(item);
    }
}
```

## Property Patterns

### Simple Property
```csharp
private string _name = string.Empty;

public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### Property with Side Effects
```csharp
private string _searchText = string.Empty;

public string SearchText
{
    get => _searchText;
    set => SetProperty(ref _searchText, value, OnSearchTextChanged);
}

private void OnSearchTextChanged()
{
    // Update dependent properties
    OnPropertyChanged(nameof(HasSearchText));
    
    // Update commands
    ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
}
```

### Computed Property
```csharp
public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

// Notify when base property changes
private void OnSearchTextChanged()
{
    OnPropertyChanged(nameof(HasSearchText));
}
```

### Observable Collection
```csharp
private ObservableCollection<ItemModel> _items = new();

public ObservableCollection<ItemModel> Items
{
    get => _items;
    set => SetProperty(ref _items, value);
}
```

## Common Scenarios

### Loading Data on Initialization
```csharp
public async Task InitializeAsync()
{
    IsLoading = true;
    try
    {
        var data = await _service.LoadDataAsync();
        Items = new ObservableCollection<ItemModel>(data);
        StatusText = $"Loaded {Items.Count} items";
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

### Search with Results
```csharp
private string _searchText = string.Empty;
private ObservableCollection<ItemModel> _results = new();
private bool _isSearching;

public string SearchText
{
    get => _searchText;
    set => SetProperty(ref _searchText, value);
}

public ObservableCollection<ItemModel> Results
{
    get => _results;
    set => SetProperty(ref _results, value);
}

public bool IsSearching
{
    get => _isSearching;
    set => SetProperty(ref _isSearching, value);
}

public ICommand SearchCommand { get; }

SearchCommand = new AsyncRelayCommand(
    SearchAsync,
    () => !string.IsNullOrWhiteSpace(SearchText) && !IsSearching);

private async Task SearchAsync()
{
    IsSearching = true;
    try
    {
        var results = await _service.SearchAsync(SearchText);
        Results = new ObservableCollection<ItemModel>(results);
    }
    finally
    {
        IsSearching = false;
    }
}
```

### Master-Detail Pattern
```csharp
private ObservableCollection<ItemModel> _items = new();
private ItemModel? _selectedItem;

public ObservableCollection<ItemModel> Items
{
    get => _items;
    set => SetProperty(ref _items, value);
}

public ItemModel? SelectedItem
{
    get => _selectedItem;
    set => SetProperty(ref _selectedItem, value, OnSelectedItemChanged);
}

private void OnSelectedItemChanged()
{
    // Update dependent properties
    OnPropertyChanged(nameof(HasSelection));
    
    // Update commands
    ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
    ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
    
    // Load details
    _ = LoadDetailsAsync();
}

public bool HasSelection => SelectedItem != null;
```

### Validation
```csharp
private string _email = string.Empty;
private string _emailError = string.Empty;

public string Email
{
    get => _email;
    set => SetProperty(ref _email, value, ValidateEmail);
}

public string EmailError
{
    get => _emailError;
    private set => SetProperty(ref _emailError, value);
}

public bool IsEmailValid => string.IsNullOrEmpty(EmailError);

private void ValidateEmail()
{
    if (string.IsNullOrWhiteSpace(Email))
    {
        EmailError = "Email is required";
    }
    else if (!Email.Contains("@"))
    {
        EmailError = "Invalid email format";
    }
    else
    {
        EmailError = string.Empty;
    }
    
    OnPropertyChanged(nameof(IsEmailValid));
    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
}
```

## Testing ViewModels

### Basic Property Test
```csharp
[TestMethod]
public void Name_WhenSet_RaisesPropertyChanged()
{
    // Arrange
    var vm = new MyViewModel();
    bool raised = false;
    vm.PropertyChanged += (s, e) => 
    {
        if (e.PropertyName == nameof(MyViewModel.Name))
            raised = true;
    };
    
    // Act
    vm.Name = "Test";
    
    // Assert
    Assert.IsTrue(raised);
    Assert.AreEqual("Test", vm.Name);
}
```

### Command Execution Test
```csharp
[TestMethod]
public async Task LoadCommand_LoadsData()
{
    // Arrange
    var mockService = new Mock<IDataService>();
    mockService.Setup(s => s.LoadDataAsync())
        .ReturnsAsync(new[] { "Item1", "Item2" });
    
    var vm = new MyViewModel(mockService.Object);
    
    // Act
    await ((AsyncRelayCommand)vm.LoadCommand).ExecuteAsync();
    
    // Assert
    Assert.AreEqual(2, vm.Items.Count);
    mockService.Verify(s => s.LoadDataAsync(), Times.Once);
}
```

### CanExecute Test
```csharp
[TestMethod]
public void SaveCommand_WhenInvalid_CannotExecute()
{
    // Arrange
    var vm = new MyViewModel();
    vm.Name = string.Empty; // Invalid state
    
    // Act
    bool canExecute = vm.SaveCommand.CanExecute(null);
    
    // Assert
    Assert.IsFalse(canExecute);
}
```

## Best Practices

1. ? **Always use SetProperty()** for property changes
2. ? **Initialize commands in constructor** or dedicated method
3. ? **Use async commands for async operations**
4. ? **Check CanExecute before command execution**
5. ? **Update dependent properties** after changes
6. ? **Raise CanExecuteChanged** when command state changes
7. ? **Don't reference UI elements** in ViewModels
8. ? **Don't put UI logic** in ViewModels
9. ? **Don't forget Mode=OneWay/TwoWay** in bindings
10. ? **Keep ViewModels testable** - inject dependencies

## Common Mistakes

### ? Not raising PropertyChanged
```csharp
// WRONG
public string Name { get; set; }

// RIGHT
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### ? Updating computed properties manually
```csharp
// WRONG
public bool IsValid { get; set; }

private void OnNameChanged()
{
    IsValid = !string.IsNullOrEmpty(Name);
}

// RIGHT
public bool IsValid => !string.IsNullOrEmpty(Name);

private void OnNameChanged()
{
    OnPropertyChanged(nameof(IsValid));
}
```

### ? Forgetting to update CanExecute
```csharp
// WRONG
private void OnDataChanged()
{
    // Command state not updated
}

// RIGHT
private void OnDataChanged()
{
    OnPropertyChanged(nameof(HasData));
    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
}
```

### ? Not handling async exceptions
```csharp
// WRONG
private async Task LoadAsync()
{
    Data = await _service.LoadAsync(); // Can throw
}

// RIGHT
private async Task LoadAsync()
{
    try
    {
        Data = await _service.LoadAsync();
        StatusText = "Loaded successfully";
    }
    catch (Exception ex)
    {
        StatusText = $"Error: {ex.Message}";
        await _logger.LogErrorAsync(ex);
    }
}
```

## Performance Tips

1. **Batch property updates** - update multiple properties, then notify once
2. **Use ObservableCollection** instead of List for bound collections
3. **Avoid expensive operations** in property getters
4. **Debounce rapid changes** (e.g., search text input)
5. **Lazy load data** - don't load everything upfront
6. **Use virtual/incremental loading** for large lists

## Resources

- Full architecture: `docs/MVVM-ARCHITECTURE.md`
- Migration guide: `docs/MAINWINDOW-MVVM-MIGRATION.md`
- Example ViewModels: `MyMemories/ViewModels/`
- Command classes: `MyMemories/Commands/`
