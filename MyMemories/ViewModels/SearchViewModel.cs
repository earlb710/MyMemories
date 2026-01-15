using System;
using System.Threading.Tasks;
using System.Windows.Input;
using MyMemories.Commands;
using MyMemories.Services;

namespace MyMemories.ViewModels;

/// <summary>
/// ViewModel for managing search functionality.
/// </summary>
public class SearchViewModel : ViewModelBase
{
    private readonly TextSearchService _textSearchService;
    
    private string _searchText = string.Empty;
    private bool _isSearching;
    private int _searchResultCount;
    private int _currentSearchIndex = -1;
    private string _searchStatus = string.Empty;

    /// <summary>
    /// Initializes a new instance of SearchViewModel.
    /// </summary>
    public SearchViewModel(TextSearchService textSearchService)
    {
        _textSearchService = textSearchService ?? throw new ArgumentNullException(nameof(textSearchService));
        InitializeCommands();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value, OnSearchTextChanged);
    }

    /// <summary>
    /// Gets or sets whether a search is in progress.
    /// </summary>
    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value, OnIsSearchingChanged);
    }

    /// <summary>
    /// Gets or sets the number of search results.
    /// </summary>
    public int SearchResultCount
    {
        get => _searchResultCount;
        set => SetProperty(ref _searchResultCount, value, OnSearchResultCountChanged);
    }

    /// <summary>
    /// Gets or sets the current search result index.
    /// </summary>
    public int CurrentSearchIndex
    {
        get => _currentSearchIndex;
        set => SetProperty(ref _currentSearchIndex, value, OnCurrentSearchIndexChanged);
    }

    /// <summary>
    /// Gets or sets the search status message.
    /// </summary>
    public string SearchStatus
    {
        get => _searchStatus;
        set => SetProperty(ref _searchStatus, value);
    }

    /// <summary>
    /// Gets whether search results are available.
    /// </summary>
    public bool HasSearchResults => SearchResultCount > 0;

    /// <summary>
    /// Gets whether the previous search button should be enabled.
    /// </summary>
    public bool CanNavigateToPreviousResult => HasSearchResults && CurrentSearchIndex > 0;

    /// <summary>
    /// Gets whether the next search button should be enabled.
    /// </summary>
    public bool CanNavigateToNextResult => HasSearchResults && CurrentSearchIndex < SearchResultCount - 1;

    #endregion

    #region Commands

    /// <summary>
    /// Command to perform a search.
    /// </summary>
    public ICommand SearchCommand { get; private set; } = null!;

    /// <summary>
    /// Command to navigate to the previous search result.
    /// </summary>
    public ICommand PreviousResultCommand { get; private set; } = null!;

    /// <summary>
    /// Command to navigate to the next search result.
    /// </summary>
    public ICommand NextResultCommand { get; private set; } = null!;

    /// <summary>
    /// Command to clear the search.
    /// </summary>
    public ICommand ClearSearchCommand { get; private set; } = null!;

    #endregion

    #region Initialization

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
        
        ClearSearchCommand = new RelayCommand(ClearSearch);
    }

    #endregion

    #region Search Operations

    /// <summary>
    /// Performs a search with the current search text.
    /// </summary>
    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        try
        {
            IsSearching = true;
            SearchStatus = "Searching...";

            // Perform search through service
            // This is a placeholder - implement actual search logic
            await Task.Delay(100); // Simulate search operation

            SearchResultCount = 0; // Update based on actual results
            CurrentSearchIndex = SearchResultCount > 0 ? 0 : -1;

            UpdateSearchStatus();
        }
        catch (Exception ex)
        {
            SearchStatus = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Navigates to the previous search result.
    /// </summary>
    private void NavigateToPreviousResult()
    {
        if (CanNavigateToPreviousResult)
        {
            CurrentSearchIndex--;
            NavigateToCurrentResult();
        }
    }

    /// <summary>
    /// Navigates to the next search result.
    /// </summary>
    private void NavigateToNextResult()
    {
        if (CanNavigateToNextResult)
        {
            CurrentSearchIndex++;
            NavigateToCurrentResult();
        }
    }

    /// <summary>
    /// Navigates to the current search result.
    /// </summary>
    private void NavigateToCurrentResult()
    {
        if (CurrentSearchIndex >= 0 && CurrentSearchIndex < SearchResultCount)
        {
            // Implement navigation to result
            UpdateSearchStatus();
        }
    }

    /// <summary>
    /// Clears the current search.
    /// </summary>
    private void ClearSearch()
    {
        SearchText = string.Empty;
        SearchResultCount = 0;
        CurrentSearchIndex = -1;
        SearchStatus = string.Empty;
    }

    /// <summary>
    /// Updates the search status message.
    /// </summary>
    private void UpdateSearchStatus()
    {
        if (SearchResultCount == 0)
        {
            SearchStatus = "No results found";
        }
        else
        {
            SearchStatus = $"Result {CurrentSearchIndex + 1} of {SearchResultCount}";
        }
    }

    #endregion

    #region Event Handlers

    private void OnSearchTextChanged()
    {
        if (SearchCommand is AsyncRelayCommand cmd)
        {
            cmd.RaiseCanExecuteChanged();
        }
    }

    private void OnIsSearchingChanged()
    {
        if (SearchCommand is AsyncRelayCommand cmd)
        {
            cmd.RaiseCanExecuteChanged();
        }
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

        if (PreviousResultCommand is RelayCommand prevCmd)
        {
            prevCmd.RaiseCanExecuteChanged();
        }

        if (NextResultCommand is RelayCommand nextCmd)
        {
            nextCmd.RaiseCanExecuteChanged();
        }
    }

    #endregion
}
