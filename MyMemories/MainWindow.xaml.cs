using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WinRT.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI;

namespace MyMemories;

/// <summary>
/// Main window for the MyMemories file viewer application.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly string _dataFolder;
    private LinkDetailsDialog? _linkDialog;
    private TreeViewNode? _contextMenuNode;
    private TreeViewNode? _lastUsedCategory;
    private List<SearchResult> _searchResults = new();
    private int _currentSearchIndex = -1;
    private string _lastSearchText = string.Empty;
    private bool _isUrlLoading;

    // Services
    private CategoryService? _categoryService;
    private FileViewerService? _fileViewerService;
    private DetailsViewService? _detailsViewService;
    private TreeViewService? _treeViewService;
    private ConfigurationService? _configService;
    private TreeViewEventService? _treeViewEventService;
    private DoubleTapHandlerService? _doubleTapHandlerService;
    private CatalogService? _catalogService;
    private FileLauncherService? _fileLauncherService;
    private LinkSelectionService? _linkSelectionService;
    private UrlStateCheckerService? _urlStateCheckerService;
    private TagManagementService? _tagService;
    private RatingManagementService? _ratingService;
    private FolderPickerService? _folderPickerService;
    private ArchiveRefreshService? _archiveRefreshService;

    /// <summary>
    /// Gets or sets whether a URL is currently loading.
    /// </summary>
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

    /// <summary>
    /// Updates the loading UI state for the URL bar.
    /// </summary>
    private void UpdateLoadingUI()
    {
        if (GoIconPanel != null && LoadingPanel != null)
        {
            GoIconPanel.Visibility = _isUrlLoading ? Visibility.Collapsed : Visibility.Visible;
            LoadingPanel.Visibility = _isUrlLoading ? Visibility.Visible : Visibility.Collapsed;
            RefreshUrlButton.IsEnabled = !_isUrlLoading;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - File Viewer";

        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemories",
            "Categories"
        );

        // Set window icon
        SetWindowIcon();

        _ = InitializeAsync();
    }

    /// <summary>
    /// Sets the window icon to the book emoji icon (??).
    /// </summary>
    private void SetWindowIcon()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            
            // Path to the icon file
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Initialize WebView2 controls
            await WebViewer.EnsureCoreWebView2Async();
            await ContentTabWeb.EnsureCoreWebView2Async();

            // Initialize configuration service FIRST
            _configService = new ConfigurationService();
            await _configService.LoadConfigurationAsync();

            // Validate configuration directories BEFORE initializing services
            var validationPassed = await ValidateConfigurationDirectoriesAsync();
            if (!validationPassed)
            {
                StatusText.Text = "?? Warning: Configuration validation failed";
                // Continue anyway - user chose to proceed
            }

            // Initialize CategoryService WITH ConfigurationService
            _categoryService = new CategoryService(_configService.WorkingDirectory, _configService);
            
            // Initialize TagManagementService and load tags
            _tagService = new TagManagementService(_configService.WorkingDirectory);
            await _tagService.LoadAsync();
            
            // Initialize RatingManagementService and load rating definitions
            _ratingService = new RatingManagementService(_configService.WorkingDirectory);
            await _ratingService.LoadAsync();
            
            // Initialize other services
            _fileViewerService = new FileViewerService(ImageViewer, WebViewer, TextViewer);
            _detailsViewService = new DetailsViewService(DetailsPanel);
            _detailsViewService.SetHeaderPanel(HeaderPanel);
            
            // Setup tabbed view for Summary and Content tabs
            _detailsViewService.SetupTabbedView(
                DetailsTabView,
                SummaryPanel,
                ContentTabGrid,
                ContentTabScroll,
                ContentPanel,
                ContentTabImage,
                ContentTabTextGrid,
                ContentTabText,
                ContentTabWeb,
                ContentTabNoContent,
                ContentTabNoContentText);
            
            // Setup line number callbacks for text viewer
            _detailsViewService.SetLineNumberCallbacks(ShowLineNumbers, HideLineNumbers);
            
            _treeViewService = new TreeViewService(LinksTreeView, this);
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);
            
            // Initialize new refactored services
            _fileLauncherService = new FileLauncherService();
            _folderPickerService = new FolderPickerService(this);
            _catalogService = new CatalogService(_categoryService, _treeViewService, _detailsViewService, 
                new ZipCatalogService(_categoryService, _treeViewService),
                _configService.AuditLogService);
            
            // Initialize archive refresh service
            _archiveRefreshService = new ArchiveRefreshService(_categoryService, _catalogService, _treeViewService);
            
            // Set the refresh archive callback
            _catalogService.SetRefreshArchiveCallback(RefreshArchiveFromManifestAsync);
            
            // Initialize URL state checker service BEFORE DoubleTapHandlerService
            _urlStateCheckerService = new UrlStateCheckerService();
            
            _linkSelectionService = new LinkSelectionService(_detailsViewService, _fileViewerService, _treeViewService, _catalogService, _fileLauncherService, _categoryService, _urlStateCheckerService);
            _linkSelectionService.SetUrlTextBox(UrlTextBox); // Wire up URL text box
            _treeViewEventService = new TreeViewEventService(_detailsViewService, _treeViewService, _linkSelectionService);
            
            // Wire up URL redirect update event
            _detailsViewService.UpdateUrlFromRedirectRequested += OnUpdateUrlFromRedirect;
            
            // Initialize DoubleTapHandlerService with URL state checker, category service, and tree view service for status updates
            _doubleTapHandlerService = new DoubleTapHandlerService(_fileLauncherService, _urlStateCheckerService, _categoryService, _treeViewService);

            // Set up WebView2 navigation events to update URL bar
            // Handle both the main WebViewer (for legacy) and ContentTabWeb (for tabbed view)
            WebViewer.CoreWebView2.NavigationStarting += (s, e) =>
            {
                UrlTextBox.Text = e.Uri;
                IsUrlLoading = true;
            };
            
            WebViewer.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                IsUrlLoading = false;
            };
            
            WebViewer.CoreWebView2.SourceChanged += (s, e) =>
            {
                if (WebViewer.CoreWebView2.Source != null)
                {
                    UrlTextBox.Text = WebViewer.CoreWebView2.Source;
                }
            };
            
            // Set up ContentTabWeb navigation events for the tabbed view
            ContentTabWeb.CoreWebView2.NavigationStarting += (s, e) =>
            {
                UrlTextBox.Text = e.Uri;
                IsUrlLoading = true;
            };
            
            ContentTabWeb.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                IsUrlLoading = false;
            };
            
            ContentTabWeb.CoreWebView2.SourceChanged += (s, e) =>
            {
                if (ContentTabWeb.CoreWebView2?.Source != null)
                {
                    UrlTextBox.Text = ContentTabWeb.CoreWebView2.Source;
                }
            };

            // Check if any categories use global password and prompt BEFORE loading
            await PromptForGlobalPasswordIfNeededAsync();

            // NOW load categories (password is cached if needed)
            await LoadAllCategoriesAsync();
            
            // Check for outdated backups after categories are loaded
            await CheckOutdatedBackupsAsync();
            
            // Final cleanup: ensure no blank/invalid nodes exist
            // This catches any nodes that might have been added by the framework
            RemoveInvalidNodes();
            
            // Schedule a delayed cleanup to catch any nodes added after initial load
            // This is a workaround for potential WinUI TreeView quirks
            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(100); // Small delay to let any deferred operations complete
                RemoveInvalidNodes();
            });

            StatusText.Text = "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Initialization error: {ex.Message}";
            
            // Log the error if logging is enabled
            if (_configService?.IsLoggingEnabled() ?? false)
            {
                await _configService.LogErrorAsync("Initialization failed", ex);
            }
        }
    }

    /// <summary>
    /// Sets the maximum allowed age for backups to 30 days.
    /// </summary>
    private TimeSpan MaximumBackupAge => TimeSpan.FromDays(30);

    /// <summary>
    /// Checks all category folders for outdated backups and removes them if found.
    /// </summary>
    private async Task CheckOutdatedBackupsAsync()
    {
        if (_configService == null)
            return;

        try
        {
            // Check if user clicked "Remind Later" today - if so, skip the check
            if (!BackupFreshnessService.ShouldShowBackupReminder())
            {
                return;
            }

            StatusText.Text = "Checking backup freshness...";

            var freshnessService = new BackupFreshnessService(_configService.WorkingDirectory);
            var outdatedBackups = await freshnessService.CheckAllBackupsAsync(LinksTreeView.RootNodes);

            if (outdatedBackups.Count == 0)
            {
                // All backups are up to date
                return;
            }

            // Show dialog with outdated backups
            var dialog = new Dialogs.BackupFreshnessDialog(Content.XamlRoot);
            var backupsToUpdate = await dialog.ShowAsync(outdatedBackups);

            if (backupsToUpdate == null)
            {
                // User chose "Remind Later" - set reminder for tomorrow
                BackupFreshnessService.SetRemindLaterForTomorrow();
                StatusText.Text = "Backup check deferred until tomorrow";
                return;
            }

            if (backupsToUpdate.Count == 0)
            {
                // User chose "Ignore All"
                StatusText.Text = "Backups ignored";
                return;
            }

            // Update selected backups with progress dialog
            StatusText.Text = $"Updating {backupsToUpdate.Count} backup(s)...";
            var (succeeded, failed) = await dialog.UpdateBackupsWithProgressAsync(backupsToUpdate, freshnessService);

            // Clear the remind later date since backups were updated
            if (succeeded > 0)
            {
                BackupFreshnessService.ClearRemindLater();
            }

            // Show summary
            await dialog.ShowUpdateSummaryAsync(succeeded, failed);

            StatusText.Text = succeeded > 0 
                ? $"Updated {succeeded} backup(s)" 
                : "Backup update completed";
        }
        catch (Exception ex)
        {
            // Don't let backup check failure stop the app from starting
            StatusText.Text = $"Backup check failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if any encrypted category files exist and prompts for the global password.
    /// </summary>
    private async Task PromptForGlobalPasswordIfNeededAsync()
    {
        if (_configService == null || !_configService.HasGlobalPassword())
            return;

        var workingDir = _configService.WorkingDirectory;
        if (!Directory.Exists(workingDir))
            return;

        // Check if any .zip.json files exist (encrypted categories)
        var encryptedFiles = Directory.GetFiles(workingDir, "*.zip.json");
        if (encryptedFiles.Length == 0)
            return;

        // Prompt for global password
        var passwordDialog = new ContentDialog
        {
            Title = "Global Password Required",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "One or more categories are protected with the global password. Please enter it:",
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    new PasswordBox
                    {
                        Name = "GlobalPasswordInput",
                        PlaceholderText = "Enter global password"
                    }
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        while (true)
        {
            var result = await passwordDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var passwordBox = (passwordDialog.Content as StackPanel)
                    ?.Children.OfType<PasswordBox>()
                    .FirstOrDefault();

                if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    // Verify the password
                    var enteredPasswordHash = MyMemories.Utilities.PasswordUtilities.HashPassword(passwordBox.Password);
                    if (enteredPasswordHash == _configService.GlobalPasswordHash)
                    {
                        // Cache the global password
                        _categoryService!.CacheGlobalPassword(passwordBox.Password);
                        StatusText.Text = "Global password verified";
                        return;
                    }
                    else
                    {
                        // Log invalid password attempt to error.log
                        if (_configService.IsLoggingEnabled() && _configService.ErrorLogService != null)
                        {
                            await _configService.ErrorLogService.LogWarningAsync(
                                "Invalid global password attempt at application startup",
                                "MainWindow.PromptForGlobalPasswordIfNeededAsync");
                        }
                        
                        // Wrong password - show error and prompt again
                        var errorDialog = new ContentDialog
                        {
                            Title = "Incorrect Password",
                            Content = "The global password you entered is incorrect. Please try again.",
                            CloseButtonText = "OK",
                            XamlRoot = Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        
                        // Reset password box for retry
                        passwordBox.Password = string.Empty;
                        continue;
                    }
                }
                else
                {
                    // Empty password - show error and prompt again
                    var errorDialog = new ContentDialog
                    {
                        Title = "Password Required",
                        Content = "You must enter the global password to access protected categories.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    continue;
                }
            }
            else
            {
                // User cancelled - app will continue but encrypted categories won't load
                StatusText.Text = "Warning: Global password not provided - encrypted categories skipped";
                return;
            }
        }
    }

    private async Task LoadAllCategoriesAsync()
    {
        try
        {
            // Log initial state
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] START - RootNodes.Count: {LinksTreeView.RootNodes.Count}");
            
            // Clear any existing nodes (including any blank placeholder nodes)
            LinksTreeView.RootNodes.Clear();
            
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] After Clear - RootNodes.Count: {LinksTreeView.RootNodes.Count}");
            
            var categories = await _categoryService!.LoadAllCategoriesAsync();

            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] Loaded {categories.Count} categories from service");

            foreach (var category in categories)
            {
                // Only add valid category nodes
                if (category.Content is CategoryItem cat)
                {
                    if (!string.IsNullOrEmpty(cat.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] Adding category: {cat.Name}");
                        _treeViewService!.InsertCategoryNode(category);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] Skipping category with empty name");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] Skipping node with content type: {category.Content?.GetType().Name ?? "null"}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] After adding categories - RootNodes.Count: {LinksTreeView.RootNodes.Count}");
            
            // Safety check: Remove any nodes with null or invalid content that may have been added
            RemoveInvalidNodes();
            
            // Update bookmark lookup categories
            UpdateBookmarkLookupCategories();
            
            // Check for folder changes and auto-refresh if enabled
            await CheckAllFoldersForChangesAsync();

            StatusText.Text = LinksTreeView.RootNodes.Count > 0
                ? $"Loaded {LinksTreeView.RootNodes.Count} categor{(LinksTreeView.RootNodes.Count == 1 ? "y" : "ies")}"
                : "Ready";
                
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] END - RootNodes.Count: {LinksTreeView.RootNodes.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadAllCategoriesAsync] ERROR: {ex.Message}");
            StatusText.Text = $"Error loading categories: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Removes any nodes with null or invalid content from the TreeView.
    /// Also removes LinkItems from root level (only CategoryItems allowed at root).
    /// </summary>
    private void RemoveInvalidNodes()
    {
        // Log all nodes for debugging
        System.Diagnostics.Debug.WriteLine($"[RemoveInvalidNodes] RootNodes.Count before cleanup: {LinksTreeView.RootNodes.Count}");
        
        for (int i = 0; i < LinksTreeView.RootNodes.Count; i++)
        {
            var node = LinksTreeView.RootNodes[i];
            var contentType = node.Content?.GetType().Name ?? "null";
            var contentName = node.Content switch
            {
                CategoryItem cat => cat.Name ?? "(null name)",
                LinkItem link => link.Title ?? "(null title)",
                _ => "(unknown)"
            };
            System.Diagnostics.Debug.WriteLine($"[RemoveInvalidNodes] Node[{i}]: Content type={contentType}, Name/Title={contentName}");
        }
        
        // Collect nodes to remove (can't modify collection while iterating)
        var nodesToRemove = new List<TreeViewNode>();
        
        foreach (var node in LinksTreeView.RootNodes)
        {
            bool shouldRemove = false;
            string reason = "";
            
            if (node.Content == null)
            {
                shouldRemove = true;
                reason = "Content is null";
            }
            else if (node.Content is LinkItem link)
            {
                // LinkItems should NEVER be at root level
                shouldRemove = true;
                reason = $"LinkItem at root level: '{link.Title}' - only CategoryItems allowed at root";
            }
            else if (node.Content is CategoryItem cat && string.IsNullOrEmpty(cat.Name))
            {
                shouldRemove = true;
                reason = "CategoryItem with empty name";
            }
            else if (node.Content is not CategoryItem)
            {
                shouldRemove = true;
                reason = $"Unknown content type at root: {node.Content.GetType().Name} - only CategoryItems allowed";
            }
            
            if (shouldRemove)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoveInvalidNodes] Marking node for removal: {reason}");
                nodesToRemove.Add(node);
            }
        }
        
        foreach (var node in nodesToRemove)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoveInvalidNodes] Removing invalid root node");
            LinksTreeView.RootNodes.Remove(node);
        }
        
        System.Diagnostics.Debug.WriteLine($"[RemoveInvalidNodes] RootNodes.Count after cleanup: {LinksTreeView.RootNodes.Count}");
    }
    
    /// <summary>
    /// Updates the bookmark lookup categories list from all categories (including subcategories).
    /// </summary>
    private void UpdateBookmarkLookupCategories()
    {
        var bookmarkLookupCategories = new List<TreeViewNode>();
        
        // Recursively collect all bookmark lookup categories
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            CollectBookmarkLookupCategories(rootNode, bookmarkLookupCategories);
        }
        
        // Pass to link dialog
        _linkDialog?.SetBookmarkLookupCategories(bookmarkLookupCategories);
        
        System.Diagnostics.Debug.WriteLine($"[UpdateBookmarkLookupCategories] Found {bookmarkLookupCategories.Count} bookmark lookup categories");
    }
    
    /// <summary>
    /// Recursively collects bookmark lookup categories from a tree node and its children.
    /// </summary>
    private void CollectBookmarkLookupCategories(TreeViewNode node, List<TreeViewNode> bookmarkCategories)
    {
        if (node.Content is CategoryItem category)
        {
            // Add this category if it's a bookmark lookup category
            if (category.IsBookmarkCategory && category.IsBookmarkLookup)
            {
                bookmarkCategories.Add(node);
                System.Diagnostics.Debug.WriteLine($"[CollectBookmarkLookupCategories] Added category: {category.Name}");
            }
            
            // Recursively check subcategories
            foreach (var child in node.Children)
            {
                CollectBookmarkLookupCategories(child, bookmarkCategories);
            }
        }
    }

    /// <summary>
    /// Checks all folder links for changes across all root categories and auto-refreshes if enabled.
    /// </summary>
    private async Task CheckAllFoldersForChangesAsync()
    {
        // Collect all catalogs that need auto-refresh from ALL root categories
        var catalogsToRefresh = new List<(LinkItem link, TreeViewNode node)>();
        
        foreach (var category in LinksTreeView.RootNodes)
        {
            CollectAutoRefreshCatalogs(category, catalogsToRefresh);
        }

        System.Diagnostics.Debug.WriteLine($"[CheckAllFoldersForChangesAsync] Found {catalogsToRefresh.Count} catalogs to auto-refresh");

        if (catalogsToRefresh.Count == 0)
        {
            // No auto-refresh needed, just update change status for display
            foreach (var category in LinksTreeView.RootNodes)
            {
                UpdateChangeStatusRecursively(category);
            }
            return;
        }

        // Show progress bar
        AutoRefreshProgressBar.Visibility = Visibility.Visible;
        AutoRefreshProgressCount.Text = $"0 / {catalogsToRefresh.Count}";
        AutoRefreshProgressText.Text = "Starting auto-refresh...";

        int completed = 0;
        foreach (var (link, linkNode) in catalogsToRefresh)
        {
            try
            {
                AutoRefreshProgressText.Text = $"Refreshing: {link.Title}";
                AutoRefreshProgressCount.Text = $"{completed + 1} / {catalogsToRefresh.Count}";

                System.Diagnostics.Debug.WriteLine($"[CheckAllFoldersForChangesAsync] Refreshing '{link.Title}'");
                await RefreshCatalogSilentlyAsync(link, linkNode);
                completed++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CheckAllFoldersForChangesAsync] Error refreshing '{link.Title}': {ex.Message}");
                completed++;
            }
        }

        // Hide progress bar when done
        AutoRefreshProgressBar.Visibility = Visibility.Collapsed;
        System.Diagnostics.Debug.WriteLine($"[CheckAllFoldersForChangesAsync] Completed {completed} auto-refreshes");
    }

    /// <summary>
    /// Collects all catalogs that have AutoRefreshCatalog enabled and have changes.
    /// </summary>
    private void CollectAutoRefreshCatalogs(TreeViewNode node, List<(LinkItem link, TreeViewNode node)> catalogsToRefresh)
    {
        if (node.Content is LinkItem link)
        {
            // Only check directory links that are catalogued (not catalog entries themselves)
            if (link.IsDirectory && !link.IsCatalogEntry && link.AutoRefreshCatalog &&
                link.LastCatalogUpdate.HasValue && Directory.Exists(link.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(link.Url);
                    var currentFileCount = Directory.GetFiles(link.Url).Length;

                    // Check for changes: either LastWriteTime (with 2 second tolerance) or file count mismatch
                    // Add tolerance to avoid false positives from timestamp precision issues
                    bool hasChanged = dirInfo.LastWriteTime > link.LastCatalogUpdate.Value.AddSeconds(2) ||
                                      (link.CatalogFileCount > 0 && currentFileCount != link.CatalogFileCount);

                    if (hasChanged)
                    {
                        catalogsToRefresh.Add((link, node));
                    }
                }
                catch
                {
                    // Folder not accessible - skip
                }
            }
        }

        // Recursively check children
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem childLink)
            {
                // Only process directories (not file catalog entries)
                if (childLink.IsDirectory)
                {
                    CollectAutoRefreshCatalogs(child, catalogsToRefresh);
                }
            }
            else if (child.Content is CategoryItem)
            {
                CollectAutoRefreshCatalogs(child, catalogsToRefresh);
            }
        }
    }

    /// <summary>
    /// Updates change status for all folder links recursively (for non-auto-refresh folders).
    /// </summary>
    private void UpdateChangeStatusRecursively(TreeViewNode node)
    {
        if (node.Content is LinkItem link)
        {
            if (link.IsDirectory && !link.IsCatalogEntry && link.LastCatalogUpdate.HasValue)
            {
                try
                {
                    if (Directory.Exists(link.Url))
                    {
                        var dirInfo = new DirectoryInfo(link.Url);
                        var currentFileCount = Directory.GetFiles(link.Url).Length;

                        // Check for changes with tolerance to avoid false positives
                        bool hasChanged = dirInfo.LastWriteTime > link.LastCatalogUpdate.Value.AddSeconds(2) ||
                                          (link.CatalogFileCount > 0 && currentFileCount != link.CatalogFileCount);

                        if (hasChanged && !link.AutoRefreshCatalog)
                        {
                            link.RefreshChangeStatus();
                            _treeViewService!.RefreshLinkNode(node, link);
                        }
                    }
                }
                catch
                {
                    // Folder not accessible - ignore
                }
            }
        }

        // Recursively check only directory children
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem childLink && childLink.IsDirectory)
            {
                UpdateChangeStatusRecursively(child);
            }
            else if (child.Content is CategoryItem)
            {
                UpdateChangeStatusRecursively(child);
            }
        }
    }

    /// <summary>
    /// Silently refreshes a catalog in the background without user interaction.
    /// Does NOT navigate to or expand nodes during the process.
    /// </summary>
    private async Task RefreshCatalogSilentlyAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        try
        {
            // Delegate to CatalogService for the actual refresh
            // Pass silent=true to prevent navigation/expansion
            await _catalogService!.RefreshCatalogAsync(linkItem, linkNode, silent: true);

            // Save the changes
            var rootNode = GetRootCategoryNode(linkNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
        }
        catch
        {
            // Log but silently fail - don't interrupt startup
        }
    }

    private TreeViewNode GetRootCategoryNode(TreeViewNode node)
    {
        // Safety check: if node is null, can't proceed
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node), "Node cannot be null");
        }

        // First, check if this node itself is already a root category
        if (LinksTreeView.RootNodes.Contains(node))
        {
            // Verify it's actually a CategoryItem
            if (node.Content is CategoryItem)
            {
                return node;
            }
            else
            {
                // This is a root node but not a category - this is an error state
                var contentType = node.Content?.GetType().Name ?? "null";
                System.Diagnostics.Debug.WriteLine($"[GetRootCategoryNode] ERROR: Root node is not a CategoryItem: {contentType}");
                
                // Try to find ANY valid category root node
                foreach (var rootNode in LinksTreeView.RootNodes)
                {
                    if (rootNode.Content is CategoryItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GetRootCategoryNode] Using alternative root category");
                        return rootNode;
                    }
                }
                
                throw new InvalidOperationException($"Root node is not a CategoryItem: {contentType}. No valid category roots found.");
            }
        }

        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100; // Prevent infinite loops
        
        // Navigate up until we find a root node or hit max depth
        while (current?.Parent != null && safetyCounter < maxDepth)
        {
            current = current.Parent;
            safetyCounter++;
        }
        
        // Safety check: current should not be null
        if (current == null)
        {
            throw new InvalidOperationException($"Node traversal resulted in null. Cannot find root category.");
        }
        
        // If we hit max depth, something is wrong
        if (safetyCounter >= maxDepth)
        {
            throw new InvalidOperationException($"Node hierarchy too deep (>{maxDepth} levels). Possible circular reference.");
        }
        
        // At this point, current should be a root node
        // Verify it's actually a CategoryItem
        if (current.Content is CategoryItem)
        {
            return current;
        }
        
        // If the root is not a CategoryItem, search all root nodes to find the category that contains this node
        System.Diagnostics.Debug.WriteLine($"[GetRootCategoryNode] Root node is not CategoryItem, searching for containing category...");
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem && NodeContainsDescendant(rootNode, node))
            {
                System.Diagnostics.Debug.WriteLine($"[GetRootCategoryNode] Found containing category: {((CategoryItem)rootNode.Content).Name}");
                return rootNode;
            }
        }
        
        // Last resort: if we can't find a proper category, return the first valid category root
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem cat)
            {
                System.Diagnostics.Debug.WriteLine($"[GetRootCategoryNode] WARNING: Using fallback category: {cat.Name}");
                return rootNode;
            }
        }
        
        // If we somehow ended up with no valid category roots at all, throw a detailed error
        var nodeContentType = current.Content?.GetType().Name ?? "null";
        var rootNodeTypes = string.Join(", ", LinksTreeView.RootNodes.Select(n => n.Content?.GetType().Name ?? "null"));
        throw new InvalidOperationException($"Could not find root category node. Found: {nodeContentType}. Root nodes: [{rootNodeTypes}]");
    }

    private bool NodeContainsDescendant(TreeViewNode ancestor, TreeViewNode target)
    {
        if (ancestor == target)
            return true;
            
        foreach (var child in ancestor.Children)
        {
            if (NodeContainsDescendant(child, target))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Handles Enter key in URL text box to load the URL.
    /// </summary>
    private void UrlTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            LoadUrlFromTextBox();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles the refresh/go button click to load the URL.
    /// </summary>
    private void RefreshUrlButton_Click(object sender, RoutedEventArgs e)
    {
        LoadUrlFromTextBox();
    }

    /// <summary>
    /// Loads the URL from the text box into the Content tab's WebView2.
    /// </summary>
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
            // Add http:// if no protocol specified
            if (!urlText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !urlText.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !urlText.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                urlText = "https://" + urlText;
            }

            if (Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri))
            {
                IsUrlLoading = true; // Start loading animation
                
                // Load URL in the Content tab's WebView, not the main WebViewer
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
            IsUrlLoading = false; // Stop loading animation on error
            StatusText.Text = $"Error loading URL: {ex.Message}";
        }
    }

    /// <summary>
    /// Handles the File > Exit menu click.
    /// </summary>
    private async void MenuFile_Exit_Click(object sender, RoutedEventArgs e)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Exit Application",
            Content = "Are you sure you want to exit?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Clear any cached passwords before exiting
            _categoryService?.ClearPasswordCache();
            
            // Close the application
            Close();
        }
    }
}
