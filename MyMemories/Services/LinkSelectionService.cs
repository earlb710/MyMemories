using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.Diagnostics;
using System.IO;
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

    public LinkSelectionService(
        DetailsViewService detailsViewService,
        FileViewerService fileViewerService,
        TreeViewService treeViewService,
        CatalogService catalogService,
        FileLauncherService fileLauncherService)
    {
        _detailsViewService = detailsViewService;
        _fileViewerService = fileViewerService;
        _treeViewService = treeViewService;
        _catalogService = catalogService;
        _fileLauncherService = fileLauncherService;
    }

    // CRITICAL FIX: Add TreeViewNode parameter
    public async Task HandleLinkSelectionAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<string> setStatus)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            await HandleEmptyUrlAsync(linkItem, linkNode, hideAllViewers, setStatus);
            return;
        }

        if (ZipUtilities.IsZipEntryUrl(linkItem.Url))
        {
            await HandleZipEntryAsync(linkItem, linkNode, hideAllViewers, setStatus);
            return;
        }

        await HandleRegularLinkAsync(linkItem, linkNode, hideAllViewers, setStatus);
    }

    private async Task HandleEmptyUrlAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<string> setStatus)
    {
        Debug.WriteLine($"[HandleLinkSelectionAsync] Link '{linkItem.Title}' has no URL");
        hideAllViewers();
        
        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
        
        ShowLinkHeaderWithBadge(linkItem, setStatus);
        setStatus("No URL specified for this link");
    }

    private async Task HandleZipEntryAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<string> setStatus)
    {
        try
        {
            if (!linkItem.IsDirectory)
            {
                hideAllViewers();
                var result = await _fileViewerService.LoadZipEntryAsync(linkItem.Url);
                
                ShowLinkHeaderWithBadge(linkItem, setStatus);
                setStatus($"Viewing from zip: {linkItem.Title}");
                Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded zip entry: {linkItem.Url}");
            }
        }
        catch (Exception ex)
        {
            setStatus($"Error loading zip entry: {ex.Message}");
            Debug.WriteLine($"[HandleLinkSelectionAsync] Error with zip entry: {ex}");
        }
    }

    private async Task HandleRegularLinkAsync(LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<string> setStatus)
    {
        try
        {
            bool isZipFile = IsZipFile(linkItem.Url);
            Debug.WriteLine($"[HandleLinkSelectionAsync] Link: '{linkItem.Title}', IsDirectory: {linkItem.IsDirectory}, IsZip: {isZipFile}");

            if (ShouldHandleAsDirectory(linkItem, isZipFile))
            {
                await HandleDirectoryOrZipAsync(linkItem, linkNode, isZipFile, hideAllViewers, setStatus);
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                await HandleUriAsync(uri, linkItem, linkNode, hideAllViewers, setStatus);
            }
        }
        catch (Exception ex)
        {
            await HandleSelectionErrorAsync(linkItem, linkNode, ex, hideAllViewers, setStatus);
        }
    }

    private async Task HandleDirectoryOrZipAsync(LinkItem linkItem, TreeViewNode? linkNode, bool isZipFile, Action hideAllViewers, Action<string> setStatus)
    {
        hideAllViewers();

        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);

        // Only add "Open in Explorer" button for real directories, not zip files
        if (!isZipFile)
        {
            await _detailsViewService.AddOpenInExplorerButtonAsync(linkItem.Url);
        }

        ShowLinkHeaderWithBadge(linkItem, setStatus);
        
        setStatus(isZipFile
            ? $"Viewing zip archive: {linkItem.Title}"
            : $"Viewing directory: {linkItem.Title}");
    }

    private async Task HandleUriAsync(Uri uri, LinkItem linkItem, TreeViewNode? linkNode, Action hideAllViewers, Action<string> setStatus)
    {
        if (uri.IsFile)
        {
            var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
            // Load file via file viewer service
            Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded file: {linkItem.Url}");
        }
        else
        {
            hideAllViewers();
            await _fileViewerService.LoadUrlAsync(uri);
            
            ShowLinkHeaderWithBadge(linkItem, setStatus);
            setStatus($"Loaded: {uri}");
            Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded URL: {uri}");
        }
    }

    private async Task HandleSelectionErrorAsync(LinkItem linkItem, TreeViewNode? linkNode, Exception ex, Action hideAllViewers, Action<string> setStatus)
    {
        var errorMsg = $"Error: {ex.Message}";
        Debug.WriteLine($"[HandleLinkSelectionAsync] Exception for '{linkItem.Title}': {ex}");
        setStatus(errorMsg);
        hideAllViewers();

        await ShowLinkDetailsWithCatalogActions(linkItem, linkNode, setStatus);
        
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
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, linkNode,
                async () => await _catalogService.CreateCatalogAsync(linkItem, linkNode),
                async () => await _catalogService.RefreshCatalogAsync(linkItem, linkNode),
                async () => await _catalogService.RefreshArchiveFromManifestAsync(linkItem, linkNode));
        }
        else
        {
            await _detailsViewService.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
        }
    }
}