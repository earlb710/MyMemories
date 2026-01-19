using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MyMemories.Services.Details;
using MyMemories.Services.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services;

/// <summary>
/// Facade service for displaying node details in the details panel.
/// Delegates to specialized builder classes for different content types.
/// Supports both Summary tab (metadata, ratings, timestamps) and Content tab (file contents).
/// </summary>
public class DetailsViewService : IDetailsViewService
{
    private readonly StackPanel _detailsPanel;
    private StackPanel? _headerPanel;
    
    // Summary tab panel
    private StackPanel? _summaryPanel;
    
    // Content tab elements
    private Grid? _contentTabGrid;
    private ScrollViewer? _contentTabScroll;
    private StackPanel? _contentPanel;
    private Image? _contentTabImage;
    private Grid? _contentTabTextGrid;  // New: wrapper grid for text content with line numbers
    private TextBox? _contentTabText;
    private WebView2? _contentTabWeb;
    private StackPanel? _contentTabNoContent;
    private TextBlock? _contentTabNoContentText;
    
    // TabView reference
    private TabView? _detailsTabView;
    
    // Line numbers support (for main content viewer)
    private Action<string>? _showLineNumbersCallback;
    private Action? _hideLineNumbersCallback;
    private Action? _setupScrollSyncCallback;
    
    private HeaderPanelBuilder? _headerBuilder;
    private UrlStatusBannerBuilder? _urlStatusBuilder;
    private CategoryDetailsBuilder? _categoryBuilder;
    private LinkDetailsBuilder? _linkBuilder;

    /// <summary>
    /// Event raised when the user requests to update a URL to its redirect target.
    /// </summary>
    public event Action<LinkItem>? UpdateUrlFromRedirectRequested;

    public DetailsViewService(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
        _urlStatusBuilder = new UrlStatusBannerBuilder(detailsPanel);
        _urlStatusBuilder.UpdateUrlRequested += OnUpdateUrlFromRedirect;
        _categoryBuilder = new CategoryDetailsBuilder(detailsPanel);
        _linkBuilder = new LinkDetailsBuilder(detailsPanel);
    }

    private void OnUpdateUrlFromRedirect(LinkItem linkItem)
    {
        UpdateUrlFromRedirectRequested?.Invoke(linkItem);
    }

    public void SetHeaderPanel(StackPanel headerPanel)
    {
        _headerPanel = headerPanel;
        _headerBuilder = new HeaderPanelBuilder(headerPanel);
        // Wire up the redirect update event from the header builder
        _headerBuilder.UpdateUrlFromRedirectRequested += OnUpdateUrlFromRedirect;
    }

    /// <summary>
    /// Sets up callbacks for line number display in main content viewer.
    /// </summary>
    public void SetLineNumberCallbacks(Action<string> showLineNumbers, Action hideLineNumbers, Action? setupScrollSync = null)
    {
        System.Diagnostics.Debug.WriteLine("[SetLineNumberCallbacks] Setting up line number callbacks");
        _showLineNumbersCallback = showLineNumbers;
        _hideLineNumbersCallback = hideLineNumbers;
        _setupScrollSyncCallback = setupScrollSync;
        System.Diagnostics.Debug.WriteLine($"[SetLineNumberCallbacks] Callbacks set - show: {_showLineNumbersCallback != null}, hide: {_hideLineNumbersCallback != null}, scrollSync: {_setupScrollSyncCallback != null}");
    }

    /// <summary>
    /// Sets up the tabbed details view with Summary and Content tabs.
    /// </summary>
    public void SetupTabbedView(
        TabView detailsTabView,
        StackPanel summaryPanel,
        Grid contentTabGrid,
        ScrollViewer contentTabScroll,
        StackPanel contentPanel,
        Image contentTabImage,
        Grid contentTabTextGrid,
        TextBox contentTabText,
        WebView2 contentTabWeb,
        StackPanel contentTabNoContent,
        TextBlock contentTabNoContentText)
    {
        _detailsTabView = detailsTabView;
        _summaryPanel = summaryPanel;
        _contentTabGrid = contentTabGrid;
        _contentTabScroll = contentTabScroll;
        _contentPanel = contentPanel;
        _contentTabImage = contentTabImage;
        _contentTabTextGrid = contentTabTextGrid;
        _contentTabText = contentTabText;
        _contentTabWeb = contentTabWeb;
        _contentTabNoContent = contentTabNoContent;
        _contentTabNoContentText = contentTabNoContentText;
        
        // Update builders to use summary panel
        _urlStatusBuilder = new UrlStatusBannerBuilder(summaryPanel);
        _urlStatusBuilder.UpdateUrlRequested += OnUpdateUrlFromRedirect;
        _categoryBuilder = new CategoryDetailsBuilder(summaryPanel);
        _linkBuilder = new LinkDetailsBuilder(summaryPanel);
    }

    /// <summary>
    /// Clears all content from the tabbed view but preserves tab selection.
    /// </summary>
    public void ClearTabbedView()
    {
        ClearTabbedViewContent();
    }

    /// <summary>
    /// Clears content from both tabs but preserves tab selection.
    /// </summary>
    public void ClearTabbedViewContent()
    {
        _summaryPanel?.Children.Clear();
        _contentPanel?.Children.Clear();
        
        // Clear image
        if (_contentTabImage != null)
        {
            _contentTabImage.Source = null;
            _contentTabImage.Visibility = Visibility.Collapsed;
        }
        
        // Clear text
        if (_contentTabText != null)
        {
            _contentTabText.Text = string.Empty;
        }
        
        // Clear WebView - navigate to blank page to stop any loading content
        if (_contentTabWeb != null)
        {
            _contentTabWeb.Visibility = Visibility.Collapsed;
            // Navigate to about:blank to clear any loaded content
            if (_contentTabWeb.CoreWebView2 != null)
            {
                try
                {
                    _contentTabWeb.CoreWebView2.Navigate("about:blank");
                }
                catch
                {
                    // Ignore navigation errors during cleanup
                }
            }
        }
        
        if (_contentTabScroll != null) _contentTabScroll.Visibility = Visibility.Collapsed;
        if (_contentTabNoContent != null) _contentTabNoContent.Visibility = Visibility.Collapsed;
        
        // Note: Tab selection is preserved - we don't change SelectedIndex here
    }

    /// <summary>
    /// Shows the tabbed view and selects the Summary tab.
    /// </summary>
    public void ShowTabbedView()
    {
        if (_detailsTabView != null)
        {
            _detailsTabView.Visibility = Visibility.Visible;
            _detailsTabView.SelectedIndex = 0; // Select Summary tab
        }
    }

    /// <summary>
    /// Shows text content in the Content tab.
    /// </summary>
    public void ShowContentText(string content)
    {
        HideAllContentElements();
        
        if (_contentTabText != null && _contentTabTextGrid != null)
        {
            _contentTabText.Text = content;
            _contentTabTextGrid.Visibility = Visibility.Visible;
        }
        
        _hideLineNumbersCallback?.Invoke();
    }

    /// <summary>
    /// Shows text content with line numbers in the Content tab.
    /// </summary>
    public void ShowContentTextWithLineNumbers(string content)
    {
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] === START ===");
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] Called with content length: {content?.Length ?? 0}");
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] First 100 chars: {(content?.Length > 100 ? content.Substring(0, 100) : content)}");
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] Callback is null: {_showLineNumbersCallback == null}");
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] _contentTabText is null: {_contentTabText == null}");
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] _contentTabTextGrid is null: {_contentTabTextGrid == null}");
        
        HideAllContentElements();
        
        if (_contentTabText != null && _contentTabTextGrid != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] Setting text to {content.Length} characters");
            
            _contentTabText.Text = content;
            _contentTabTextGrid.Visibility = Visibility.Visible;
            
            System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] Text set, length: {_contentTabText.Text.Length}");
            System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] ContentTabTextGrid.Visibility: {_contentTabTextGrid.Visibility}");
            
            // Switch to Content tab
            if (_detailsTabView != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] Switching to Content tab (index 1)");
                _detailsTabView.SelectedIndex = 1;
            }
            
            // Set up scroll synchronization
            if (_setupScrollSyncCallback != null)
            {
                System.Diagnostics.Debug.WriteLine("[ShowContentTextWithLineNumbers] Invoking scroll sync setup callback");
                _setupScrollSyncCallback.Invoke();
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] ERROR: TextBox or Grid is null!");
        }
        
        if (_showLineNumbersCallback != null)
        {
            System.Diagnostics.Debug.WriteLine("[ShowContentTextWithLineNumbers] Invoking line numbers callback");
            _showLineNumbersCallback.Invoke(content);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ShowContentTextWithLineNumbers] WARNING: Line numbers callback is NULL!");
        }
        
        System.Diagnostics.Debug.WriteLine($"[ShowContentTextWithLineNumbers] === END ===");
    }

    /// <summary>
    /// Shows an image in the Content tab.
    /// </summary>
    public async Task ShowContentImageAsync(string filePath)
    {
        HideAllContentElements();
        
        if (_contentTabImage != null && File.Exists(filePath))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var bitmap = new BitmapImage();
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                await bitmap.SetSourceAsync(stream);
                
                _contentTabImage.Source = bitmap;
                _contentTabImage.Visibility = Visibility.Visible;
                
                // Note: Don't auto-switch tabs - preserve user's tab selection
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContentImageAsync] Error loading image: {ex.Message}");
                ShowContentMessage($"Error loading image: {ex.Message}");
            }
        }
    }

    // Guard to prevent re-entry during content loading
    private bool _isLoadingWebContent = false;

    /// <summary>
    /// Shows web content (HTML, PDF, URL) in the Content tab.
    /// </summary>
    public async Task ShowContentWebAsync(string urlOrPath)
    {
        // Prevent re-entry
        if (_isLoadingWebContent)
        {
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] SKIPPED - Already loading content");
            return;
        }
        
        _isLoadingWebContent = true;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] START - URL: {urlOrPath}");
            
            HideAllContentElements();
            
            if (_contentTabWeb == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] ERROR: _contentTabWeb is null!");
                ShowContentMessage("WebView not available - setup error");
                return;
            }
            
            // Make WebView visible FIRST
            _contentTabWeb.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] WebView Visibility set to Visible");
            
            // Initialize WebView2 if needed
            if (_contentTabWeb.CoreWebView2 == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] Initializing CoreWebView2...");
                await _contentTabWeb.EnsureCoreWebView2Async();
                System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] CoreWebView2 initialized");
            }
            
            Uri? targetUri = null;
            
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri))
            {
                targetUri = uri;
            }
            else if (File.Exists(urlOrPath))
            {
                var file = await StorageFile.GetFileFromPathAsync(urlOrPath);
                targetUri = new Uri(file.Path);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] ERROR: Cannot parse URI or file not found: {urlOrPath}");
                ShowContentMessage($"Cannot load: {urlOrPath}");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] Setting Source to: {targetUri}");
            _contentTabWeb.Source = targetUri;
            
            // Switch to Content tab to show the web content
            if (_detailsTabView != null)
            {
                _detailsTabView.SelectedIndex = 1; // Content tab
                System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] Switched to Content tab");
            }
            
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] DONE - WebView should now be loading URL");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShowContentWebAsync] StackTrace: {ex.StackTrace}");
            ShowContentMessage($"Error loading content: {ex.Message}");
        }
        finally
        {
            _isLoadingWebContent = false;
        }
    }

    /// <summary>
    /// Shows a message in the Content tab (e.g., "No content", "Cannot preview").
    /// </summary>
    public void ShowContentMessage(string message)
    {
        HideAllContentElements();
        
        if (_contentTabNoContent != null && _contentTabNoContentText != null)
        {
            _contentTabNoContentText.Text = message;
            _contentTabNoContent.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Hides all content elements in the Content tab and clears the WebView.
    /// </summary>
    private void HideAllContentElements()
    {
        if (_contentTabImage != null) _contentTabImage.Visibility = Visibility.Collapsed;
        if (_contentTabTextGrid != null) _contentTabTextGrid.Visibility = Visibility.Collapsed;
        
        // Hide and clear WebView to prevent old content from showing
        if (_contentTabWeb != null)
        {
            _contentTabWeb.Visibility = Visibility.Collapsed;
            // Navigate to about:blank to clear any loaded content
            if (_contentTabWeb.CoreWebView2 != null)
            {
                try
                {
                    _contentTabWeb.CoreWebView2.Navigate("about:blank");
                }
                catch
                {
                    // Ignore navigation errors during cleanup
                }
            }
        }
        
        if (_contentTabScroll != null) _contentTabScroll.Visibility = Visibility.Collapsed;
        if (_contentTabNoContent != null) _contentTabNoContent.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Loads content for a link item into the Content tab.
    /// </summary>
    public async Task LoadContentForLinkAsync(LinkItem linkItem)
    {
        System.Diagnostics.Debug.WriteLine($"[LoadContentForLinkAsync] START - Title: {linkItem?.Title ?? "null"}, URL: {linkItem?.Url ?? "null"}");
        
        if (linkItem == null)
        {
            ShowContentMessage("No content to display");
            return;
        }

        var url = linkItem.Url;
        
        // Check if it's a zip entry
        if (!string.IsNullOrEmpty(url) && url.Contains("::"))
        {
            System.Diagnostics.Debug.WriteLine($"[LoadContentForLinkAsync] Detected zip entry, loading...");
            await LoadZipEntryContentAsync(linkItem);
            return;
        }
        
        // Check if it's a web URL
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadContentForLinkAsync] Detected web URL: {url}");
            await ShowContentWebAsync(url);
            return;
        }

        // Check if it's a directory
        if (linkItem.IsDirectory)
        {
            if (!string.IsNullOrEmpty(url) && Directory.Exists(url))
            {
                ShowContentMessage("Directory selected. Use the Summary tab for details, or double-click to open in Explorer.");
            }
            else
            {
                ShowContentMessage("Directory not accessible.");
            }
            return;
        }

        // Check if it's a file
        if (!string.IsNullOrEmpty(url) && File.Exists(url))
        {
            var extension = Path.GetExtension(url).ToLowerInvariant();
            
            // Image files
            if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".ico" or ".webp")
            {
                await ShowContentImageAsync(url);
                return;
            }
            
            // Text-based files
            if (extension is ".txt" or ".xml" or ".json" or ".md" or ".log" or ".cs" or ".xaml" 
                or ".config" or ".ini" or ".yaml" or ".yml" or ".csv" or ".manifest"
                or ".css" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h"
                or ".csproj" or ".sln" or ".props" or ".targets" or ".sql" or ".sh" or ".bat" or ".ps1")
            {
                try
                {
                    var content = await File.ReadAllTextAsync(url);
                    
                    bool shouldShowLineNumbers = ShouldShowLineNumbers(extension);
                    System.Diagnostics.Debug.WriteLine($"[LoadContentForLinkAsync] Extension: {extension}, ShouldShowLineNumbers: {shouldShowLineNumbers}");
                    
                    // Add line numbers for source code files
                    if (shouldShowLineNumbers)
                    {
                        System.Diagnostics.Debug.WriteLine("[LoadContentForLinkAsync] Calling ShowContentTextWithLineNumbers");
                        ShowContentTextWithLineNumbers(content);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LoadContentForLinkAsync] Calling ShowContentText (no line numbers)");
                        ShowContentText(content);
                    }
                }
                catch (Exception ex)
                {
                    ShowContentMessage($"Error reading file: {ex.Message}");
                }
                return;
            }
            
            // PDF and HTML in WebView
            if (extension is ".pdf" or ".html" or ".htm")
            {
                await ShowContentWebAsync(url);
                return;
            }
            
            // Zip files
            if (extension is ".zip")
            {
                ShowContentMessage("Zip archive. Use the Summary tab for details, or expand to browse contents.");
                return;
            }
            
            // Unknown file type
            ShowContentMessage($"Cannot preview files with extension: {extension}\nDouble-click to open with default application.");
            return;
        }

        // No content
        if (string.IsNullOrEmpty(url))
        {
            ShowContentMessage("No URL or path specified for this link.");
        }
        else
        {
            ShowContentMessage($"File or URL not accessible: {url}");
        }
    }

    /// <summary>
    /// Loads content from a zip entry into the Content tab.
    /// </summary>
    private async Task LoadZipEntryContentAsync(LinkItem linkItem)
    {
        var url = linkItem.Url;
        
        // Parse zip entry URL (format: zipPath::entryPath)
        var parts = url.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            ShowContentMessage("Invalid zip entry format.");
            return;
        }

        var zipPath = parts[0];
        var entryPath = parts[1];

        if (!File.Exists(zipPath))
        {
            ShowContentMessage($"Zip archive not found: {zipPath}");
            return;
        }

        // Directory entries in zip
        if (linkItem.IsDirectory)
        {
            ShowContentMessage("Zip folder selected. Use the Summary tab for details.");
            return;
        }

        try
        {
            var extension = Path.GetExtension(entryPath).ToLowerInvariant();

            // Image files from zip
            if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".ico" or ".webp")
            {
                await LoadZipEntryImageAsync(zipPath, entryPath);
                return;
            }

            // Text files from zip
            if (extension is ".txt" or ".xml" or ".json" or ".md" or ".log" or ".cs" or ".xaml" 
                or ".config" or ".ini" or ".yaml" or ".yml" or ".csv" or ".manifest"
                or ".css" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h"
                or ".csproj" or ".sln" or ".props" or ".targets" or ".sql" or ".sh" or ".bat" or ".ps1")
            {
                await LoadZipEntryTextAsync(zipPath, entryPath);
                return;
            }

            // Other file types
            ShowContentMessage($"Cannot preview {extension} files from zip archive.\nDouble-click to extract and open.");
        }
        catch (Exception ex)
        {
            ShowContentMessage($"Error reading zip entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads an image from a zip entry.
    /// </summary>
    private async Task LoadZipEntryImageAsync(string zipPath, string entryPath)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var normalizedPath = entryPath.Replace('\\', '/');
            var entry = archive.GetEntry(normalizedPath) ?? archive.GetEntry(entryPath);
            
            if (entry == null)
            {
                ShowContentMessage($"Entry not found in archive: {entryPath}");
                return;
            }

            using var entryStream = entry.Open();
            using var memoryStream = new MemoryStream();
            await entryStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(memoryStream.AsRandomAccessStream());

            HideAllContentElements();
            if (_contentTabImage != null)
            {
                _contentTabImage.Source = bitmap;
                _contentTabImage.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ShowContentMessage($"Error loading image from zip: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads text content from a zip entry.
    /// </summary>
    private async Task LoadZipEntryTextAsync(string zipPath, string entryPath)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var normalizedPath = entryPath.Replace('\\', '/');
            var entry = archive.GetEntry(normalizedPath) ?? archive.GetEntry(entryPath);
            
            if (entry == null)
            {
                ShowContentMessage($"Entry not found in archive: {entryPath}");
                return;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var content = await reader.ReadToEndAsync();

            ShowContentText(content);
        }
        catch (Exception ex)
        {
            ShowContentMessage($"Error reading text from zip: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows file header information with name, description, and size.
    /// </summary>
    public async Task ShowFileHeaderAsync(string fileName, string? description, StorageFile file, BitmapImage? bitmap = null)
    {
        if (_headerBuilder == null || _headerPanel == null) return;
        await _headerBuilder.ShowFileHeaderAsync(fileName, description, file, bitmap);
    }

    /// <summary>
    /// Shows category header in the header panel with icon on the left.
    /// </summary>
    public void ShowCategoryHeader(string categoryName, string? description, string icon, CategoryItem? category = null, bool isRootCategory = false)
    {
        if (_headerBuilder == null || _headerPanel == null) return;
        _headerBuilder.ShowCategoryHeader(categoryName, description, icon, category, isRootCategory);
    }

    /// <summary>
    /// Shows link header in the header panel with icon on the left and optional link badge.
    /// </summary>
    public void ShowLinkHeader(string linkTitle, string? description, string icon, bool showLinkBadge = false, 
        ulong? fileSize = null, DateTime? createdDate = null, DateTime? modifiedDate = null, LinkItem? linkItem = null)
    {
        if (_headerBuilder == null || _headerPanel == null) return;
        _headerBuilder.ShowLinkHeader(linkTitle, description, icon, showLinkBadge, fileSize, createdDate, modifiedDate, linkItem);
    }

    /// <summary>
    /// Shows URL status banner at the top of the details panel for non-accessible URLs or redirects.
    /// </summary>
    public void ShowUrlStatusBanner(LinkItem linkItem)
    {
        if (linkItem.UrlStatus != UrlStatus.Unknown && linkItem.UrlStatus != UrlStatus.Accessible)
        {
            _urlStatusBuilder?.ShowUrlStatusBanner(linkItem);
        }
    }

    /// <summary>
    /// Shows category details in the Summary tab.
    /// </summary>
    public async Task<Button?> ShowCategoryDetailsAsync(CategoryItem category, TreeViewNode node, 
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null, Func<Task>? onSyncBookmarks = null,
        Func<string, Task>? onClearArchive = null)
    {
        return await _categoryBuilder!.ShowCategoryDetailsAsync(category, node, onRefreshBookmarks, onRefreshUrlState, onSyncBookmarks, onClearArchive);
    }

    /// <summary>
    /// Shows link details in the Summary tab with file information and catalog buttons for directories.
    /// </summary>
    public async Task<(Button? createButton, Button? refreshButton)> ShowLinkDetailsAsync(
        LinkItem linkItem,
        TreeViewNode? node,
        Func<Task> onCreateCatalog,
        Func<Task> onRefreshCatalog,
        Func<Task>? onRefreshArchive = null,
        Func<Task>? onSaveCategory = null)
    {
        return await _linkBuilder!.ShowLinkDetailsAsync(linkItem, node, onCreateCatalog, onRefreshCatalog, onRefreshArchive, onSaveCategory);
    }

    /// <summary>
    /// Adds an "Open in Explorer" button for directories.
    /// </summary>
    public async Task<Button?> AddOpenInExplorerButtonAsync(String path)
    {
        return await _linkBuilder!.AddOpenInExplorerButtonAsync(path);
    }

    /// <summary>
    /// Determines if a file type should display line numbers.
    /// Only source code and structured data files, not plain text or logs.
    /// </summary>
    private static bool ShouldShowLineNumbers(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;
            
        extension = extension.ToLowerInvariant();
        
        // Ensure extension starts with a dot
        if (!extension.StartsWith("."))
            extension = "." + extension;
        
        return extension switch
        {
            // Programming languages
            ".cs" or ".java" or ".cpp" or ".c" or ".h" or ".hpp" or ".py" or ".js" or ".ts" or
            ".jsx" or ".tsx" or ".php" or ".rb" or ".go" or ".rs" or ".swift" or ".kt" or 
            ".scala" or ".m" or ".mm" or ".vb" or ".fs" or ".dart" or ".lua" or
            
            // Markup and structured data
            ".json" or ".xml" or ".html" or ".htm" or ".css" or ".scss" or ".sass" or ".less" or
            ".yaml" or ".yml" or ".toml" or ".config" or ".xaml" or ".manifest" or
            
            // Scripts and configuration
            ".sql" or ".sh" or ".bash" or ".bat" or ".cmd" or ".ps1" or ".psm1" or
            ".ini" or ".properties" or ".conf" or
            
            // Markdown and documentation (with code)
            ".md" or ".markdown" => true,
            
            _ => false
        };
    }
    
    /// <summary>
    /// Shows search results in the Summary panel.
    /// </summary>
    public async Task ShowSearchResultsAsync(SearchExecutionResult result, Func<SearchResultItem, Task> onNavigateToItem)
    {
        var panel = _summaryPanel ?? _detailsPanel;
        panel.Children.Clear();
        
        // Header
        panel.Children.Add(new TextBlock
        {
            Text = $"Search Results: {result.Search.Name}",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        // Statistics
        panel.Children.Add(new TextBlock
        {
            Text = $"Found {result.Results.Count} item(s) in {result.ExecutionTime.TotalMilliseconds:F0}ms",
            FontSize = 13,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 0, 0, 16)
        });
        
        if (result.Results.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No items match the search criteria.",
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }
        
        // Results list
        var resultsPanel = new StackPanel { Spacing = 4 };
        
        foreach (var item in result.Results)
        {
            var itemBorder = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 2, 0, 2)
            };
            
            var itemGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 12
            };
            
            // Icon
            var iconText = new TextBlock
            {
                Text = item.Icon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            itemGrid.Children.Add(iconText);
            
            // Content
            var contentStack = new StackPanel { Spacing = 2 };
            
            // Name with type badge
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            nameRow.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            nameRow.Children.Add(new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = item.ItemType,
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                }
            });
            contentStack.Children.Add(nameRow);
            
            // Category path
            contentStack.Children.Add(new TextBlock
            {
                Text = item.CategoryPath,
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            
            // Description if present
            if (!string.IsNullOrEmpty(item.Description))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = item.Description,
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1
                });
            }
            
            Grid.SetColumn(contentStack, 1);
            itemGrid.Children.Add(contentStack);
            
            itemBorder.Child = itemGrid;
            
            // Click handler
            var capturedItem = item;
            itemBorder.PointerPressed += async (s, e) =>
            {
                await onNavigateToItem(capturedItem);
            };
            
            // Hover effect
            itemBorder.PointerEntered += (s, e) =>
            {
                itemBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215));
            };
            itemBorder.PointerExited += (s, e) =>
            {
                itemBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128));
            };
            
            resultsPanel.Children.Add(itemBorder);
        }
        
        panel.Children.Add(resultsPanel);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Shows the Searches node details with Add Search button.
    /// </summary>
    public void ShowSearchesNodeDetails(int searchCount, Func<Task> onAddSearch)
    {
        var panel = _summaryPanel ?? _detailsPanel;
        panel.Children.Clear();
        
        // Header
        panel.Children.Add(new TextBlock
        {
            Text = "Saved Searches",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        // Description
        panel.Children.Add(new TextBlock
        {
            Text = "Create and save complex searches with AND, OR, and NOT conditions. Saved searches can be executed at any time to find matching items across all categories.",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 0, 0, 16)
        });
        
        // Statistics
        var statsPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 16) };
        
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"Saved Searches: {searchCount}",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        
        panel.Children.Add(statsPanel);
        
        // Add Search button
        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 16 },
                    new TextBlock { Text = "Add Search", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Margin = new Thickness(0, 0, 0, 16)
        };
        
        addButton.Click += async (s, e) =>
        {
            await onAddSearch();
        };
        
        panel.Children.Add(addButton);
        
        // Help section
        var helpPanel = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 0, 120, 215)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 16, 0, 0)
        };
        
        var helpContent = new StackPanel { Spacing = 8 };
        helpContent.Children.Add(new TextBlock
        {
            Text = "How to Use Saved Searches",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        helpContent.Children.Add(new TextBlock
        {
            Text = "• Click 'Add Search' to create a new saved search\n• Define conditions using field, operator, and value\n• Use groups for complex AND/OR logic\n• Use NOT to exclude items matching a condition\n• Double-click a saved search to execute it",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        
        helpPanel.Child = helpContent;
        panel.Children.Add(helpPanel);
    }
    
    /// <summary>
    /// Shows details for a specific saved search with Run, Edit, Delete buttons.
    /// </summary>
    public async Task ShowSavedSearchDetailsAsync(SavedSearch search, Func<Task> onRun, Func<Task> onEdit, Func<Task> onDelete)
    {
        var panel = _summaryPanel ?? _detailsPanel;
        panel.Children.Clear();
        
        // Header
        panel.Children.Add(new TextBlock
        {
            Text = search.Name,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        // Description
        if (!string.IsNullOrEmpty(search.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = search.Description,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 0, 0, 16)
            });
        }
        
        // Action buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };
        
        // Run button
        var runButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE768", FontSize = 16 }, // Play icon
                    new TextBlock { Text = "Run Search", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        runButton.Click += async (s, e) => await onRun();
        buttonPanel.Children.Add(runButton);
        
        // Edit button
        var editButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE70F", FontSize = 16 }, // Edit icon
                    new TextBlock { Text = "Edit", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        editButton.Click += async (s, e) => await onEdit();
        buttonPanel.Children.Add(editButton);
        
        // Delete button
        var deleteButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE74D", FontSize = 16 }, // Delete icon
                    new TextBlock { Text = "Delete", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        deleteButton.Click += async (s, e) => await onDelete();
        buttonPanel.Children.Add(deleteButton);
        
        panel.Children.Add(buttonPanel);
        
        // Statistics
        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"Created: {search.CreatedDate:yyyy-MM-dd HH:mm}",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        });
        
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"Modified: {search.ModifiedDate:yyyy-MM-dd HH:mm}",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        });
        
        // Show included categories
        var categoriesText = search.IncludedCategories.Count == 0 
            ? "All Categories" 
            : string.Join(", ", search.IncludedCategories);
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"Searches In: {categoriesText}",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });
        
        if (search.LastExecutedDate.HasValue)
        {
            statsPanel.Children.Add(new TextBlock
            {
                Text = $"Last Run: {search.LastExecutedDate:yyyy-MM-dd HH:mm} ({search.LastResultCount ?? 0} results)",
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
        }
        
        panel.Children.Add(statsPanel);
        
        // Conditions section
        panel.Children.Add(new TextBlock
        {
            Text = "Search Conditions",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 8)
        });
        
        var conditionsPanel = new StackPanel { Spacing = 8 };
        
        foreach (var group in search.ConditionGroups)
        {
            var groupBorder = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 0, 120, 215)),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8)
            };
            
            var groupContent = new StackPanel { Spacing = 4 };
            
            // Group header
            var groupHeader = group.IsNegated ? "NOT (" : "";
            groupContent.Children.Add(new TextBlock
            {
                Text = $"{groupHeader}Condition Group ({group.GroupOperator})",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            
            // Conditions
            foreach (var condition in group.Conditions)
            {
                var conditionText = $"  {condition.ConditionOperator} {condition.Field} {condition.Operator} \"{condition.Value}\"";
                if (condition.IsNegated)
                    conditionText = "  NOT " + conditionText.TrimStart();
                
                groupContent.Children.Add(new TextBlock
                {
                    Text = conditionText,
                    FontSize = 11,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                });
            }
            
            if (group.IsNegated)
            {
                groupContent.Children.Add(new TextBlock { Text = ")", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            }
            
            groupBorder.Child = groupContent;
            conditionsPanel.Children.Add(groupBorder);
        }
        
        panel.Children.Add(conditionsPanel);
        
        await Task.CompletedTask;
    }
}
