using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MyMemories.Services.Details;
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
public class DetailsViewService
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
    private ScrollViewer? _contentTabTextScroll;
    private TextBox? _contentTabText;
    private WebView2? _contentTabWeb;
    private StackPanel? _contentTabNoContent;
    private TextBlock? _contentTabNoContentText;
    
    // TabView reference
    private TabView? _detailsTabView;
    
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
    /// Sets up the tabbed details view with Summary and Content tabs.
    /// </summary>
    public void SetupTabbedView(
        TabView detailsTabView,
        StackPanel summaryPanel,
        Grid contentTabGrid,
        ScrollViewer contentTabScroll,
        StackPanel contentPanel,
        Image contentTabImage,
        ScrollViewer contentTabTextScroll,
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
        _contentTabTextScroll = contentTabTextScroll;
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
        if (_contentTabText != null) _contentTabText.Text = string.Empty;
        if (_contentTabTextScroll != null) _contentTabTextScroll.Visibility = Visibility.Collapsed;
        
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
        
        if (_contentTabText != null && _contentTabTextScroll != null)
        {
            _contentTabText.Text = content;
            _contentTabTextScroll.Visibility = Visibility.Visible;
        }
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
        if (_contentTabTextScroll != null) _contentTabTextScroll.Visibility = Visibility.Collapsed;
        
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
                or ".config" or ".ini" or ".yaml" or ".yml" or ".csv"
                or ".css" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h"
                or ".csproj" or ".sln" or ".props" or ".targets" or ".sql" or ".sh" or ".bat" or ".ps1")
            {
                try
                {
                    var content = await File.ReadAllTextAsync(url);
                    ShowContentText(content);
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
                or ".config" or ".ini" or ".yaml" or ".yml" or ".csv"
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
    public void ShowCategoryHeader(string categoryName, string? description, string icon, CategoryItem? category = null)
    {
        if (_headerBuilder == null || _headerPanel == null) return;
        _headerBuilder.ShowCategoryHeader(categoryName, description, icon, category);
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
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null, Func<Task>? onSyncBookmarks = null)
    {
        return await _categoryBuilder!.ShowCategoryDetailsAsync(category, node, onRefreshBookmarks, onRefreshUrlState, onSyncBookmarks);
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
}