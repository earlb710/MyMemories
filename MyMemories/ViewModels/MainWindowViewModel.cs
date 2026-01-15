using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Commands;
using MyMemories.Models;
using MyMemories.Services;
using MyMemories.Utilities;

namespace MyMemories.ViewModels;

/// <summary>
/// ViewModel for the main window, managing application state and commands.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly CategoryService _categoryService;
    private readonly ConfigurationService _configService;
    private readonly TreeViewService _treeViewService;
    private readonly FileViewerService _fileViewerService;
    private readonly DetailsViewService _detailsViewService;
    private readonly LinkSelectionService _linkSelectionService;
    private readonly CatalogService _catalogService;
    
    private string _statusText = "Ready";
    private bool _isUrlLoading;
    private string _urlText = string.Empty;
    private int _currentSearchIndex = -1;
    private string _lastSearchText = string.Empty;
    private List<SearchResult> _searchResults = new();
    private TreeViewNode? _lastUsedCategory;

    /// <summary>
    /// Initializes a new instance of MainWindowViewModel.
    /// </summary>
    public MainWindowViewModel(
        CategoryService categoryService,
        ConfigurationService configService,
        TreeViewService treeViewService,
        FileViewerService fileViewerService,
        DetailsViewService detailsViewService,
        LinkSelectionService linkSelectionService,
        CatalogService catalogService)
    {
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _treeViewService = treeViewService ?? throw new ArgumentNullException(nameof(treeViewService));
        _fileViewerService = fileViewerService ?? throw new ArgumentNullException(nameof(fileViewerService));
        _detailsViewService = detailsViewService ?? throw new ArgumentNullException(nameof(detailsViewService));
        _linkSelectionService = linkSelectionService ?? throw new ArgumentNullException(nameof(linkSelectionService));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));

        InitializeCommands();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the status text displayed in the status bar.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets or sets whether a URL is currently loading.
    /// </summary>
    public bool IsUrlLoading
    {
        get => _isUrlLoading;
        set => SetProperty(ref _isUrlLoading, value, OnUrlLoadingChanged);
    }

    /// <summary>
    /// Gets or sets the URL text in the URL bar.
    /// </summary>
    public string UrlText
    {
        get => _urlText;
        set => SetProperty(ref _urlText, value);
    }

    /// <summary>
    /// Gets or sets the current search index.
    /// </summary>
    public int CurrentSearchIndex
    {
        get => _currentSearchIndex;
        set => SetProperty(ref _currentSearchIndex, value);
    }

    /// <summary>
    /// Gets or sets the last search text.
    /// </summary>
    public string LastSearchText
    {
        get => _lastSearchText;
        set => SetProperty(ref _lastSearchText, value);
    }

    /// <summary>
    /// Gets the search results.
    /// </summary>
    public List<SearchResult> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    /// <summary>
    /// Gets or sets the last used category node.
    /// </summary>
    public TreeViewNode? LastUsedCategory
    {
        get => _lastUsedCategory;
        set => SetProperty(ref _lastUsedCategory, value);
    }

    /// <summary>
    /// Gets whether the URL bar should show the go button (not loading).
    /// </summary>
    public bool ShowGoButton => !IsUrlLoading;

    /// <summary>
    /// Gets whether the URL bar should show the loading indicator.
    /// </summary>
    public bool ShowLoadingIndicator => IsUrlLoading;

    /// <summary>
    /// Gets whether the refresh button is enabled.
    /// </summary>
    public bool IsRefreshButtonEnabled => !IsUrlLoading;

    #endregion

    #region Commands

    /// <summary>
    /// Command to load a URL from the URL text box.
    /// </summary>
    public ICommand LoadUrlCommand { get; private set; } = null!;

    /// <summary>
    /// Command to refresh the current URL.
    /// </summary>
    public ICommand RefreshUrlCommand { get; private set; } = null!;

    /// <summary>
    /// Command to load all categories.
    /// </summary>
    public ICommand LoadCategoriesCommand { get; private set; } = null!;

    /// <summary>
    /// Command to exit the application.
    /// </summary>
    public ICommand ExitApplicationCommand { get; private set; } = null!;

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes all commands.
    /// </summary>
    private void InitializeCommands()
    {
        LoadUrlCommand = new AsyncRelayCommand(LoadUrlAsync, () => !string.IsNullOrWhiteSpace(UrlText));
        RefreshUrlCommand = new AsyncRelayCommand(RefreshUrlAsync, () => !IsUrlLoading);
        LoadCategoriesCommand = new AsyncRelayCommand(LoadAllCategoriesAsync);
        ExitApplicationCommand = new RelayCommand(() => RequestExit?.Invoke());
    }

    /// <summary>
    /// Initializes the ViewModel asynchronously.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            StatusText = "Initializing...";

            // Validate configuration
            var validationPassed = await ValidateConfigurationAsync();
            if (!validationPassed)
            {
                StatusText = "?? Warning: Configuration validation failed";
            }

            // Prompt for global password if needed
            await PromptForGlobalPasswordIfNeededAsync();

            // Load categories
            await LoadAllCategoriesAsync();

            // Check for outdated backups
            await CheckOutdatedBackupsAsync();

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            StatusText = $"Initialization error: {ex.Message}";
            await GlobalExceptionHandler.Instance.HandleKnownExceptionAsync(
                ex,
                "Failed to initialize the application. Some features may not work correctly.",
                "MainWindowViewModel.InitializeAsync");
        }
    }

    #endregion

    #region URL Management

    /// <summary>
    /// Loads a URL from the URL text box.
    /// </summary>
    private async Task LoadUrlAsync()
    {
        var urlText = UrlText?.Trim();

        if (string.IsNullOrWhiteSpace(urlText))
        {
            StatusText = "Please enter a URL";
            return;
        }

        try
        {
            // Add http:// if no protocol specified
            if (!urlText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !urlText.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !urlText.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                urlText = "https://" + urlText;
            }

            if (Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri))
            {
                IsUrlLoading = true;

                // Load URL in the Content tab's WebView
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

    /// <summary>
    /// Refreshes the current URL.
    /// </summary>
    private async Task RefreshUrlAsync()
    {
        await LoadUrlAsync();
    }

    /// <summary>
    /// Updates the URL text from a redirect.
    /// </summary>
    public void UpdateUrlFromRedirect(string url)
    {
        UrlText = url;
    }

    /// <summary>
    /// Called when IsUrlLoading changes to update dependent properties.
    /// </summary>
    private void OnUrlLoadingChanged()
    {
        OnPropertyChanged(nameof(ShowGoButton));
        OnPropertyChanged(nameof(ShowLoadingIndicator));
        OnPropertyChanged(nameof(IsRefreshButtonEnabled));
        
        // Update command can-execute state
        if (RefreshUrlCommand is AsyncRelayCommand refreshCmd)
        {
            refreshCmd.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region Category Management

    /// <summary>
    /// Loads all categories.
    /// </summary>
    private async Task LoadAllCategoriesAsync()
    {
        try
        {
            StatusText = "Loading categories...";

            var categories = await _categoryService.LoadAllCategoriesAsync();

            int count = 0;
            foreach (var category in categories)
            {
                if (category.Content is CategoryItem cat && !string.IsNullOrEmpty(cat.Name))
                {
                    _treeViewService.InsertCategoryNode(category);
                    count++;
                }
            }

            // Check for folder changes and auto-refresh
            await CheckAllFoldersForChangesAsync();

            // TODO: Add GetRootNodeCount() method to TreeViewService or access TreeView directly
            // var count = _treeViewService.GetRootNodeCount();
            StatusText = count > 0
                ? $"Loaded {count} categor{(count == 1 ? "y" : "ies")}"
                : "Ready";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading categories: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] ERROR: {ex.Message}");
        }
    }

    #endregion

    #region Validation & Security

    /// <summary>
    /// Validates the configuration directories.
    /// </summary>
    private async Task<bool> ValidateConfigurationAsync()
    {
        // This would call the validation logic from MainWindow.Config.Validation.cs
        // For now, return true - implement based on your validation needs
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Prompts for global password if needed.
    /// </summary>
    private async Task PromptForGlobalPasswordIfNeededAsync()
    {
        // This would call the password prompt logic from MainWindow.Password.cs
        // Implement based on your security requirements
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks for outdated backups.
    /// </summary>
    private async Task CheckOutdatedBackupsAsync()
    {
        // This would call the backup freshness check logic
        // Implement based on your backup requirements
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks all folders for changes.
    /// </summary>
    private async Task CheckAllFoldersForChangesAsync()
    {
        // This would call the folder change detection logic
        // Implement based on your folder monitoring needs
        await Task.CompletedTask;
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the application should exit.
    /// </summary>
    public event Action? RequestExit;

    #endregion
}
