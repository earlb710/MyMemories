using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
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

            StatusText.Text = categories.Count > 0
                ? $"Loaded {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}"
                : "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading categories: {ex.Message}";
        }
    }

    #region Search Operations

    private class SearchResult
    {
        public string DisplayText { get; set; } = string.Empty;
        public TreeViewNode Node { get; set; } = null!;
        public string NodeType { get; set; } = string.Empty; // "Category" or "Link"
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void SearchComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
    }

    private void SearchComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var searchText = SearchComboBox.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            SearchComboBox.ItemsSource = null;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            _lastSearchText = string.Empty;
            StatusText.Text = "Ready";
            return;
        }

        // Check if this is a new search or cycling through existing results
        bool isNewSearch = searchText != _lastSearchText;

        if (isNewSearch)
        {
            // Perform new search
            _searchResults = SearchNodes(searchText);
            _currentSearchIndex = -1;
            _lastSearchText = searchText;

            if (_searchResults.Count == 0)
            {
                SearchComboBox.ItemsSource = null;
                StatusText.Text = $"No results found for '{searchText}'";
                return;
            }

            // Populate dropdown with results
            SearchComboBox.ItemsSource = _searchResults.Select(r => r.DisplayText).ToList();
        }

        // Navigate to next result (or first result if new search)
        if (_searchResults.Count > 0)
        {
            _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
            NavigateToSearchResult(_searchResults[_currentSearchIndex], false);
            
            StatusText.Text = $"Result {_currentSearchIndex + 1} of {_searchResults.Count} for '{searchText}'";
        }
    }

    private List<SearchResult> SearchNodes(string searchText)
    {
        var results = new List<SearchResult>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            SearchNodeRecursive(rootNode, searchLower, results);
        }

        return results;
    }

    private void SearchNodeRecursive(TreeViewNode node, string searchLower, List<SearchResult> results)
    {
        if (node.Content is CategoryItem category)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(node);
            
            // Search ONLY in category name and description (not the full path)
            if (category.Name.ToLowerInvariant().Contains(searchLower) ||
                (!string.IsNullOrEmpty(category.Description) && category.Description.ToLowerInvariant().Contains(searchLower)))
            {
                results.Add(new SearchResult
                {
                    DisplayText = $"📁 {categoryPath}",
                    Node = node,
                    NodeType = "Category"
                });
            }

            // Search in child nodes
            foreach (var child in node.Children)
            {
                SearchNodeRecursive(child, searchLower, results);
            }
        }
        else if (node.Content is LinkItem link)
        {
            var categoryPath = link.CategoryPath;
            
            // Search in link title, URL, and description (not category path)
            if (link.Title.ToLowerInvariant().Contains(searchLower) ||
                (!string.IsNullOrEmpty(link.Url) && link.Url.ToLowerInvariant().Contains(searchLower)) ||
                (!string.IsNullOrEmpty(link.Description) && link.Description.ToLowerInvariant().Contains(searchLower)))
            {
                // Use the link's icon instead of generic link icon
                var icon = link.GetIcon();
                var displayText = string.IsNullOrEmpty(categoryPath) 
                    ? $"{icon} {link.Title}" 
                    : $"{icon} {link.Title} [{categoryPath}]";

                results.Add(new SearchResult
                {
                    DisplayText = displayText,
                    Node = node,
                    NodeType = "Link"
                });
            }
        }
    }

    private void NavigateToSearchResult(SearchResult result, bool clearSearch = true)
    {
        // Expand all parent nodes
        ExpandParentNodes(result.Node);

        // Select the node
        LinksTreeView.SelectedNode = result.Node;

        if (clearSearch)
        {
            // Clear search box
            SearchComboBox.Text = string.Empty;
            SearchComboBox.ItemsSource = null;
            SearchComboBox.IsDropDownOpen = false;
            _searchResults.Clear();
            _currentSearchIndex = -1;
            _lastSearchText = string.Empty;
            StatusText.Text = $"Navigated to: {result.DisplayText}";
        }
    }

    private void ExpandParentNodes(TreeViewNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
    }

    #endregion

    #region File Operations

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            var hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.FileTypeFilter.Add("*");
            
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening file: {ex.Message}";
        }
    }

    private async Task LoadFileAsync(StorageFile file, string? description = null)
    {
        try
        {
            StatusText.Text = $"Loading {file.Name}...";

            HideAllViewers();

            var result = await _fileViewerService!.LoadFileAsync(file);
            
            // Show header with file info
            await _detailsViewService!.ShowFileHeaderAsync(file.Name, description, file, result.Bitmap);
            HeaderViewerScroll.Visibility = Visibility.Visible;
            
            ShowViewer(result.ViewerType);

            var properties = await file.GetBasicPropertiesAsync();
            var fileSize = FileViewerService.FormatFileSize(properties.Size);
            StatusText.Text = $"Loaded: {file.Name} ({fileSize})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading file: {ex.Message}";
            ShowWelcome();
        }
    }

    #endregion

    #region Category Operations

    private async void CreateCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _linkDialog!.ShowCategoryDialogAsync("Create New Category");

        if (result != null)
        {
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
                    Icon = result.Icon,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                }
            };

            _treeViewService!.InsertCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(categoryNode);

            StatusText.Text = $"Created category: {result.Name}";
        }
    }

    private async Task CreateSubCategoryAsync(TreeViewNode parentNode)
    {
        var parentCategoryPath = _treeViewService!.GetCategoryPath(parentNode);
        var result = await _linkDialog!.ShowCategoryDialogAsync($"Create Sub Category under '{parentCategoryPath}'");

        if (result != null)
        {
            var subCategoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = result.Name,
                    Description = result.Description,
                    Icon = result.Icon,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                }
            };

            _treeViewService!.InsertSubCategoryNode(parentNode, subCategoryNode);
            await _categoryService!.SaveCategoryAsync(GetRootCategoryNode(parentNode));

            var fullPath = _treeViewService.GetCategoryPath(subCategoryNode);
            StatusText.Text = $"Created sub category: {fullPath}";
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

    private List<CategoryNode> GetAllCategoriesFlat()
    {
        var allCategories = new List<CategoryNode>();
        
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem)
            {
                var categoriesWithSubs = _treeViewService!.GetCategoryWithSubcategories(rootNode);
                allCategories.AddRange(categoriesWithSubs);
            }
        }
        
        return allCategories;
    }

    private async Task EditCategoryAsync(CategoryItem category, TreeViewNode node)
    {
        string oldCategoryName = category.Name;

        var result = await _linkDialog!.ShowCategoryDialogAsync(
            "Edit Category",
            category.Name,
            category.Description,
            category.Icon);

        if (result != null)
        {
            // Only delete if it's a root category and name changed
            if (node.Parent == null && oldCategoryName != result.Name)
            {
                await _categoryService!.DeleteCategoryAsync(oldCategoryName);
            }

            var updatedCategory = new CategoryItem
            {
                Name = result.Name,
                Description = result.Description,
                Icon = result.Icon,
                CreatedDate = category.CreatedDate, // Keep original created date
                ModifiedDate = DateTime.Now // Update modified date
            };

            var newNode = _treeViewService!.RefreshCategoryNode(node, updatedCategory);

            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = newNode;
            }

            // Save the root category node
            var rootNode = GetRootCategoryNode(newNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
            StatusText.Text = $"Updated category: {result.Name}";

            if (LinksTreeView.SelectedNode == newNode)
            {
                _detailsViewService!.ShowCategoryDetails(updatedCategory, newNode);
                _detailsViewService.ShowCategoryHeader(_treeViewService!.GetCategoryPath(newNode), updatedCategory.Description, updatedCategory.Icon);
                HeaderViewerScroll.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task DeleteCategoryAsync(CategoryItem category, TreeViewNode node)
    {
        int totalLinks = CountAllLinks(node);
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Category",
            Content = $"Are you sure you want to delete '{category.Icon} {category.Name}' and all its {totalLinks} link(s) and subcategories?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (node.Parent == null)
            {
                // Root category - delete file
                await _categoryService!.DeleteCategoryAsync(category.Name);
                LinksTreeView.RootNodes.Remove(node);
            }
            else
            {
                // Sub category - remove from parent and save
                node.Parent.Children.Remove(node);
                var rootNode = GetRootCategoryNode(node.Parent);
                await _categoryService!.SaveCategoryAsync(rootNode);
            }

            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = null;
            }

            ShowWelcome();
            StatusText.Text = $"Deleted category: {category.Name}";
        }
    }

    private int CountAllLinks(TreeViewNode node)
    {
        int count = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem)
            {
                count++;
            }
            else if (child.Content is CategoryItem)
            {
                count += CountAllLinks(child);
            }
        }
        return count;
    }

    #endregion

    #region Link Operations

    private async void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var categories = LinksTreeView.RootNodes
            .Where(n => n.Content is CategoryItem)
            .Select(n => new CategoryNode
            {
                Name = ((CategoryItem)n.Content).Name,
                Node = n
            })
            .ToList();

        if (categories.Count == 0)
        {
            StatusText.Text = "Please create a category first";
            return;
        }

        var selectedCategory = _treeViewService!.GetParentCategoryNode(LinksTreeView.SelectedNode) ?? _lastUsedCategory;
        var selectedCategoryNode = selectedCategory != null
            ? new CategoryNode { Name = ((CategoryItem)selectedCategory.Content).Name, Node = selectedCategory }
            : null;

        var result = await _linkDialog!.ShowAddAsync(categories, selectedCategoryNode);

        if (result?.CategoryNode != null)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(result.CategoryNode);
            
            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = result.Title,
                    Url = result.Url,
                    Description = result.Description,
                    IsDirectory = result.IsDirectory,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    FolderType = result.FolderType,
                    FileFilters = result.FileFilters
                }
            };

            result.CategoryNode.Children.Add(linkNode);
            result.CategoryNode.IsExpanded = true;
            _lastUsedCategory = result.CategoryNode;

            var rootNode = GetRootCategoryNode(result.CategoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var categoryItem = result.CategoryNode.Content as CategoryItem;
            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";
        }
    }

    private async Task EditLinkAsync(LinkItem link, TreeViewNode node)
    {
        var editResult = await _linkDialog!.ShowEditAsync(link);
        
        if (editResult != null)
        {
            link.Title = editResult.Title;
            link.Url = editResult.Url;
            link.Description = editResult.Description;
            link.IsDirectory = editResult.IsDirectory;
            link.CategoryPath = _treeViewService!.GetCategoryPath(node.Parent);
            link.ModifiedDate = DateTime.Now; // Update modified date
            link.FolderType = editResult.FolderType;
            link.FileFilters = editResult.FileFilters;

            // Refresh the link node and get the new node reference
            var newNode = _treeViewService!.RefreshLinkNode(node, link);

            // Update context menu node if it was the edited node
            if (_contextMenuNode == node)
            {
                _contextMenuNode = newNode;
            }

            var parentCategory = newNode.Parent;
            if (parentCategory != null)
            {
                var rootNode = GetRootCategoryNode(parentCategory);
                await _categoryService!.SaveCategoryAsync(rootNode);
            }

            StatusText.Text = $"Updated link: {editResult.Title}";

            if (LinksTreeView.SelectedNode == newNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    newNode,
                    async () => await CreateCatalogAsync(link, newNode),
                    async () => await RefreshCatalogAsync(link, newNode)
                );
            }
        }
    }

    private async Task MoveLinkAsync(LinkItem link, TreeViewNode node)
    {
        if (node.Parent == null || node.Parent.Content is not CategoryItem)
        {
            StatusText.Text = "Cannot move link: Invalid parent category";
            return;
        }

        var currentCategoryNode = node.Parent;
        var allCategories = GetAllCategoriesFlat();
        
        var result = await _linkDialog!.ShowMoveLinkAsync(allCategories, currentCategoryNode, link.Title);

        if (result?.TargetCategoryNode != null)
        {
            var targetCategoryNode = result.TargetCategoryNode;
            var targetCategoryPath = _treeViewService!.GetCategoryPath(targetCategoryNode);

            // Update the link's category path
            link.CategoryPath = targetCategoryPath;

            // Remove from current category
            currentCategoryNode.Children.Remove(node);

            // Add to target category
            targetCategoryNode.Children.Add(node);
            targetCategoryNode.IsExpanded = true;

            // Save both affected root categories
            var sourceRootNode = GetRootCategoryNode(currentCategoryNode);
            var targetRootNode = GetRootCategoryNode(targetCategoryNode);

            await _categoryService!.SaveCategoryAsync(sourceRootNode);
            
            // Only save target if it's different from source
            if (sourceRootNode != targetRootNode)
            {
                await _categoryService!.SaveCategoryAsync(targetRootNode);
            }

            StatusText.Text = $"Moved link '{link.Title}' to '{targetCategoryPath}'";

            // If the moved link was selected, update the details view
            if (LinksTreeView.SelectedNode == node)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    node,
                    async () => await CreateCatalogAsync(link, node),
                    async () => await RefreshCatalogAsync(link, node)
                );
            }
        }
    }

    private async Task DeleteLinkAsync(LinkItem link, TreeViewNode node)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Remove Link",
            Content = $"Remove link '{link.Title}'?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary && node.Parent != null)
        {
            var parentCategory = node.Parent;
            parentCategory.Children.Remove(node);

            var rootNode = GetRootCategoryNode(parentCategory);
            await _categoryService!.SaveCategoryAsync(rootNode);

            if (LinksTreeView.SelectedNode == node)
            {
                ShowWelcome();
            }

            StatusText.Text = $"Removed link: {link.Title}";
        }
    }

    #endregion

    #region TreeView Events

    private async void LinksTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not TreeViewNode node)
            return;

        if (node.Content is CategoryItem category)
        {
            HideAllViewers();
            _detailsViewService!.ShowCategoryDetails(category, node);
            DetailsViewerScroll.Visibility = Visibility.Visible;
            
            var categoryPath = _treeViewService!.GetCategoryPath(node);
            _detailsViewService.ShowCategoryHeader(categoryPath, category.Description, category.Icon);
            HeaderViewerScroll.Visibility = Visibility.Visible;
            
            StatusText.Text = $"Viewing: {categoryPath} ({node.Children.Count} item(s))";
        }
        else if (node.Content is LinkItem linkItem)
        {
            await HandleLinkSelectionAsync(linkItem);
        }
    }

    private async void LinksTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Get the tapped element
        if (e.OriginalSource is not FrameworkElement element)
            return;

        // Find the TreeViewItem
        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        // Handle double-tap for both categories and links
        if (node.Content is CategoryItem category)
        {
            // Open category edit dialog
            await EditCategoryAsync(category, node);
            e.Handled = true;
        }
        else if (node.Content is LinkItem linkItem)
        {
            // Open link externally
            await OpenLinkAsync(linkItem);
            e.Handled = true;
        }
    }

    private async Task OpenLinkAsync(LinkItem linkItem)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            StatusText.Text = "Link has no URL to open";
            return;
        }

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
            {
                // Open directory in File Explorer
                await Launcher.LaunchFolderPathAsync(linkItem.Url);
                StatusText.Text = $"Opened directory: {linkItem.Title}";
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    // Open file with default application
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await Launcher.LaunchFileAsync(file);
                    StatusText.Text = $"Opened file: {linkItem.Title}";
                }
                else
                {
                    // Open URL in default browser
                    await Launcher.LaunchUriAsync(uri);
                    StatusText.Text = $"Opened URL: {linkItem.Title}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening link: {ex.Message}";
        }
    }

    private async Task HandleLinkSelectionAsync(LinkItem linkItem)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            HideAllViewers();
            
            // Find the TreeViewNode for this link
            var linkNode = FindLinkNode(linkItem);
            
            StatusText.Text = $"DEBUG: Empty URL, linkNode is {(linkNode == null ? "NULL" : "FOUND")}";
            
            if (linkNode != null)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    linkItem,
                    linkNode,
                    async () => await CreateCatalogAsync(linkItem, linkNode),
                    async () => await RefreshCatalogAsync(linkItem, linkNode)
                );
            }
            else
            {
                // No node found - show details without catalog buttons
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
            }
            
            DetailsViewerScroll.Visibility = Visibility.Visible;
            _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
            HeaderViewerScroll.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
            {
                HideAllViewers();
                
                // Find the TreeViewNode for this link
                var linkNode = FindLinkNode(linkItem);
                
                StatusText.Text = $"DEBUG: Directory link, linkNode is {(linkNode == null ? "NULL" : "FOUND")}";
                
                if (linkNode != null)
                {
                    await _detailsViewService!.ShowLinkDetailsAsync(
                        linkItem, 
                        linkNode,
                        async () => await CreateCatalogAsync(linkItem, linkNode),
                        async () => await RefreshCatalogAsync(linkItem, linkNode)
                    );
                }
                else
                {
                    // No node found - show details without catalog buttons
                    await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
                }
                
                await _detailsViewService.AddOpenInExplorerButtonAsync(linkItem.Url);
                DetailsViewerScroll.Visibility = Visibility.Visible;
                _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
                HeaderViewerScroll.Visibility = Visibility.Visible;
                StatusText.Text = $"Viewing directory: {linkItem.Title}";
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await LoadFileAsync(file, linkItem.Description);
                }
                else
                {
                    HideAllViewers();
                    await _fileViewerService!.LoadUrlAsync(uri);
                    WebViewer.Visibility = Visibility.Visible;
                    _detailsViewService!.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
                    HeaderViewerScroll.Visibility = Visibility.Visible;
                    StatusText.Text = $"Loaded: {uri}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            HideAllViewers();
            
            // Find the TreeViewNode for this link
            var linkNode = FindLinkNode(linkItem);
            
            if (linkNode != null)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    linkItem,
                    linkNode,
                    async () => await CreateCatalogAsync(linkItem, linkNode),
                    async () => await RefreshCatalogAsync(linkItem, linkNode)
                );
            }
            else
            {
                // No node found - show details without catalog buttons
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
            }
            
            DetailsViewerScroll.Visibility = Visibility.Visible;
            _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
            HeaderViewerScroll.Visibility = Visibility.Visible;
        }
    }

    private TreeViewNode? FindLinkNode(LinkItem linkItem)
    {
        // Use the currently selected node if it matches
        if (LinksTreeView.SelectedNode?.Content is LinkItem selectedLink && 
            selectedLink.Title == linkItem.Title && 
            selectedLink.Url == linkItem.Url)
        {
            return LinksTreeView.SelectedNode;
        }
        
        // Otherwise search the tree
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            var found = FindLinkNodeRecursive(rootNode, linkItem);
            if (found != null) return found;
        }
        return null;
    }

    private TreeViewNode? FindLinkNodeRecursive(TreeViewNode node, LinkItem targetLink)
    {
        if (node.Content is LinkItem link && 
            link.Title == targetLink.Title && 
            link.Url == targetLink.Url &&
            link.CategoryPath == targetLink.CategoryPath)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindLinkNodeRecursive(child, targetLink);
            if (found != null) return found;
        }

        return null;
    }

    private async Task CreateCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        Debug.WriteLine($"[MainWindow] CreateCatalogAsync called for: {linkItem.Title}, Path: {linkItem.Url}");
        try
        {
            StatusText.Text = "Creating catalog...";
            Debug.WriteLine($"[MainWindow] Status updated to 'Creating catalog...'");

            // Create catalog entries
            Debug.WriteLine($"[MainWindow] Calling CreateCatalogEntriesAsync...");
            var catalogEntries = await _categoryService!.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);
            Debug.WriteLine($"[MainWindow] Created {catalogEntries.Count} catalog entries");

            // Update the folder link's LastCatalogUpdate timestamp
            linkItem.LastCatalogUpdate = DateTime.Now;
            Debug.WriteLine($"[MainWindow] Updated LastCatalogUpdate timestamp");

            // Add catalog entries to the tree
            Debug.WriteLine($"[MainWindow] Adding {catalogEntries.Count} entries to tree...");
            foreach (var entry in catalogEntries)
            {
                var entryNode = new TreeViewNode
                {
                    Content = entry
                };
                linkNode.Children.Add(entryNode);
                Debug.WriteLine($"[MainWindow] Added catalog entry: {entry.Title}");
            }

            // Refresh the link node to update the display (remove asterisk if it was there)
            Debug.WriteLine($"[MainWindow] Refreshing link node...");
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);
            Debug.WriteLine($"[MainWindow] Link node refreshed");

            // Save the changes
            Debug.WriteLine($"[MainWindow] Saving category...");
            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);
            Debug.WriteLine($"[MainWindow] Category saved");

            // Refresh the view
            refreshedNode.IsExpanded = true;
            Debug.WriteLine($"[MainWindow] Expanding node and refreshing view...");
            await _detailsViewService!.ShowLinkDetailsAsync(
                linkItem,
                refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode)
            );
            Debug.WriteLine($"[MainWindow] View refreshed");

            StatusText.Text = $"Created catalog with {catalogEntries.Count} entries";
            Debug.WriteLine($"[MainWindow] CreateCatalogAsync completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] ERROR in CreateCatalogAsync: {ex.Message}");
            Debug.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
            StatusText.Text = $"Error creating catalog: {ex.Message}";
        }
    }

    private async Task RefreshCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        Debug.WriteLine($"[MainWindow] RefreshCatalogAsync called for: {linkItem.Title}");
        try
        {
            StatusText.Text = "Refreshing catalog...";

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

            // Refresh the link node to update the display (remove asterisk)
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            // Save the changes
            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);

            // Refresh the view
            refreshedNode.IsExpanded = true;
            await _detailsViewService!.ShowLinkDetailsAsync(
                linkItem,
                refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode)
            );

            StatusText.Text = $"Refreshed catalog with {catalogEntries.Count} entries";
            Debug.WriteLine($"[MainWindow] RefreshCatalogAsync completed successfully with {catalogEntries.Count} entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] ERROR in RefreshCatalogAsync: {ex.Message}");
            StatusText.Text = $"Error refreshing catalog: {ex.Message}";
        }
    }

    #endregion

    #region Context Menu

    private void LinksTreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        _contextMenuNode = node;

        var menu = node.Content switch
        {
            CategoryItem => LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout,
            LinkItem => LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout,
            _ => null
        };

        menu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
        e.Handled = true;
    }

    private async void CategoryMenu_AddLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode == null) return;

        // Get the selected category and all its subcategories
        var categoriesWithSubs = _treeViewService!.GetCategoryWithSubcategories(_contextMenuNode);

        var selectedCategoryNode = new CategoryNode
        {
            Name = _treeViewService.GetCategoryPath(_contextMenuNode),
            Node = _contextMenuNode
        };

        var result = await _linkDialog!.ShowAddAsync(categoriesWithSubs, selectedCategoryNode);

        if (result?.CategoryNode != null)
        {
            var categoryPath = _treeViewService.GetCategoryPath(result.CategoryNode);

            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = result.Title,
                    Url = result.Url,
                    Description = result.Description,
                    IsDirectory = result.IsDirectory,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    FolderType = result.FolderType,
                    FileFilters = result.FileFilters
                }
            };

            result.CategoryNode.Children.Add(linkNode);
            result.CategoryNode.IsExpanded = true;

            var rootNode = GetRootCategoryNode(result.CategoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);
            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";

            if (LinksTreeView.SelectedNode == result.CategoryNode)
            {
                _detailsViewService!.ShowCategoryDetails((CategoryItem)result.CategoryNode.Content, result.CategoryNode);
            }
        }
    }

    private async void CategoryMenu_AddSubCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem)
        {
            await CreateSubCategoryAsync(_contextMenuNode);
        }
    }

    private async void CategoryMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await EditCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void CategoryMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await DeleteCategoryAsync(category, _contextMenuNode);
        }
    }

    private async void LinkMenu_AddSubCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Parent != null)
        {
            var parentCategoryNode = _treeViewService!.GetParentCategoryNode(_contextMenuNode);
            if (parentCategoryNode != null)
            {
                await CreateSubCategoryAsync(parentCategoryNode);
            }
        }
    }

    private async void LinkMenu_Move_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            await MoveLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            // Prevent editing catalog entries
            if (link.IsCatalogEntry)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Edit Catalog Entry",
                    Content = "Catalog entries are read-only and cannot be edited. Use 'Refresh Catalog' to update them.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await EditLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            // Prevent removing catalog entries individually
            if (link.IsCatalogEntry)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Cannot Remove Catalog Entry",
                    Content = "Catalog entries cannot be removed individually. Use 'Refresh Catalog' to update them.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await DeleteLinkAsync(link, _contextMenuNode);
        }
    }

    #endregion

    #region View Management

    private void HideAllViewers()
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        WebViewer.Visibility = Visibility.Collapsed;
        TextViewerScroll.Visibility = Visibility.Collapsed;
        DetailsViewerScroll.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Collapsed;
        HeaderViewerScroll.Visibility = Visibility.Collapsed;
    }

    private void ShowWelcome()
    {
        HideAllViewers();
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private void ShowViewer(FileViewerType viewerType)
    {
        switch (viewerType)
        {
            case FileViewerType.Image:
                ImageViewer.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Web:
                WebViewer.Visibility = Visibility.Visible;
                break;
            case FileViewerType.Text:
                TextViewerScroll.Visibility = Visibility.Visible;
                break;
        }
    }

    #endregion

    #region Helper Methods

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);
        return parent switch
        {
            null => null,
            T typedParent => typedParent,
            _ => FindParent<T>(parent)
        };
    }

    #endregion
}
