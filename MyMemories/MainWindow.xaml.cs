using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private TreeViewNode? _contextMenuNode;

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
        DetailsViewerScroll.Visibility = Visibility.Collapsed;
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
            if (node.Content is CategoryItem category)
            {
                // Show category details on the right side
                await ShowCategoryDetails(category, node);
            }
            else if (node.Content is LinkItem linkItem)
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
                            await ShowLinkDirectoryDetails(linkItem);
                            return;
                        }

                        // Check if it's a URL or local file
                        if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri? uri))
                        {
                            if (uri.IsFile)
                            {
                                // Show link details first
                                await ShowLinkDetails(linkItem);
                                
                                // Load local file
                                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                                await LoadFile(file);
                            }
                            else
                            {
                                // Show link details for URL
                                await ShowLinkDetails(linkItem);
                                
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
                        await ShowLinkDetails(linkItem);
                    }
                }
                else
                {
                    // Show link details even if URL is empty
                    await ShowLinkDetails(linkItem);
                }
            }
        }
    }

    private async Task ShowCategoryDetails(CategoryItem category, TreeViewNode node)
    {
        HideAllViewers();
        DetailsPanel.Children.Clear();

        // Category icon (large)
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = category.Icon,
            FontSize = 64,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Category name
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = category.Name,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Divider
        DetailsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 16, 0, 16)
        });

        // Description section
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = "Description",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(category.Description) 
                ? "(No description provided)" 
                : category.Description,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = string.IsNullOrWhiteSpace(category.Description)
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                : null
        });

        // Statistics section
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = "Statistics",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"📊 Total Links: {node.Children.Count}",
            FontSize = 14
        });

        int fileCount = 0;
        int dirCount = 0;
        int urlCount = 0;
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                if (link.IsDirectory)
                    dirCount++;
                else if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && !uri.IsFile)
                    urlCount++;
                else
                    fileCount++;
            }
        }

        statsPanel.Children.Add(new TextBlock
        {
            Text = $"📄 Files: {fileCount}",
            FontSize = 14
        });
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"📁 Directories: {dirCount}",
            FontSize = 14
        });
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"🌐 URLs: {urlCount}",
            FontSize = 14
        });

        DetailsPanel.Children.Add(statsPanel);

        // Links list
        if (node.Children.Count > 0)
        {
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = "Links in this Category",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var linksListPanel = new StackPanel { Spacing = 8 };
            foreach (var child in node.Children)
            {
                if (child.Content is LinkItem link)
                {
                    var linkCard = new Border
                    {
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Transparent),
                        BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(12),
                        Margin = new Thickness(0, 0, 0, 4)
                    };

                    var linkInfo = new StackPanel { Spacing = 4 };
                    linkInfo.Children.Add(new TextBlock
                    {
                        Text = link.ToString(),
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });

                    if (!string.IsNullOrWhiteSpace(link.Description))
                    {
                        linkInfo.Children.Add(new TextBlock
                        {
                            Text = link.Description,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                        });
                    }

                    linkCard.Child = linkInfo;
                    linksListPanel.Children.Add(linkCard);
                }
            }
            DetailsPanel.Children.Add(linksListPanel);
        }

        DetailsViewerScroll.Visibility = Visibility.Visible;
        CurrentFileText.Text = $"Category: {category.Name}";
        StatusText.Text = $"Viewing category: {category.Name} ({node.Children.Count} link(s))";
    }

    private async Task ShowLinkDetails(LinkItem linkItem)
    {
        HideAllViewers();
        DetailsPanel.Children.Clear();

        // Link icon
        string linkIcon = linkItem.IsDirectory ? "📁" : "🔗";
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = linkIcon,
            FontSize = 64,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Link title
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = linkItem.Title,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        // Type badge
        string typeText = linkItem.IsDirectory ? "Directory" : 
            (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var uri) && !uri.IsFile ? "Web URL" : "File");
        
        var typeBorder = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4, 12, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        typeBorder.Child = new TextBlock
        {
            Text = typeText,
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        DetailsPanel.Children.Add(typeBorder);

        // Divider
        DetailsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Path/URL section
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = linkItem.IsDirectory ? "Directory Path" : "Path/URL",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = linkItem.Url,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Description section
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = "Description",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(linkItem.Description) 
                ? "(No description provided)" 
                : linkItem.Description,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = string.IsNullOrWhiteSpace(linkItem.Description)
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                : null
        });

        // File/Directory information
        try
        {
            if (linkItem.IsDirectory && Directory.Exists(linkItem.Url))
            {
                var dirInfo = new DirectoryInfo(linkItem.Url);
                
                DetailsPanel.Children.Add(new TextBlock
                {
                    Text = "Directory Information",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📅 Created: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📝 Last Modified: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"👁️ Last Accessed: {dirInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });

                try
                {
                    var files = dirInfo.GetFiles();
                    var dirs = dirInfo.GetDirectories();
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"📄 Contains: {files.Length} file(s), {dirs.Length} folder(s)",
                        FontSize = 14
                    });
                }
                catch { }

                DetailsPanel.Children.Add(infoPanel);
            }
            else if (File.Exists(linkItem.Url))
            {
                var fileInfo = new FileInfo(linkItem.Url);
                
                DetailsPanel.Children.Add(new TextBlock
                {
                    Text = "File Information",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📦 Size: {FormatFileSize((ulong)fileInfo.Length)}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📂 Extension: {fileInfo.Extension}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📅 Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📝 Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"👁️ Last Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 14
                });

                DetailsPanel.Children.Add(infoPanel);
            }
        }
        catch (Exception ex)
        {
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = $"⚠️ Unable to access file/directory information: {ex.Message}",
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });
        }

        DetailsViewerScroll.Visibility = Visibility.Visible;
        CurrentFileText.Text = linkItem.Title;
        StatusText.Text = $"Viewing link: {linkItem.Title}";
    }

    private async Task ShowLinkDirectoryDetails(LinkItem linkItem)
    {
        await ShowLinkDetails(linkItem);

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(linkItem.Url);
            var items = await folder.GetItemsAsync();

            // Add open button at the end
            var openButton = new Button
            {
                Content = "Open in File Explorer",
                HorizontalAlignment = HorizontalAlignment.Stretch,
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
            DetailsPanel.Children.Add(openButton);
        }
        catch (Exception ex)
        {
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = $"Error accessing directory: {ex.Message}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 16, 0, 0)
            });
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
            
            // Show welcome screen
            ShowWelcome();
            
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
            
            // Refresh details view if this node is selected
            if (LinksTreeView.SelectedNode == newNode)
            {
                await ShowCategoryDetails(updatedCategory, newNode);
            }
        }
    }

    // Right-click context menu handlers
    private void LinksTreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element)
        {
            // Find the TreeViewItem that was right-clicked
            var treeViewItem = FindParent<TreeViewItem>(element);
            if (treeViewItem?.Content is TreeViewNode node)
            {
                _contextMenuNode = node;
                
                // Show appropriate context menu based on node type
                if (node.Content is CategoryItem)
                {
                    var categoryMenu = LinksTreeView.Resources["CategoryContextMenu"] as MenuFlyout;
                    categoryMenu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
                }
                else if (node.Content is LinkItem)
                {
                    var linkMenu = LinksTreeView.Resources["LinkContextMenu"] as MenuFlyout;
                    linkMenu?.ShowAt(treeViewItem, e.GetPosition(treeViewItem));
                }
                
                e.Handled = true;
            }
        }
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);

        if (parentObject == null)
            return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
    }

    // Category context menu handlers
    private async void CategoryMenu_AddLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode == null || _linkDialog == null)
            return;

        var categories = LinksTreeView.RootNodes
            .Where(n => n.Content is CategoryItem)
            .Select(n => new CategoryNode 
            { 
                Name = ((CategoryItem)n.Content).Name, 
                Node = n 
            })
            .ToList();

        var selectedCategoryNode = new CategoryNode 
        { 
            Name = ((CategoryItem)_contextMenuNode.Content).Name, 
            Node = _contextMenuNode 
        };

        var result = await _linkDialog.ShowAddAsync(categories, selectedCategoryNode);

        if (result != null && result.CategoryNode != null)
        {
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
            
            await SaveCategory(result.CategoryNode);
            
            StatusText.Text = $"Added link '{result.Title}'";
            
            // Refresh details if category is still selected
            if (LinksTreeView.SelectedNode == result.CategoryNode)
            {
                await ShowCategoryDetails((CategoryItem)result.CategoryNode.Content, result.CategoryNode);
            }
        }
    }

    private async void CategoryMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await EditCategoryDialog(category, _contextMenuNode);
        }
    }

    private async void CategoryMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is CategoryItem category)
        {
            await DeleteCategoryDialog(category, _contextMenuNode);
        }
    }

    // Link context menu handlers
    private async void LinkMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link && _linkDialog != null)
        {
            var editResult = await _linkDialog.ShowEditAsync(link);
            if (editResult != null)
            {
                link.Title = editResult.Title;
                link.Url = editResult.Url;
                link.Description = editResult.Description;
                link.IsDirectory = editResult.IsDirectory;
                
                // Force TreeView to refresh
                _contextMenuNode.Content = null;
                _contextMenuNode.Content = link;

                // Save the updated category
                var parentCategory = _contextMenuNode.Parent;
                if (parentCategory != null)
                {
                    await SaveCategory(parentCategory);
                }

                StatusText.Text = $"Updated link: {editResult.Title}";
                
                // Refresh details if link is still selected
                if (LinksTreeView.SelectedNode == _contextMenuNode)
                {
                    await ShowLinkDetails(link);
                }
            }
        }
    }

    private async void LinkMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link && _contextMenuNode.Parent != null)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Remove Link",
                Content = $"Are you sure you want to remove the link '{link.Title}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var parentCategory = _contextMenuNode.Parent;
                parentCategory.Children.Remove(_contextMenuNode);
                
                await SaveCategory(parentCategory);
                
                // Show welcome screen or parent category details
                if (LinksTreeView.SelectedNode == _contextMenuNode)
                {
                    ShowWelcome();
                }
                
                StatusText.Text = $"Removed link: {link.Title}";
            }
        }
    }
}
