using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

/// <summary>
/// Main window for the MyMemories file viewer application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private string? _currentFilePath;
    private TreeViewNode? _lastUsedCategory;
    private readonly string _dataFolder;
    private LinkDetailsDialog? _linkDialog;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "My Memories - File Viewer";
        
        // Set up data folder path
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyMemories",
            "Categories"
        );
        
        // Ensure data folder exists
        Directory.CreateDirectory(_dataFolder);
        
        // Load saved data and initialize WebView2 asynchronously
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await WebViewer.EnsureCoreWebView2Async();
            await LoadAllCategories();
            
            // Initialize link dialog
            _linkDialog = new LinkDetailsDialog(this, this.Content.XamlRoot);
            
            StatusText.Text = "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Initialization warning: {ex.Message}";
        }
    }

    private async Task InitializeWebView()
    {
        try
        {
            await WebViewer.EnsureCoreWebView2Async();
            StatusText.Text = "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"WebView2 initialization warning: {ex.Message}";
        }
    }

    private async Task LoadAllCategories()
    {
        try
        {
            if (!Directory.Exists(_dataFolder))
            {
                return;
            }

            var jsonFiles = Directory.GetFiles(_dataFolder, "*.json");
            
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(jsonFile);
                    var categoryData = JsonSerializer.Deserialize<CategoryData>(json);
                    
                    if (categoryData != null)
                    {
                        // Create category node
                        var categoryNode = new TreeViewNode
                        {
                            Content = new CategoryItem
                            {
                                Name = categoryData.Name,
                                Description = categoryData.Description,
                                Icon = categoryData.Icon
                            }
                        };

                        // Add links to category
                        foreach (var linkData in categoryData.Links)
                        {
                            var linkNode = new TreeViewNode
                            {
                                Content = new LinkItem
                                {
                                    Title = linkData.Title,
                                    Url = linkData.Url,
                                    Description = linkData.Description,
                                    IsDirectory = linkData.IsDirectory
                                }
                            };
                            categoryNode.Children.Add(linkNode);
                        }

                        // Add to tree view
                        InsertCategoryNode(categoryNode);
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error loading {Path.GetFileName(jsonFile)}: {ex.Message}";
                }
            }

            if (jsonFiles.Length > 0)
            {
                StatusText.Text = $"Loaded {jsonFiles.Length} categor{(jsonFiles.Length == 1 ? "y" : "ies")}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading categories: {ex.Message}";
        }
    }

    private async Task SaveCategory(TreeViewNode categoryNode)
    {
        try
        {
            if (categoryNode.Content is not CategoryItem category)
            {
                return;
            }

            // Create category data object
            var categoryData = new CategoryData
            {
                Name = category.Name,
                Description = category.Description,
                Icon = category.Icon,
                Links = new List<LinkData>()
            };

            // Add all links
            foreach (var childNode in categoryNode.Children)
            {
                if (childNode.Content is LinkItem link)
                {
                    categoryData.Links.Add(new LinkData
                    {
                        Title = link.Title,
                        Url = link.Url,
                        Description = link.Description,
                        IsDirectory = link.IsDirectory
                    });
                }
            }

            // Serialize to JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(categoryData, options);

            // Save to file (use sanitized category name as filename)
            var fileName = SanitizeFileName(category.Name) + ".json";
            var filePath = Path.Combine(_dataFolder, fileName);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving category: {ex.Message}";
        }
    }

    private async Task SaveAllCategories()
    {
        try
        {
            int savedCount = 0;
            foreach (var rootNode in LinksTreeView.RootNodes)
            {
                if (rootNode.Content is CategoryItem)
                {
                    await SaveCategory(rootNode);
                    savedCount++;
                }
            }
            StatusText.Text = $"Saved {savedCount} categor{(savedCount == 1 ? "y" : "ies")}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving categories: {ex.Message}";
        }
    }

    private async Task DeleteCategoryFile(string categoryName)
    {
        try
        {
            var fileName = SanitizeFileName(categoryName) + ".json";
            var filePath = Path.Combine(_dataFolder, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error deleting category file: {ex.Message}";
        }
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create a file picker
            var openPicker = new FileOpenPicker();
            
            // Initialize the file picker with the window handle
            var hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hWnd);

            // Set file type filters
            openPicker.FileTypeFilter.Add("*"); // All files
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".gif");
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".html");
            openPicker.FileTypeFilter.Add(".htm");
            openPicker.FileTypeFilter.Add(".pdf");
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add(".xml");
            openPicker.FileTypeFilter.Add(".json");
            openPicker.FileTypeFilter.Add(".md");
            openPicker.FileTypeFilter.Add(".log");

            // Pick a file
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFile(file);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error opening file: {ex.Message}";
        }
    }

    private async Task LoadFile(StorageFile file)
    {
        try
        {
            _currentFilePath = file.Path;
            CurrentFileText.Text = file.Name;
            StatusText.Text = $"Loading {file.Name}...";

            // Hide all viewers
            HideAllViewers();

            string extension = file.FileType.ToLowerInvariant();

            // Determine file type and display accordingly
            if (IsImageFile(extension))
            {
                await LoadImage(file);
            }
            else if (extension == ".html" || extension == ".htm")
            {
                await LoadHtml(file);
            }
            else if (extension == ".pdf")
            {
                await LoadPdf(file);
            }
            else if (IsTextFile(extension))
            {
                await LoadText(file);
            }
            else
            {
                // Try to load as text for unknown types
                await LoadText(file);
            }

            StatusText.Text = $"Loaded: {file.Name} ({FormatFileSize(await GetFileSize(file))})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading file: {ex.Message}";
            ShowWelcome();
        }
    }

    private void HideAllViewers()
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        WebViewer.Visibility = Visibility.Collapsed;
        TextViewerScroll.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowWelcome()
    {
        HideAllViewers();
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private bool IsImageFile(string extension)
    {
        return extension == ".jpg" || extension == ".jpeg" || 
               extension == ".png" || extension == ".gif" || 
               extension == ".bmp" || extension == ".ico";
    }

    private bool IsTextFile(string extension)
    {
        return extension == ".txt" || extension == ".xml" || 
               extension == ".json" || extension == ".md" || 
               extension == ".log" || extension == ".cs" || 
               extension == ".xaml" || extension == ".config" ||
               extension == ".ini" || extension == ".yaml" ||
               extension == ".yml" || extension == ".csv";
    }

    private async Task LoadImage(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            
            ImageViewer.Source = bitmap;
            ImageViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading image: {ex.Message}";
            throw;
        }
    }

    private async Task LoadHtml(StorageFile file)
    {
        try
        {
            // Ensure WebView2 is initialized
            if (WebViewer.CoreWebView2 == null)
            {
                await WebViewer.EnsureCoreWebView2Async();
            }

            // Load HTML file
            WebViewer.Source = new Uri(file.Path);
            WebViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading HTML: {ex.Message}";
            // Fallback to text view
            await LoadText(file);
        }
    }

    private async Task LoadPdf(StorageFile file)
    {
        try
        {
            // Ensure WebView2 is initialized
            if (WebViewer.CoreWebView2 == null)
            {
                await WebViewer.EnsureCoreWebView2Async();
            }

            // Load PDF file - WebView2 can display PDFs natively
            WebViewer.Source = new Uri(file.Path);
            WebViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading PDF: {ex.Message}";
            throw;
        }
    }

    private async Task LoadText(StorageFile file)
    {
        try
        {
            string content = await FileIO.ReadTextAsync(file);
            TextViewer.Text = content;
            TextViewerScroll.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading text file: {ex.Message}";
            throw;
        }
    }

    private async Task<ulong> GetFileSize(StorageFile file)
    {
        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            return properties.Size;
        }
        catch
        {
            return 0;
        }
    }

    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async void CreateCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_linkDialog == null)
        {
            StatusText.Text = "Dialog not initialized";
            return;
        }

        var result = await _linkDialog.ShowCategoryDialogAsync("Create New Category");

        if (result != null)
        {
            // Create a new category node
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem 
                { 
                    Name = result.Name, 
                    Description = result.Description,
                    Icon = result.Icon
                }
            };

            // Insert category at the beginning to keep categories first
            InsertCategoryNode(categoryNode);
            
            // Save the new category
            await SaveCategory(categoryNode);
            
            StatusText.Text = $"Created category: {result.Name}";
        }
    }

    private void InsertCategoryNode(TreeViewNode categoryNode)
    {
        // Find the position where to insert (after all categories)
        int insertIndex = 0;
        foreach (var node in LinksTreeView.RootNodes)
        {
            if (node.Content is CategoryItem)
            {
                insertIndex++;
            }
            else
            {
                break;
            }
        }
        LinksTreeView.RootNodes.Insert(insertIndex, categoryNode);
    }

    private async void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_linkDialog == null)
        {
            StatusText.Text = "Dialog not initialized";
            return;
        }

        // Check if there are any categories
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
            StatusText.Text = "Please create a category first before adding links";
            return;
        }

        // Get selected category if any
        TreeViewNode? selectedCategory = null;
        if (LinksTreeView.SelectedNode != null)
        {
            var selectedNode = LinksTreeView.SelectedNode;
            if (selectedNode.Content is CategoryItem)
            {
                selectedCategory = selectedNode;
            }
            else if (selectedNode.Parent != null)
            {
                selectedCategory = selectedNode.Parent;
            }
        }

        // Use last used category if no category is selected
        if (selectedCategory == null && _lastUsedCategory != null)
        {
            selectedCategory = _lastUsedCategory;
        }

        var selectedCategoryNode = selectedCategory != null 
            ? new CategoryNode { Name = ((CategoryItem)selectedCategory.Content).Name, Node = selectedCategory }
            : null;

        var result = await _linkDialog.ShowAddAsync(categories, selectedCategoryNode);

        if (result != null && result.CategoryNode != null)
        {
            // Create link node
            var linkNode = new TreeViewNode
            {
                Content = new LinkItem 
                { 
                    Title = result.Title, 
                    Url = result.Url,
                    Description = result.Description,
                    IsDirectory = result.IsDirectory
                }
            };

            result.CategoryNode.Children.Add(linkNode);
            result.CategoryNode.IsExpanded = true;
            _lastUsedCategory = result.CategoryNode;
            
            // Save the updated category
            await SaveCategory(result.CategoryNode);
            
            var categoryItem = result.CategoryNode.Content as CategoryItem;
            StatusText.Text = $"Added link '{result.Title}' to category '{categoryItem?.Name}'";
        }
    }

    private async void LinksTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewNode node)
        {
            if (node.Content is LinkItem linkItem)
            {
                string filePath = linkItem.Url;
                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        // Check if it's a directory
                        if (linkItem.IsDirectory || System.IO.Directory.Exists(filePath))
                        {
                            // Show directory details
                            await ShowDirectoryDetails(linkItem);
                            return;
                        }

                        // Check if it's a URL or local file
                        if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri? uri))
                        {
                            if (uri.IsFile)
                            {
                                // Load local file
                                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                                await LoadFile(file);
                            }
                            else
                            {
                                // Load URL in WebView
                                HideAllViewers();
                                if (WebViewer.CoreWebView2 == null)
                                {
                                    await WebViewer.EnsureCoreWebView2Async();
                                }
                                WebViewer.Source = uri;
                                WebViewer.Visibility = Visibility.Visible;
                                CurrentFileText.Text = linkItem.Title;
                                StatusText.Text = $"Loaded: {uri}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"Error loading link: {ex.Message}";
                    }
                }
            }
        }
    }

    private async Task ShowDirectoryDetails(LinkItem linkItem)
    {
        HideAllViewers();
        
        var detailsPanel = new StackPanel { Spacing = 12, Margin = new Thickness(20) };

        detailsPanel.Children.Add(new TextBlock
        {
            Text = linkItem.Title,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Directory Path:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 4)
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = linkItem.Url,
            IsTextSelectionEnabled = true
        });

        if (!string.IsNullOrWhiteSpace(linkItem.Description))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = "Description:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4)
            });
            detailsPanel.Children.Add(new TextBlock
            {
                Text = linkItem.Description,
                TextWrapping = TextWrapping.Wrap
            });
        }

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(linkItem.Url);
            var items = await folder.GetItemsAsync();

            detailsPanel.Children.Add(new TextBlock
            {
                Text = "Contents:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4)
            });
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"{items.Count} item(s)"
            });

            var openButton = new Button
            {
                Content = "Open in File Explorer",
                Margin = new Thickness(0, 16, 0, 0)
            };
            openButton.Click += async (s, e) =>
            {
                try
                {
                    await Windows.System.Launcher.LaunchFolderAsync(folder);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error opening folder: {ex.Message}";
                }
            };
            detailsPanel.Children.Add(openButton);
        }
        catch (Exception ex)
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"Error accessing directory: {ex.Message}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                Margin = new Thickness(0, 16, 0, 0)
            });
        }

        var scrollViewer = new ScrollViewer
        {
            Content = detailsPanel
        };

        ContentGrid.Children.Add(scrollViewer);
        scrollViewer.Visibility = Visibility.Visible;
        
        CurrentFileText.Text = linkItem.Title;
        StatusText.Text = $"Viewing directory: {linkItem.Title}";
    }

    private async void LinksTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && node.Content is CategoryItem category)
        {
            await ShowCategoryDetailsDialog(category, node);
        }
        else if (args.InvokedItem is TreeViewNode linkNode && linkNode.Content is LinkItem link)
        {
            if (_linkDialog != null)
            {
                bool shouldEdit = await _linkDialog.ShowAsync(link);
                if (shouldEdit)
                {
                    var editResult = await _linkDialog.ShowEditAsync(link);
                    if (editResult != null)
                    {
                        link.Title = editResult.Title;
                        link.Url = editResult.Url;
                        link.Description = editResult.Description;
                        link.IsDirectory = editResult.IsDirectory;
                        
                        // Force TreeView to refresh the display
                        linkNode.Content = null;
                        linkNode.Content = link;

                        // Save the updated category
                        var parentCategory = linkNode.Parent;
                        if (parentCategory != null)
                        {
                            await SaveCategory(parentCategory);
                        }

                        StatusText.Text = $"Updated link: {editResult.Title}";
                    }
                }
            }
        }
    }

    private async Task ShowCategoryDetailsDialog(CategoryItem category, TreeViewNode node)
    {
        // Create details panel
        var detailsPanel = new StackPanel { Spacing = 12 };

        // Category icon
        detailsPanel.Children.Add(new TextBlock
        {
            Text = category.Icon,
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Category name
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Category Name:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = category.Name,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Category description
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Description:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(category.Description) 
                ? "(No description)" 
                : category.Description,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Item count
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Links:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = $"{node.Children.Count} link(s)",
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Create and show dialog
        var dialog = new ContentDialog
        {
            Title = "Category Details",
            Content = detailsPanel,
            CloseButtonText = "Close",
            SecondaryButtonText = "Edit",
            PrimaryButtonText = "Delete",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            await EditCategoryDialog(category, node);
        }
        else if (result == ContentDialogResult.Primary)
        {
            await DeleteCategoryDialog(category, node);
        }
    }

    private async Task DeleteCategoryDialog(CategoryItem category, TreeViewNode node)
    {
        // Confirm deletion
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Category",
            Content = $"Are you sure you want to delete the category '{category.Icon} {category.Name}' and all its links ({node.Children.Count} link(s))?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Delete the JSON file
            await DeleteCategoryFile(category.Name);
            
            // Remove from TreeView
            LinksTreeView.RootNodes.Remove(node);
            
            // Clear last used category if it was this one
            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = null;
            }
            
            StatusText.Text = $"Deleted category: {category.Name}";
        }
    }

    private async Task EditCategoryDialog(CategoryItem category, TreeViewNode node)
    {
        if (_linkDialog == null)
        {
            StatusText.Text = "Dialog not initialized";
            return;
        }

        string oldCategoryName = category.Name;
        bool wasExpanded = node.IsExpanded;
        
        var result = await _linkDialog.ShowCategoryDialogAsync(
            "Edit Category", 
            category.Name, 
            category.Description, 
            category.Icon);

        if (result != null)
        {
            // If name changed, delete old file
            if (oldCategoryName != result.Name)
            {
                await DeleteCategoryFile(oldCategoryName);
            }

            // Create a new CategoryItem instance to force TreeView refresh
            var updatedCategory = new CategoryItem
            {
                Name = result.Name,
                Description = result.Description,
                Icon = result.Icon
            };
            
            // Store children temporarily
            var children = new List<TreeViewNode>();
            foreach (var child in node.Children)
            {
                children.Add(child);
            }
            
            // Find the node's position in the tree
            int nodeIndex = LinksTreeView.RootNodes.IndexOf(node);
            
            // Remove the old node
            LinksTreeView.RootNodes.Remove(node);
            
            // Create a new node with updated content
            var newNode = new TreeViewNode
            {
                Content = updatedCategory,
                IsExpanded = wasExpanded
            };
            
            // Restore children
            foreach (var child in children)
            {
                newNode.Children.Add(child);
            }
            
            // Insert the new node at the same position
            LinksTreeView.RootNodes.Insert(nodeIndex, newNode);
            
            // Update last used category reference if needed
            if (_lastUsedCategory == node)
            {
                _lastUsedCategory = newNode;
            }

            // Save with new name
            await SaveCategory(newNode);

            StatusText.Text = $"Updated category: {result.Name}";
        }
    }
}
