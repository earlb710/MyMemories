using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services;

public class LinkSelectionService
{
    private readonly DetailsViewService _detailsViewService;
    private readonly FileViewerService _fileViewerService;
    private readonly TreeViewService _treeViewService;
    private readonly CatalogService _catalogService;
    private readonly FileLauncherService _fileLauncherService;
    private readonly CategoryService _categoryService;
    private readonly UrlStateCheckerService? _urlStateCheckerService;
    private TextBox? _urlTextBox;

    public LinkSelectionService(
        DetailsViewService detailsViewService,
        FileViewerService fileViewerService,
        TreeViewService treeViewService,
        CatalogService catalogService,
        FileLauncherService fileLauncherService,
        CategoryService categoryService,
        UrlStateCheckerService? urlStateCheckerService = null)
    {
        _detailsViewService = detailsViewService;
        _fileViewerService = fileViewerService;
        _treeViewService = treeViewService;
        _catalogService = catalogService;
        _fileLauncherService = fileLauncherService;
        _categoryService = categoryService;
        _urlStateCheckerService = urlStateCheckerService;
    }

    /// <summary>
    /// Sets the URL text box for web navigation.
    /// </summary>
    public void SetUrlTextBox(TextBox urlTextBox)
    {
        _urlTextBox = urlTextBox;
    }

    public async Task HandleLinkSelectionAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<FileViewerType> showViewer, Action<string> setStatus)
    {
        System.Diagnostics.Debug.WriteLine($"[HandleLinkSelectionAsync] START - Title: {linkItem.Title}, URL: {linkItem.Url ?? "(null)"}");
        
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            System.Diagnostics.Debug.WriteLine($"[HandleLinkSelectionAsync] Empty URL - calling HandleEmptyUrlAsync");
            await HandleEmptyUrlAsync(linkItem, linkNode, hideAllViewers, showDetailsViewers, setStatus);
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[HandleLinkSelectionAsync] Checking if zip entry URL...");
        if (ZipUtilities.IsZipEntryUrl(linkItem.Url))
        {
            System.Diagnostics.Debug.WriteLine($"[HandleLinkSelectionAsync] IS zip entry URL - calling HandleZipEntryAsync");
            await HandleZipEntryAsync(linkItem, linkNode, hideAllViewers, showDetailsViewers, setStatus);
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[HandleLinkSelectionAsync] NOT zip entry URL - calling HandleRegularLinkAsync");
        await HandleRegularLinkAsync(linkItem, linkNode, hideAllViewers, showDetailsViewers, showViewer, setStatus);
    }

    private async Task HandleEmptyUrlAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        hideAllViewers();
        _detailsViewService.ClearTabbedViewContent();
        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
        showDetailsViewers();
        ShowLinkHeaderWithBadge(linkItem, setStatus);
        setStatus("No URL specified for this link");
    }

    private async Task HandleZipEntryAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] Starting for: {linkItem.Title}");
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] URL: {linkItem.Url}");
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] IsDirectory: {linkItem.IsDirectory}");
            
            hideAllViewers();
            _detailsViewService.ClearTabbedViewContent();
            
            // Show link details in Summary tab
            await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
            
            // Show header
            ShowLinkHeaderWithBadge(linkItem, setStatus);
            
            // Show tabbed view
            showDetailsViewers();
            
            if (linkItem.IsDirectory)
            {
                setStatus($"Viewing zip folder: {linkItem.Title}");
            }
            else
            {
                setStatus($"Viewing from zip: {linkItem.Title}");
            }
            
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] Completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] Error: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[HandleZipEntryAsync] StackTrace: {ex.StackTrace}");
            setStatus($"Error loading zip entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows details for a zip entry file in the details panel.
    /// </summary>
    private async Task ShowZipEntryDetailsAsync(LinkItem linkItem, TreeViewNode? linkNode)
    {
        // Parse the zip entry URL to get file info
        var (zipPath, entryPath) = ZipUtilities.ParseZipEntryUrl(linkItem.Url);
        
        if (zipPath == null || entryPath == null)
            return;

        // Show basic file information in details panel
        // The DetailsViewService will be shown alongside the image viewer
        if (linkNode != null)
        {
            // Create a minimal save callback
            Func<Task> saveCallback = async () =>
            {
                var rootNode = GetRootCategoryNode(linkNode);
                if (rootNode != null)
                {
                    await _categoryService.SaveCategoryAsync(rootNode);
                }
            };

            // Show link details (will show file size, path, timestamps, etc.)
            await _detailsViewService.ShowLinkDetailsAsync(
                linkItem, 
                linkNode,
                async () => { }, // No catalog create for zip entries
                async () => { }, // No catalog refresh for zip entries
                null, // No archive refresh
                saveCallback);
        }
    }

    private async Task HandleRegularLinkAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<FileViewerType> showViewer, Action<string> setStatus)
    {
        try
        {
            bool isZipFile = IsZipFile(linkItem.Url);

            if (ShouldHandleAsDirectory(linkItem, isZipFile))
            {
                await HandleDirectoryOrZipAsync(linkItem, linkNode, isZipFile, hideAllViewers, showDetailsViewers, setStatus);
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                await HandleUriAsync(uri, linkItem, linkNode, hideAllViewers, showDetailsViewers, showViewer, setStatus);
            }
        }
        catch (Exception ex)
        {
            await HandleSelectionErrorAsync(linkItem, linkNode, ex, hideAllViewers, showDetailsViewers, setStatus);
        }
    }

    private async Task HandleDirectoryOrZipAsync(LinkItem linkItem, TreeViewNode? linkNode, bool isZipFile, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        hideAllViewers();
        _detailsViewService.ClearTabbedViewContent();

        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);

        // Only add "Open in Explorer" button for real directories, not zip files
        if (!isZipFile)
        {
            await _detailsViewService.AddOpenInExplorerButtonAsync(linkItem.Url);
        }

        showDetailsViewers();
        ShowLinkHeaderWithBadge(linkItem, setStatus);
        
        setStatus(isZipFile
            ? $"Viewing zip archive: {linkItem.Title}"
            : $"Viewing directory: {linkItem.Title}");
    }

    private async Task HandleUriAsync(Uri uri, LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<FileViewerType> showViewer, Action<string> setStatus)
    {
        System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] START - URI: {uri}, IsFile: {uri.IsFile}");
        
        hideAllViewers();
        _detailsViewService.ClearTabbedViewContent();
        
        // Show link details in Summary tab (but don't load content - we'll handle that below)
        await ShowLinkDetailsInSummaryOnly(linkItem, linkNode, setStatus);
        
        // Show URL status banner if not accessible or if redirect detected (from previous check)
        if (linkItem.UrlStatus != UrlStatus.Unknown && linkItem.UrlStatus != UrlStatus.Accessible)
        {
            _detailsViewService.ShowUrlStatusBanner(linkItem);
        }
        else if (linkItem.HasRedirect)
        {
            _detailsViewService.ShowUrlStatusBanner(linkItem);
        }
        
        // Show header
        ShowLinkHeaderWithBadge(linkItem, setStatus);
        
        // For web URLs (not file:// URLs), load content and check for redirects
        if (!uri.IsFile)
        {
            // Update URL text box
            if (_urlTextBox != null)
            {
                _urlTextBox.Text = uri.ToString();
            }
            
            // Show tabbed details view (Summary + Content tabs) WITH URL bar FIRST
            showDetailsViewers();
            
            // Also show the URL bar panel for web navigation
            showViewer(FileViewerType.Web);
            
            // Now load URL in the Content tab's WebView
            System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] Loading URL in ContentTabWeb: {uri}");
            await _detailsViewService.ShowContentWebAsync(uri.ToString());
            
            setStatus($"Loaded: {uri}");
            
            // Check URL status synchronously for redirect detection
            // This allows us to show the redirect button immediately
            if (_urlStateCheckerService != null && linkNode != null)
            {
                bool shouldCheck = !linkItem.UrlLastChecked.HasValue ||
                                   (DateTime.Now - linkItem.UrlLastChecked.Value).TotalMinutes > 5;
                
                if (shouldCheck)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] Checking URL for redirect detection...");
                    setStatus($"Checking URL status...");
                    
                    try
                    {
                        var result = await _urlStateCheckerService.CheckUrlWithRedirectAsync(uri.ToString());
                        
                        System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] Check result: Status={result.Status}, RedirectDetected={result.RedirectDetected}, RedirectUrl={result.RedirectUrl ?? "(null)"}");
                        
                        // Update link item with results
                        linkItem.UrlStatus = result.Status;
                        linkItem.UrlStatusMessage = result.Message;
                        linkItem.UrlLastChecked = DateTime.Now;
                        
                        if (result.RedirectDetected && !string.IsNullOrEmpty(result.RedirectUrl))
                        {
                            linkItem.RedirectUrl = result.RedirectUrl;
                            System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] REDIRECT DETECTED: {uri} -> {result.RedirectUrl}");
                            
                            // Refresh header to show redirect button
                            ShowLinkHeaderWithBadge(linkItem, setStatus);
                            
                            // Show URL status banner for redirect
                            _detailsViewService.ShowUrlStatusBanner(linkItem);
                        }
                        else
                        {
                            linkItem.RedirectUrl = null;
                        }
                        
                        // Save the category to persist changes
                        var rootNode = GetRootCategoryNode(linkNode);
                        if (rootNode != null)
                        {
                            await _categoryService.SaveCategoryAsync(rootNode);
                        }
                        
                        setStatus($"Loaded: {uri}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] URL check failed: {ex.Message}");
                        setStatus($"Loaded: {uri}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] Skipping URL check - last checked {linkItem.UrlLastChecked}");
                }
            }
        }
        else
        {
            // For file:// URLs, use tabbed details view without URL bar
            // Load file content
            await _detailsViewService.LoadContentForLinkAsync(linkItem);
            showDetailsViewers();
            setStatus($"Viewing file: {linkItem.Title}");
        }
        
        System.Diagnostics.Debug.WriteLine($"[HandleUriAsync] END");
    }
    
    /// <summary>
    /// Shows link details in Summary tab WITHOUT loading content in the Content tab.
    /// Used when we want to handle content loading separately (e.g., for web URLs).
    /// </summary>
    private async Task ShowLinkDetailsInSummaryOnly(LinkItem linkItem, TreeViewNode? linkNode, Action<string> setStatus)
    {
        if (linkNode != null)
        {
            // Only pass RefreshArchiveFromManifestAsync for zip files
            bool isZipFile = IsZipFile(linkItem.Url);
            
            // Create save callback
            Func<Task> saveCallback = async () =>
            {
                var rootNode = GetRootCategoryNode(linkNode);
                if (rootNode != null)
                {
                    await _categoryService.SaveCategoryAsync(rootNode);
                }
            };
            
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, linkNode,
                async () => await _catalogService.CreateCatalogAsync(linkItem, linkNode),
                async () => await _catalogService.RefreshCatalogAsync(linkItem, linkNode),
                isZipFile ? async () => await _catalogService.RefreshArchiveFromManifestAsync(linkItem, linkNode) : null,
                saveCallback);
        }
        else
        {
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { }, null, null);
        }
        
        // NOTE: We intentionally do NOT call LoadContentForLinkAsync here
        // The caller will handle content loading separately
    }

    private async Task HandleSelectionErrorAsync(LinkItem linkItem, TreeViewNode? linkNode, Exception ex, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        // Special handling for file not found during operations
        if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            // Check if this is a temporary state (URL is empty means operation in progress)
            if (string.IsNullOrEmpty(linkItem.Url))
            {
                setStatus("Operation in progress - please wait...");
            }
            else
            {
                setStatus($"Error: File or directory not found - {linkItem.Url}");
            }
        }
        else
        {
            setStatus($"Error: {ex.Message}");
        }
        
        hideAllViewers();
        _detailsViewService.ClearTabbedViewContent();

        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
        
        showDetailsViewers();
        ShowLinkHeaderWithBadge(linkItem, setStatus);
    }

    private bool IsZipFile(string url)
    {
        return url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
    }

    private bool ShouldHandleAsDirectory(LinkItem linkItem, bool isZipFile)
    {
        return (linkItem.IsDirectory && Directory.Exists(linkItem.Url)) || isZipFile;
    }

    private void ShowLinkHeaderWithBadge(LinkItem linkItem, Action<string> setStatus)
    {
        bool showLinkBadge = linkItem.IsDirectory && linkItem.FolderType == FolderLinkType.LinkOnly;
        
        // Get file size and timestamps - use stored values or try to get from file system
        ulong? fileSize = linkItem.FileSize;
        DateTime? createdDate = linkItem.CreatedDate;
        DateTime? modifiedDate = linkItem.ModifiedDate;
        
        if (!string.IsNullOrEmpty(linkItem.Url))
        {
            try
            {
                // Check if it's a zip entry URL
                if (ZipUtilities.IsZipEntryUrl(linkItem.Url))
                {
                    // For zip entries, try to get info from the archive
                    var (zipPath, entryPath) = ZipUtilities.ParseZipEntryUrl(linkItem.Url);
                    if (zipPath != null && entryPath != null && File.Exists(zipPath))
                    {
                        var entryInfo = ZipUtilities.GetEntryInfo(zipPath, entryPath);
                        if (entryInfo.exists)
                        {
                            if (!fileSize.HasValue || fileSize == 0)
                            {
                                fileSize = (ulong)entryInfo.size;
                            }
                            if (entryInfo.modified != DateTime.MinValue)
                            {
                                modifiedDate = entryInfo.modified;
                            }
                        }
                    }
                }
                else if (linkItem.IsDirectory)
                {
                    // For directories, get directory info
                    if (Directory.Exists(linkItem.Url))
                    {
                        var dirInfo = new DirectoryInfo(linkItem.Url);
                        if (createdDate == default || createdDate == DateTime.MinValue)
                        {
                            createdDate = dirInfo.CreationTime;
                        }
                        if (modifiedDate == default || modifiedDate == DateTime.MinValue)
                        {
                            modifiedDate = dirInfo.LastWriteTime;
                        }
                        // For directories, we can show the item count instead of size
                        // fileSize remains null for directories
                    }
                }
                else if (File.Exists(linkItem.Url))
                {
                    // For regular files (including zip archives), get file info
                    var fileInfo = new FileInfo(linkItem.Url);
                    if (!fileSize.HasValue || fileSize == 0)
                    {
                        fileSize = (ulong)fileInfo.Length;
                    }
                    if (createdDate == default || createdDate == DateTime.MinValue)
                    {
                        createdDate = fileInfo.CreationTime;
                    }
                    if (modifiedDate == default || modifiedDate == DateTime.MinValue)
                    {
                        modifiedDate = fileInfo.LastWriteTime;
                    }
                }
            }
            catch
            {
                // Ignore errors getting file info
            }
        }
        
        _detailsViewService.ShowLinkHeader(
            linkItem.Title, 
            linkItem.Description, 
            linkItem.GetIcon(), 
            showLinkBadge,
            fileSize,
            createdDate,
            modifiedDate,
            linkItem);
    }

    private async Task ShowLinkDetailsWithCatalogActions(LinkItem linkItem, TreeViewNode? linkNode, Action<string> setStatus)
    {
        if (linkNode != null)
        {
            // Only pass RefreshArchiveFromManifestAsync for zip files
            bool isZipFile = IsZipFile(linkItem.Url);
            
            // Create save callback
            Func<Task> saveCallback = async () =>
            {
                var rootNode = GetRootCategoryNode(linkNode);
                if (rootNode != null)
                {
                    await _categoryService.SaveCategoryAsync(rootNode);
                }
            };
            
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, linkNode,
                async () => await _catalogService.CreateCatalogAsync(linkItem, linkNode),
                async () => await _catalogService.RefreshCatalogAsync(linkItem, linkNode),
                isZipFile ? async () => await _catalogService.RefreshArchiveFromManifestAsync(linkItem, linkNode) : null,
                saveCallback);
        }
        else
        {
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { }, null, null);
        }
        
        // Load content in the Content tab
        await _detailsViewService.LoadContentForLinkAsync(linkItem);
    }

    private TreeViewNode? GetRootCategoryNode(TreeViewNode node)
    {
        var current = node;
        int safetyCounter = 0;
        const int maxDepth = 100; // Prevent infinite loops
        
        while (current?.Parent != null && safetyCounter < maxDepth)
        {
            if (current.Content is CategoryItem)
            {
                var parent = current.Parent;
                if (parent.Content is not CategoryItem)
                {
                    return current;
                }
                current = parent;
            }
            else
            {
                current = current.Parent;
            }
            safetyCounter++;
        }
        
        // Safety check: current should not be null
        if (current == null)
        {
            return null; // Return null instead of crashing
        }
        
        // If we hit max depth, return null
        if (safetyCounter >= maxDepth)
        {
            return null;
        }
        
        return current.Content is CategoryItem ? current : null;
    }

    private async Task<string?> GetCategoryPasswordAsync(CategoryItem category)
    {
        if (category.PasswordProtection == PasswordProtectionType.GlobalPassword)
        {
            var globalPassword = _categoryService.GetCachedGlobalPassword();
            if (!string.IsNullOrEmpty(globalPassword))
            {
                return globalPassword;
            }
            return null;
        }
        else if (category.PasswordProtection == PasswordProtectionType.OwnPassword)
        {
            return null;
        }

        return null;
    }
}