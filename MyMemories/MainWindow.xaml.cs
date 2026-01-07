using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities; // Add this line
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Add this line
using System.Threading.Tasks;
using WinRT.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI;

namespace MyMemories;

/// <summary>
/// Main window for the MyMemories file viewer application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly string _dataFolder;
    private LinkDetailsDialog? _linkDialog;
    private TreeViewNode? _contextMenuNode;
    private TreeViewNode? _lastUsedCategory;
    private List<SearchResult> _searchResults = new();
    private int _currentSearchIndex = -1;
    private string _lastSearchText = string.Empty;

    // Services
    private CategoryService? _categoryService;
    private FileViewerService? _fileViewerService;
    private DetailsViewService? _detailsViewService;
    private TreeViewService? _treeViewService;
    private ConfigurationService? _configService;

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
    /// Sets the window icon to the book emoji icon (📚).
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
            // Initialize WebView2
            await WebViewer.EnsureCoreWebView2Async();

            // Initialize configuration service FIRST
            _configService = new ConfigurationService();
            await _configService.LoadConfigurationAsync();

            // Validate configuration directories BEFORE initializing services
            var validationPassed = await ValidateConfigurationDirectoriesAsync();
            if (!validationPassed)
            {
                StatusText.Text = "⚠️ Warning: Configuration validation failed";
                // Continue anyway - user chose to proceed
            }

            // Initialize CategoryService WITH ConfigurationService
            _categoryService = new CategoryService(_configService.WorkingDirectory, _configService);
            
            // Initialize other services
            _fileViewerService = new FileViewerService(ImageViewer, WebViewer, TextViewer);
            _detailsViewService = new DetailsViewService(DetailsPanel);
            _detailsViewService.SetHeaderPanel(HeaderPanel);
            _treeViewService = new TreeViewService(LinksTreeView, this);
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot, _configService);

            // Check if any categories use global password and prompt BEFORE loading
            await PromptForGlobalPasswordIfNeededAsync();

            // NOW load categories (password is cached if needed)
            await LoadAllCategoriesAsync();

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
            var categories = await _categoryService!.LoadAllCategoriesAsync();

            foreach (var category in categories)
            {
                _treeViewService!.InsertCategoryNode(category);
            }
            
            // Check for folder changes and auto-refresh if enabled
            foreach (var category in LinksTreeView.RootNodes)
            {
                CheckFolderChangesRecursively(category);
            }

            StatusText.Text = categories.Count > 0
                ? $"Loaded {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}"
                : "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading categories: {ex.Message}";
        }
    }

    /// <summary>
    /// Recursively checks all folder links for changes and auto-refreshes if enabled.
    /// Only checks directory links, skips individual file catalog entries.
    /// </summary>
    private async void CheckFolderChangesRecursively(TreeViewNode node)
    {
        if (node.Content is LinkItem link)
        {
            // Only check directory links, skip catalog entry files
            if (link.IsDirectory && !link.IsCatalogEntry && 
                link.LastCatalogUpdate.HasValue && Directory.Exists(link.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(link.Url);
                    var currentFileCount = Directory.GetFiles(link.Url).Length;
                    
                    // Check for changes: either LastWriteTime or file count mismatch
                    bool hasChanged = dirInfo.LastWriteTime > link.LastCatalogUpdate.Value ||
                                      currentFileCount != link.CatalogFileCount;
                    
                    if (hasChanged)
                    {
                        // Folder has changed - if auto-refresh is enabled, refresh it
                        if (link.AutoRefreshCatalog)
                        {
                            await RefreshCatalogSilentlyAsync(link, node);
                        }
                        else
                        {
                            // Notify the UI that the change status needs updating
                            link.RefreshChangeStatus();
                            
                            // Force TreeView to refresh the node visually by recreating it
                            var refreshedNode = _treeViewService!.RefreshLinkNode(node, link);
                            
                            // Continue checking only directory children (skip file entries)
                            foreach (var child in refreshedNode.Children)
                            {
                                if (child.Content is LinkItem childLink && childLink.IsDirectory)
                                {
                                    CheckFolderChangesRecursively(child);
                                }
                            }
                            return; // Exit early since we already processed children
                        }
                    }
                }
                catch
                {
                    // Folder not accessible - ignore
                }
            }
        }

        // Recursively check only directory children (skip file catalog entries)
        foreach (var child in node.Children)
        {
            // Skip individual file catalog entries - only recurse into directories
            if (child.Content is LinkItem childLink)
            {
                // Only process if it's a directory (not a catalog file entry)
                if (childLink.IsDirectory)
                {
                    CheckFolderChangesRecursively(child);
                }
                // Skip: individual files in catalog don't need change checking
            }
            else if (child.Content is CategoryItem)
            {
                // Always recurse into category nodes
                CheckFolderChangesRecursively(child);
            }
        }
    }

    /// <summary>
    /// Silently refreshes a catalog in the background without user interaction.
    /// </summary>
    private async Task RefreshCatalogSilentlyAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        try
        {
            // Remove existing catalog entries
            _categoryService!.RemoveCatalogEntries(linkNode);

            // Create new catalog entries
            var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);

            // Update the folder link's LastCatalogUpdate timestamp
            linkItem.LastCatalogUpdate = DateTime.Now;

            // Add new catalog entries to the tree
            foreach (var entry in catalogEntries)
            {
                var entryNode = new TreeViewNode { Content = entry };
                
                // If it's a subdirectory, recursively populate its contents
                if (entry.IsDirectory)
                {
                    await PopulateSubdirectoryAsync(entryNode, entry, linkItem.CategoryPath);
                }
                
                linkNode.Children.Add(entryNode);
            }

            // Update the catalog file count
            _categoryService.UpdateCatalogFileCount(linkNode);

            // Refresh the link node to update the display and get the NEW node reference
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            // Save the changes using the NEW refreshed node (not the old linkNode)
            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);
        }
        catch
        {
            // Log but silently fail - don't interrupt startup
        }
    }

    private TreeViewNode GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100; // Prevent infinite loops
        
        // Navigate up until we find a root category node (one without a parent)
        while (current.Parent != null && safetyCounter < maxDepth)
        {
            current = current.Parent;
            safetyCounter++;
        }
        
        // If we still have a parent after max depth, something is wrong
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
        
        // If the root is a LinkItem, search all root nodes to find its category
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem && NodeContainsDescendant(rootNode, node))
            {
                return rootNode;
            }
        }
        
        // If we somehow ended up with a non-category root, throw an error
        var contentType = current.Content?.GetType().Name ?? "null";
        throw new InvalidOperationException($"Could not find root category node. Found: {contentType}");
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
}
