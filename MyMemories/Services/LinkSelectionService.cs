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
    private TextBox? _urlTextBox;

    public LinkSelectionService(
        DetailsViewService detailsViewService,
        FileViewerService fileViewerService,
        TreeViewService treeViewService,
        CatalogService catalogService,
        FileLauncherService fileLauncherService,
        CategoryService categoryService)
    {
        _detailsViewService = detailsViewService;
        _fileViewerService = fileViewerService;
        _treeViewService = treeViewService;
        _catalogService = catalogService;
        _fileLauncherService = fileLauncherService;
        _categoryService = categoryService;
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
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            await HandleEmptyUrlAsync(linkItem, linkNode, hideAllViewers, showDetailsViewers, setStatus);
            return;
        }

        if (ZipUtilities.IsZipEntryUrl(linkItem.Url))
        {
            await HandleZipEntryAsync(linkItem, linkNode, hideAllViewers, showViewer, setStatus);
            return;
        }

        await HandleRegularLinkAsync(linkItem, linkNode, hideAllViewers, showDetailsViewers, showViewer, setStatus);
    }

    private async Task HandleEmptyUrlAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        hideAllViewers();
        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
        showDetailsViewers();
        ShowLinkHeaderWithBadge(linkItem, setStatus);
        setStatus("No URL specified for this link");
    }

    private async Task HandleZipEntryAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<FileViewerType> showViewer, Action<string> setStatus)
    {
        try
        {
            if (!linkItem.IsDirectory)
            {
                // Get the password from the root category
                string? password = null;
                if (linkNode != null)
                {
                    var rootCategoryNode = GetRootCategoryNode(linkNode);
                    var rootCategory = rootCategoryNode?.Content as CategoryItem;

                    if (rootCategory?.PasswordProtection != PasswordProtectionType.None)
                    {
                        password = await GetCategoryPasswordAsync(rootCategory);
                    }
                }

                hideAllViewers();
                var result = await _fileViewerService.LoadZipEntryAsync(linkItem.Url, password);

                // Show the appropriate viewer based on the file type
                showViewer(result.ViewerType);

                ShowLinkHeaderWithBadge(linkItem, setStatus);
                setStatus($"Viewing from zip: {linkItem.Title}");
            }
        }
        catch (Exception ex)
        {
            setStatus($"Error loading zip entry: {ex.Message}");
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
                await HandleUriAsync(uri, linkItem, linkNode, hideAllViewers, showViewer, setStatus);
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

    private async Task HandleUriAsync(Uri uri, LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<FileViewerType> showViewer, Action<string> setStatus)
    {
        if (uri.IsFile)
        {
            hideAllViewers();
            var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
            var result = await _fileViewerService.LoadFileAsync(file);
            
            // Show the appropriate viewer based on the file type
            showViewer(result.ViewerType);
            
            ShowLinkHeaderWithBadge(linkItem, setStatus);
            setStatus($"Viewing file: {linkItem.Title}");
        }
        else
        {
            hideAllViewers();
            await _fileViewerService.LoadUrlAsync(uri, _urlTextBox);
            
            // Show web viewer for URLs
            showViewer(FileViewerType.Web);
            
            ShowLinkHeaderWithBadge(linkItem, setStatus);
            setStatus($"Loaded: {uri}");
        }
    }

    private async Task HandleSelectionErrorAsync(LinkItem linkItem, TreeViewNode? linkNode, Exception ex, Action hideAllViewers, Action showDetailsViewers, Action<string> setStatus)
    {
        // Special handling for file not found during operations
        if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            // Check if this is a temporary state (URL is empty means operation in progress)
            if (string.IsNullOrEmpty(linkItem.Url))
            {
                setStatus("? Operation in progress - please wait...");
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
        _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon(), showLinkBadge);
    }

    private async Task ShowLinkDetailsWithCatalogActions(LinkItem linkItem, TreeViewNode? linkNode, Action<string> setStatus)
    {
        if (linkNode != null)
        {
            // Only pass RefreshArchiveFromManifestAsync for zip files
            bool isZipFile = IsZipFile(linkItem.Url);
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, linkNode,
                async () => await _catalogService.CreateCatalogAsync(linkItem, linkNode),
                async () => await _catalogService.RefreshCatalogAsync(linkItem, linkNode),
                isZipFile ? async () => await _catalogService.RefreshArchiveFromManifestAsync(linkItem, linkNode) : null);
        }
        else
        {
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { }, null);
        }
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