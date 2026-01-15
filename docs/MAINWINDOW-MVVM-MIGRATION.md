# MainWindow MVVM Migration Guide

## Overview

This guide outlines how to migrate MainWindow from a code-heavy approach to a proper MVVM architecture using MainWindowViewModel.

## Step-by-Step Migration

### Step 1: Add ViewModel to MainWindow

**In MainWindow.xaml.cs:**

```csharp
public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    
    public MainWindowViewModel ViewModel => _viewModel!;
    
    public MainWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - File Viewer";
        SetWindowIcon();
        _ = InitializeAsync();
    }
    
    private async Task InitializeAsync()
    {
        try
        {
            // Initialize WebView2 controls FIRST (UI requirement)
            await WebViewer.EnsureCoreWebView2Async();
            await ContentTabWeb.EnsureCoreWebView2Async();

            // Initialize services (existing pattern)
            await InitializeServicesAsync();
            
            // Create and initialize ViewModel
            _viewModel = new MainWindowViewModel(
                _categoryService!,
                _configService!,
                _treeViewService!,
                _fileViewerService!,
                _detailsViewService!,
                _linkSelectionService!,
                _catalogService!);
            
            // Wire up ViewModel events
            _viewModel.RequestExit += OnRequestExit;
            
            // Initialize ViewModel (replaces most InitializeAsync logic)
            await _viewModel.InitializeAsync();
            
            // Setup WebView navigation events
            SetupWebViewEvents();
        }
        catch (Exception ex)
        {
            await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(
                ex, 
                "Failed to initialize the application.",
                "MainWindow.InitializeAsync");
        }
    }
    
    private async Task InitializeServicesAsync()
    {
        // Existing service initialization code
        _configService = new ConfigurationService();
        await _configService.LoadConfigurationAsync();
        
        GlobalExceptionHandler.Instance.Initialize(
            _configService.ErrorLogService, 
            Content.XamlRoot);
        
        // ... rest of service initialization
    }
    
    private void SetupWebViewEvents()
    {
        // WebViewer events
        WebViewer.CoreWebView2.NavigationStarting += (s, e) =>
        {
            _viewModel!.UrlText = e.Uri;
            _viewModel.IsUrlLoading = true;
        };
        
        WebViewer.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            _viewModel!.IsUrlLoading = false;
        };
        
        // ContentTabWeb events
        ContentTabWeb.CoreWebView2.NavigationStarting += (s, e) =>
        {
            _viewModel!.UrlText = e.Uri;
            _viewModel.IsUrlLoading = true;
        };
        
        ContentTabWeb.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            _viewModel!.IsUrlLoading = false;
        };
    }
    
    private void OnRequestExit()
    {
        // Clear cached passwords
        _categoryService?.ClearPasswordCache();
        Close();
    }
}
```

### Step 2: Update XAML Bindings

**In MainWindow.xaml:**

```xml
<Window
    x:Class="MyMemories.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Status Bar -->
    <TextBlock 
        x:Name="StatusText"
        Text="{x:Bind ViewModel.StatusText, Mode=OneWay}" />
    
    <!-- URL Bar -->
    <TextBox 
        x:Name="UrlTextBox"
        Text="{x:Bind ViewModel.UrlText, Mode=TwoWay}"
        KeyDown="UrlTextBox_KeyDown" />
    
    <!-- Refresh/Go Button -->
    <Button 
        x:Name="RefreshUrlButton"
        Command="{x:Bind ViewModel.RefreshUrlCommand}"
        IsEnabled="{x:Bind ViewModel.IsRefreshButtonEnabled, Mode=OneWay}">
        
        <!-- Go Icon Panel -->
        <StackPanel 
            x:Name="GoIconPanel"
            Visibility="{x:Bind ViewModel.ShowGoButton, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
            <FontIcon Glyph="&#xE72A;" />
        </StackPanel>
        
        <!-- Loading Panel -->
        <StackPanel 
            x:Name="LoadingPanel"
            Visibility="{x:Bind ViewModel.ShowLoadingIndicator, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
            <ProgressRing IsActive="True" Width="16" Height="16" />
        </StackPanel>
    </Button>
</Window>
```

### Step 3: Migrate Event Handlers to Commands

**Before (Event Handler):**
```csharp
private void RefreshUrlButton_Click(object sender, RoutedEventArgs e)
{
    LoadUrlFromTextBox();
}

private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter)
    {
        LoadUrlFromTextBox();
        e.Handled = true;
    }
}

private async void LoadUrlFromTextBox()
{
    var urlText = UrlTextBox.Text?.Trim();
    
    if (string.IsNullOrWhiteSpace(urlText))
    {
        StatusText.Text = "Please enter a URL";
        return;
    }
    
    // ... loading logic
}
```

**After (Command in ViewModel):**
```csharp
// In MainWindowViewModel
public ICommand LoadUrlCommand { get; private set; }

private void InitializeCommands()
{
    LoadUrlCommand = new AsyncRelayCommand(
        LoadUrlAsync, 
        () => !string.IsNullOrWhiteSpace(UrlText));
}

private async Task LoadUrlAsync()
{
    if (string.IsNullOrWhiteSpace(UrlText))
    {
        StatusText = "Please enter a URL";
        return;
    }
    
    // Loading logic here
}
```

**In MainWindow.xaml.cs (keep minimal UI-specific handler):**
```csharp
private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter && ViewModel.LoadUrlCommand.CanExecute(null))
    {
        ViewModel.LoadUrlCommand.Execute(null);
        e.Handled = true;
    }
}
```

### Step 4: Remove or Reduce Responsibilities

**Move to ViewModel:**
- ? URL loading logic ? MainWindowViewModel.LoadUrlAsync()
- ? Category loading ? MainWindowViewModel.LoadAllCategoriesAsync()
- ? Status text management ? MainWindowViewModel.StatusText property
- ? URL loading state ? MainWindowViewModel.IsUrlLoading property

**Keep in MainWindow:**
- ? WebView2 initialization (UI-specific)
- ? Window icon setup (UI-specific)
- ? UI element setup (splitters, panels, etc.)
- ? Pure event routing (KeyDown, etc.)

**Move to Services (if not already there):**
- Configuration validation
- Password prompting
- Backup checking
- Folder change detection

## Property Migration Reference

| MainWindow Property | Migrate To | New Location |
|-------------------|-----------|--------------|
| `_statusText` | ? Remove | `MainWindowViewModel.StatusText` |
| `_isUrlLoading` | ? Remove | `MainWindowViewModel.IsUrlLoading` |
| `_urlText` | ? Remove | `MainWindowViewModel.UrlText` |
| `_searchResults` | ? Remove | `SearchViewModel.SearchResults` |
| `_currentSearchIndex` | ? Remove | `SearchViewModel.CurrentSearchIndex` |
| `_lastSearchText` | ? Remove | `SearchViewModel.LastSearchText` |
| `_contextMenuNode` | ? Remove | `TreeViewViewModel.ContextMenuNode` |
| `_lastUsedCategory` | ? Remove | `MainWindowViewModel.LastUsedCategory` |
| Service fields | ? Keep | MainWindow (but inject into ViewModels) |

## Method Migration Reference

| MainWindow Method | Action | New Location |
|------------------|--------|--------------|
| `LoadUrlFromTextBox()` | ? Move | `MainWindowViewModel.LoadUrlAsync()` |
| `LoadAllCategoriesAsync()` | ? Move | `MainWindowViewModel.LoadAllCategoriesAsync()` |
| `CheckOutdatedBackupsAsync()` | ? Move | `MainWindowViewModel.CheckOutdatedBackupsAsync()` |
| `PromptForGlobalPasswordIfNeededAsync()` | ? Move | `MainWindowViewModel.PromptForGlobalPasswordIfNeededAsync()` |
| `RefreshUrlButton_Click()` | ? Replace | `MainWindowViewModel.RefreshUrlCommand` |
| `MenuFile_Exit_Click()` | ? Replace | `MainWindowViewModel.ExitApplicationCommand` |
| `SetWindowIcon()` | ? Keep | MainWindow (UI-specific) |
| `InitializeAsync()` | ?? Split | Keep service init, move logic to ViewModel |

## Event Migration

| MainWindow Event | Migrate To |
|-----------------|-----------|
| `PropertyChanged` | ? Remove (use ViewModel.PropertyChanged) |
| Custom events | ? Move to ViewModel or keep if UI-specific |

## Partial Class Migration

The MainWindow is split across multiple partial classes. Here's how to handle each:

### MainWindow.xaml.cs (Main)
- ? Keep: Constructor, window setup, service initialization
- ? Move: Business logic ? ViewModel
- ? Keep: WebView2 setup (UI requirement)

### MainWindow.Categories.cs
- ? Move most logic to MainWindowViewModel or CategoryViewModel
- ? Keep only UI-specific category operations

### MainWindow.Links.cs
- ? Move logic to TreeViewViewModel or LinkViewModel
- ? Keep only UI event routing

### MainWindow.Config.cs
- ? Create ConfigurationViewModel
- ? Move configuration logic there

### MainWindow.Search.cs
- ? Move to SearchViewModel (already created)

### MainWindow.TreeView.cs
- ? Move to TreeViewViewModel (already created)

## Testing the Migration

### 1. Verify Bindings
```csharp
[TestMethod]
public void StatusText_UpdatesUI()
{
    var viewModel = new MainWindowViewModel(...);
    bool propertyChanged = false;
    
    viewModel.PropertyChanged += (s, e) => 
    {
        if (e.PropertyName == nameof(MainWindowViewModel.StatusText))
            propertyChanged = true;
    };
    
    viewModel.StatusText = "Test";
    
    Assert.IsTrue(propertyChanged);
}
```

### 2. Verify Commands
```csharp
[TestMethod]
public async Task LoadUrlCommand_LoadsUrl()
{
    var mockService = new Mock<IDetailsViewService>();
    var viewModel = new MainWindowViewModel(..., mockService.Object, ...);
    
    viewModel.UrlText = "https://example.com";
    await ((AsyncRelayCommand)viewModel.LoadUrlCommand).ExecuteAsync();
    
    mockService.Verify(s => s.ShowContentWebAsync(It.IsAny<string>()), Times.Once);
}
```

### 3. Manual Testing Checklist
- [ ] URL loading works (enter URL and press Enter)
- [ ] Refresh button updates correctly
- [ ] Loading indicator shows during URL load
- [ ] Status text updates correctly
- [ ] Categories load on startup
- [ ] All existing functionality still works

## Common Issues and Solutions

### Issue: Binding not updating
**Solution:** Ensure `Mode=OneWay` or `Mode=TwoWay` is set correctly:
```xml
<TextBlock Text="{x:Bind ViewModel.StatusText, Mode=OneWay}" />
```

### Issue: Command not executing
**Solution:** Check `CanExecute` logic and ensure it returns true:
```csharp
LoadUrlCommand = new AsyncRelayCommand(
    LoadUrlAsync, 
    () => !string.IsNullOrWhiteSpace(UrlText) // Must return true
);
```

### Issue: Property changes not notifying
**Solution:** Use `SetProperty()` instead of direct assignment:
```csharp
// Wrong
public string StatusText { get; set; }

// Correct
private string _statusText;
public string StatusText
{
    get => _statusText;
    set => SetProperty(ref _statusText, value);
}
```

### Issue: ViewModel is null
**Solution:** Ensure ViewModel is initialized before XAML bindings:
```csharp
public MainWindow()
{
    _viewModel = new MainWindowViewModel(...);
    this.InitializeComponent(); // Bindings evaluate after this
}
```

## Gradual Migration Approach

If you want to migrate gradually:

1. **Phase 1**: Keep existing code working, add ViewModel
2. **Phase 2**: Duplicate critical paths (old + new) for testing
3. **Phase 3**: Switch XAML bindings one section at a time
4. **Phase 4**: Remove old code once new code is verified
5. **Phase 5**: Refactor and clean up

## Next Steps

After MainWindow migration:
1. Create CategoryViewModel for category operations
2. Create LinkViewModel for link operations  
3. Create ConfigurationViewModel for settings
4. Create BookmarkViewModel for bookmark management
5. Update all XAML files to use ViewModels

## Resources

- See `docs/MVVM-ARCHITECTURE.md` for architecture details
- See `ViewModels/` folder for ViewModel examples
- See `Commands/` folder for command implementations
