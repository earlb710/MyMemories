using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyMemories.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace MyMemories;

public sealed partial class MainWindow
{
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

    private async void LinksTreeView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        var treeViewItem = FindParent<TreeViewItem>(element);
        if (treeViewItem?.Content is not TreeViewNode node)
            return;

        if (node.Content is CategoryItem category)
        {
            await EditCategoryAsync(category, node);
            e.Handled = true;
        }
        else if (node.Content is LinkItem linkItem)
        {
            // Check if this is a zip entry (URL contains "::")
            if (linkItem.Url.Contains("::"))
            {
                // If it's a directory within the zip, just expand/collapse
                if (linkItem.IsDirectory)
                {
                    LinksTreeView.SelectedNode.IsExpanded = !LinksTreeView.SelectedNode.IsExpanded;
                }
                else
                {
                    // It's a file within the zip - extract and open it
                    await OpenZipEntryAsync(linkItem);
                }
            }
            else if (linkItem.IsDirectory)
            {
                // Expand/collapse regular directory
                LinksTreeView.SelectedNode.IsExpanded = !LinksTreeView.SelectedNode.IsExpanded;
            }
            else
            {
                // Open regular file
                await OpenFileAsync(linkItem.Url);
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// Opens a file using the default system application.
    /// </summary>
    private async Task OpenFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var errorMsg = $"File not found: {filePath}";
                Debug.WriteLine($"[OpenFileAsync] {errorMsg}");
                StatusText.Text = errorMsg;
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(filePath);
            await Launcher.LaunchFileAsync(file);
            StatusText.Text = $"Opened: {Path.GetFileName(filePath)}";
            Debug.WriteLine($"[OpenFileAsync] Successfully opened: {filePath}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening file: {ex.Message}";
            Debug.WriteLine($"[OpenFileAsync] Exception: {ex}");
            StatusText.Text = errorMsg;
        }
    }

    private async Task OpenLinkAsync(LinkItem linkItem)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            Debug.WriteLine("[OpenLinkAsync] Link has no URL");
            StatusText.Text = "Link has no URL to open";
            return;
        }

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
            {
                await Launcher.LaunchFolderPathAsync(linkItem.Url);
                StatusText.Text = $"Opened directory: {linkItem.Title}";
                Debug.WriteLine($"[OpenLinkAsync] Opened directory: {linkItem.Url}");
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await Launcher.LaunchFileAsync(file);
                    StatusText.Text = $"Opened file: {linkItem.Title}";
                    Debug.WriteLine($"[OpenLinkAsync] Opened file: {linkItem.Url}");
                }
                else
                {
                    await Launcher.LaunchUriAsync(uri);
                    StatusText.Text = $"Opened URL: {linkItem.Title}";
                    Debug.WriteLine($"[OpenLinkAsync] Opened URL: {uri}");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening link: {ex.Message}";
            Debug.WriteLine($"[OpenLinkAsync] Exception for '{linkItem.Title}': {ex}");
            StatusText.Text = errorMsg;
        }
    }

    private async Task HandleLinkSelectionAsync(LinkItem linkItem)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            Debug.WriteLine($"[HandleLinkSelectionAsync] Link '{linkItem.Title}' has no URL");
            HideAllViewers();
            var linkNode = FindLinkNode(linkItem);

            if (linkNode != null)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, linkNode,
                    async () => await CreateCatalogAsync(linkItem, linkNode),
                    async () => await RefreshCatalogAsync(linkItem, linkNode));
            }
            else
            {
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
            }

            DetailsViewerScroll.Visibility = Visibility.Visible;
            _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
            HeaderViewerScroll.Visibility = Visibility.Visible;
            StatusText.Text = "No URL specified for this link";
            return;
        }

        // Check if it's a zip entry URL
        if (ZipUtilities.IsZipEntryUrl(linkItem.Url))
        {
            try
            {
                if (!linkItem.IsDirectory)
                {
                    HideAllViewers();
                    var result = await _fileViewerService!.LoadZipEntryAsync(linkItem.Url);
                    ShowViewer(result.ViewerType);
                    
                    _detailsViewService!.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
                    HeaderViewerScroll.Visibility = Visibility.Visible;
                    
                    StatusText.Text = $"Viewing from zip: {linkItem.Title}";
                    Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded zip entry: {linkItem.Url}");
                    return;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading zip entry: {ex.Message}";
                Debug.WriteLine($"[HandleLinkSelectionAsync] Error with zip entry: {ex}");
            }
        }

        try
        {
            // Check if it's a zip file (treated as directory but is actually a file)
            bool isZipFile = linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                           File.Exists(linkItem.Url);

            Debug.WriteLine($"[HandleLinkSelectionAsync] Link: '{linkItem.Title}', IsDirectory: {linkItem.IsDirectory}, IsZip: {isZipFile}, URL: {linkItem.Url}");

            // Handle as directory/catalog if:
            // 1. It's marked as directory AND is a real directory
            // 2. It's a zip file (regardless of IsDirectory flag - we auto-detect and treat as catalog)
            if ((linkItem.IsDirectory && Directory.Exists(linkItem.Url)) || isZipFile)
            {
                HideAllViewers();
                var linkNode = FindLinkNode(linkItem);

                if (linkNode != null)
                {
                    await _detailsViewService!.ShowLinkDetailsAsync(linkItem, linkNode,
                        async () => await CreateCatalogAsync(linkItem, linkNode),
                        async () => await RefreshCatalogAsync(linkItem, linkNode));
                }
                else
                {
                    await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
                }

                // Only add "Open in Explorer" button for real directories, not zip files
                if (!isZipFile)
                {
                    await _detailsViewService.AddOpenInExplorerButtonAsync(linkItem.Url);
                }

                DetailsViewerScroll.Visibility = Visibility.Visible;
                _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
                HeaderViewerScroll.Visibility = Visibility.Visible;
                StatusText.Text = isZipFile
                    ? $"Viewing zip archive: {linkItem.Title}"
                    : $"Viewing directory: {linkItem.Title}";
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await LoadFileAsync(file, linkItem.Description);
                    Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded file: {linkItem.Url}");
                }
                else
                {
                    HideAllViewers();
                    await _fileViewerService!.LoadUrlAsync(uri);
                    WebViewer.Visibility = Visibility.Visible;
                    _detailsViewService!.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
                    HeaderViewerScroll.Visibility = Visibility.Visible;
                    StatusText.Text = $"Loaded: {uri}";
                    Debug.WriteLine($"[HandleLinkSelectionAsync] Loaded URL: {uri}");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error: {ex.Message}";
            Debug.WriteLine($"[HandleLinkSelectionAsync] Exception for '{linkItem.Title}': {ex}");
            StatusText.Text = errorMsg;
            HideAllViewers();

            var linkNode = FindLinkNode(linkItem);
            if (linkNode != null)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, linkNode,
                    async () => await CreateCatalogAsync(linkItem, linkNode),
                    async () => await RefreshCatalogAsync(linkItem, linkNode));
            }
            else
            {
                await _detailsViewService!.ShowLinkDetailsAsync(linkItem, null, async () => { }, async () => { });
            }

            DetailsViewerScroll.Visibility = Visibility.Visible;
            _detailsViewService.ShowLinkHeader(linkItem.Title, linkItem.Description, linkItem.GetIcon());
            HeaderViewerScroll.Visibility = Visibility.Visible;
        }
    }

    private TreeViewNode? FindLinkNode(LinkItem linkItem)
    {
        if (LinksTreeView.SelectedNode?.Content is LinkItem selectedLink &&
            selectedLink.Title == linkItem.Title &&
            selectedLink.Url == linkItem.Url)
        {
            return LinksTreeView.SelectedNode;
        }

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
        try
        {
            // Check if it's a zip file
            bool isZipFile = linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                           File.Exists(linkItem.Url);

            Debug.WriteLine($"[CreateCatalogAsync] Creating catalog for '{linkItem.Title}', IsZip: {isZipFile}");
            StatusText.Text = isZipFile ? "Cataloging zip file..." : "Creating catalog...";

            var tempCreatingItem = new LinkItem
            {
                Title = isZipFile ? "Cataloging zip..." : "Creating catalog...",
                Description = "Please wait while scanning " + (isZipFile ? "zip archive" : "directory"),
                IsDirectory = false,
                IsCatalogEntry = false
            };

            var tempNode = new TreeViewNode { Content = tempCreatingItem };
            linkNode.Children.Add(tempNode);
            linkNode.IsExpanded = true;

            if (isZipFile)
            {
                // Catalog zip file
                await CatalogZipFileAsync(linkItem, linkNode);
                linkNode.Children.Remove(tempNode);
            }
            else
            {
                // Catalog normal directory
                var catalogEntries = await _categoryService!.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);
                linkNode.Children.Remove(tempNode);

                // Recursively add catalog entries with their subdirectories populated
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
            }

            linkItem.LastCatalogUpdate = DateTime.Now;
            _categoryService!.UpdateCatalogFileCount(linkNode);
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);

            refreshedNode.IsExpanded = true;
            await _detailsViewService!.ShowLinkDetailsAsync(linkItem, refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode));

            var count = refreshedNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry);
            StatusText.Text = $"Created catalog with {count} entries";
            Debug.WriteLine($"[CreateCatalogAsync] Successfully created catalog for '{linkItem.Title}' with {count} entries");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error creating catalog: {ex.Message}";
            Debug.WriteLine($"[CreateCatalogAsync] Exception for '{linkItem.Title}': {ex}");
            StatusText.Text = errorMsg;
        }
    }

    private async Task RefreshCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        bool wasExpanded = linkNode.IsExpanded;

        try
        {
            // Check if it's a zip file
            bool isZipFile = linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                           File.Exists(linkItem.Url);

            Debug.WriteLine($"[RefreshCatalogAsync] Refreshing catalog for '{linkItem.Title}', IsZip: {isZipFile}");
            StatusText.Text = isZipFile ? "Refreshing zip catalog..." : "Refreshing catalog...";

            _categoryService!.RemoveCatalogEntries(linkNode);

            var tempRefreshingItem = new LinkItem
            {
                Title = isZipFile ? "Refreshing zip catalog..." : "Refreshing catalog...",
                Description = "Please wait while the catalog is being updated",
                IsDirectory = false,
                IsCatalogEntry = false
            };

            var tempNode = new TreeViewNode { Content = tempRefreshingItem };
            linkNode.Children.Add(tempNode);
            linkNode.IsExpanded = true;

            if (isZipFile)
            {
                // Refresh zip catalog
                await CatalogZipFileAsync(linkItem, linkNode);
                linkNode.Children.Remove(tempNode);
            }
            else
            {
                // Refresh normal directory catalog
                var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);
                linkNode.Children.Remove(tempNode);

                // Recursively add catalog entries with their subdirectories populated
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
            }

            linkItem.LastCatalogUpdate = DateTime.Now;
            _categoryService.UpdateCatalogFileCount(linkNode);

            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);

            await _detailsViewService!.ShowLinkDetailsAsync(linkItem, refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode));

            var count = refreshedNode.Children.Count(c => c.Content is LinkItem link && link.IsCatalogEntry);
            StatusText.Text = $"Refreshed catalog with {count} entries";
            Debug.WriteLine($"[RefreshCatalogAsync] Successfully refreshed catalog for '{linkItem.Title}' with {count} entries");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error refreshing catalog: {ex.Message}";
            Debug.WriteLine($"[RefreshCatalogAsync] Exception for '{linkItem.Title}': {ex}");
            StatusText.Text = errorMsg;
        }
        finally
        {
            linkNode.IsExpanded = wasExpanded;
        }
    }

    /// <summary>
    /// Recursively populates a subdirectory with all its files and nested subdirectories.
    /// </summary>
    private async Task PopulateSubdirectoryAsync(TreeViewNode subdirNode, LinkItem subdirItem, string categoryPath)
    {
        try
        {
            // Get all contents of this subdirectory
            var subCatalogEntries = await _categoryService!.CreateSubdirectoryCatalogEntriesAsync(subdirItem.Url, categoryPath);

            foreach (var subEntry in subCatalogEntries)
            {
                var subEntryNode = new TreeViewNode { Content = subEntry };

                // If this is also a subdirectory, recursively populate it too
                if (subEntry.IsDirectory)
                {
                    await PopulateSubdirectoryAsync(subEntryNode, subEntry, categoryPath);
                }

                subdirNode.Children.Add(subEntryNode);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopulateSubdirectoryAsync] Exception for '{subdirItem.Title}': {ex}");
            // Don't throw - allow parent operation to continue
        }
    }

    /// <summary>
    /// Extracts and opens a file from within a zip archive.
    /// </summary>
    private async Task OpenZipEntryAsync(LinkItem zipEntry)
    {
        try
        {
            var parts = zipEntry.Url.Split("::", 2);
            if (parts.Length != 2)
            {
                Debug.WriteLine($"[OpenZipEntryAsync] Invalid zip entry URL format: {zipEntry.Url}");
                return;
            }

            var zipPath = parts[0];
            var entryPath = parts[1];

            Debug.WriteLine($"[OpenZipEntryAsync] Opening zip entry: {entryPath} from {zipPath}");

            if (!File.Exists(zipPath))
            {
                var errorMsg = "Zip file not found";
                Debug.WriteLine($"[OpenZipEntryAsync] {errorMsg}: {zipPath}");
                StatusText.Text = errorMsg;
                return;
            }

            // Create temp directory for extraction
            var tempDir = Path.Combine(Path.GetTempPath(), "MyMemories", Path.GetFileNameWithoutExtension(zipPath));
            Directory.CreateDirectory(tempDir);

            var extractedPath = Path.Combine(tempDir, entryPath.Replace('/', Path.DirectorySeparatorChar));

            // Extract the specific file
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entry = archive.GetEntry(entryPath);
                if (entry != null)
                {
                    // Ensure directory exists
                    var extractedDir = Path.GetDirectoryName(extractedPath);
                    if (!string.IsNullOrEmpty(extractedDir))
                    {
                        Directory.CreateDirectory(extractedDir);
                    }

                    entry.ExtractToFile(extractedPath, true);

                    // Open the extracted file
                    await OpenFileAsync(extractedPath);
                    StatusText.Text = $"Opened file from zip: {zipEntry.Title}";
                    Debug.WriteLine($"[OpenZipEntryAsync] Successfully extracted and opened: {entryPath}");
                }
                else
                {
                    Debug.WriteLine($"[OpenZipEntryAsync] Entry not found in archive: {entryPath}");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening zip entry: {ex.Message}";
            Debug.WriteLine($"[OpenZipEntryAsync] Exception for '{zipEntry.Title}': {ex}");
            StatusText.Text = errorMsg;
        }
    }
}