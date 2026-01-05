using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinRT.Interop;

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

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - File Viewer";

        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemories",
            "Categories"
        );

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Initialize WebView2
            await WebViewer.EnsureCoreWebView2Async();

            // Initialize services
            _categoryService = new CategoryService(_dataFolder);
            _fileViewerService = new FileViewerService(ImageViewer, WebViewer, TextViewer);
            _detailsViewService = new DetailsViewService(DetailsPanel);
            _detailsViewService.SetHeaderPanel(HeaderPanel);
            _treeViewService = new TreeViewService(LinksTreeView);
            _linkDialog = new LinkDetailsDialog(this, Content.XamlRoot);

            // Load categories
            await LoadAllCategoriesAsync();

            StatusText.Text = "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Initialization error: {ex.Message}";
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
    /// </summary>
    private async void CheckFolderChangesRecursively(TreeViewNode node)
    {
        if (node.Content is LinkItem link)
        {
            // Check if this is a folder link with a catalog that might have changed
            if (link.IsDirectory && !link.IsCatalogEntry && 
                link.LastCatalogUpdate.HasValue && Directory.Exists(link.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(link.Url);
                    if (dirInfo.LastWriteTime > link.LastCatalogUpdate.Value)
                    {
                        // Folder has changed - if auto-refresh is enabled, refresh it
                        if (link.AutoRefreshCatalog)
                        {
                            await RefreshCatalogSilentlyAsync(link, node);
                        }
                        else
                        {
                            // Just update the display to show the change indicator
                            var tempContent = node.Content;
                            node.Content = null;
                            node.Content = tempContent;
                        }
                    }
                }
                catch
                {
                    // Folder not accessible - ignore
                }
            }
        }

        // Recursively check all children
        foreach (var child in node.Children)
        {
            CheckFolderChangesRecursively(child);
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
                var entryNode = new TreeViewNode
                {
                    Content = entry
                };
                linkNode.Children.Add(entryNode);
            }

            // Update the catalog file count
            _categoryService.UpdateCatalogFileCount(linkNode);

            // Refresh the link node to update the display
            _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            // Save the changes
            var rootNode = GetRootCategoryNode(linkNode);
            await _categoryService.SaveCategoryAsync(rootNode);
        }
        catch
        {
            // Silently fail - don't interrupt startup
        }
    }

    private TreeViewNode GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        while (current.Parent != null && current.Parent.Content is CategoryItem)
        {
            current = current.Parent;
        }
        return current;
    }
}
