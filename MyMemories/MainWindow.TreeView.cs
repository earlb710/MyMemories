using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
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

    private async void LinksTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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
                await Launcher.LaunchFolderPathAsync(linkItem.Url);
                StatusText.Text = $"Opened directory: {linkItem.Title}";
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await Launcher.LaunchFileAsync(file);
                    StatusText.Text = $"Opened file: {linkItem.Title}";
                }
                else
                {
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

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
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
            StatusText.Text = "Creating catalog...";

            var tempCreatingItem = new LinkItem
            {
                Title = "Creating catalog...",
                Description = "Please wait while scanning directory",
                IsDirectory = false,
                IsCatalogEntry = false
            };
            
            var tempNode = new TreeViewNode { Content = tempCreatingItem };
            linkNode.Children.Add(tempNode);
            linkNode.IsExpanded = true;

            var catalogEntries = await _categoryService!.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);
            linkNode.Children.Remove(tempNode);

            linkItem.LastCatalogUpdate = DateTime.Now;

            foreach (var entry in catalogEntries)
            {
                var entryNode = new TreeViewNode { Content = entry };
                linkNode.Children.Add(entryNode);
            }

            _categoryService.UpdateCatalogFileCount(linkNode);
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);

            refreshedNode.IsExpanded = true;
            await _detailsViewService!.ShowLinkDetailsAsync(linkItem, refreshedNode,
                async () => await CreateCatalogAsync(linkItem, refreshedNode),
                async () => await RefreshCatalogAsync(linkItem, refreshedNode));

            StatusText.Text = $"Created catalog with {catalogEntries.Count} entries";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error creating catalog: {ex.Message}";
        }
    }

    private async Task RefreshCatalogAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        bool wasExpanded = linkNode.IsExpanded;
        
        try
        {
            StatusText.Text = "Refreshing catalog...";

            _categoryService!.RemoveCatalogEntries(linkNode);

            var tempRefreshingItem = new LinkItem
            {
                Title = "Refreshing catalog...",
                Description = "Please wait while the catalog is being updated",
                IsDirectory = false,
                IsCatalogEntry = false
            };
            
            var tempNode = new TreeViewNode { Content = tempRefreshingItem };
            linkNode.Children.Add(tempNode);
            linkNode.IsExpanded = true;

            var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(linkItem.Url, linkItem.CategoryPath);
            linkNode.Children.Remove(tempNode);

            linkItem.LastCatalogUpdate = DateTime.Now;

            foreach (var entry in catalogEntries)
            {
                var entryNode = new TreeViewNode { Content = entry };
                linkNode.Children.Add(entryNode);
            }

            _categoryService.UpdateCatalogFileCount(linkNode);
            
            // FIX: Capture the NEW node reference returned by RefreshLinkNode
            var refreshedNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);

            // FIX: Use the NEW refreshed node to find root and save
            var rootNode = GetRootCategoryNode(refreshedNode);
            await _categoryService.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Refreshed catalog with {catalogEntries.Count} entries";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error refreshing catalog: {ex.Message}";
        }
        finally
        {
            // Note: wasExpanded is tracked on the OLD node reference
            // The refreshed node will maintain its expansion state
            linkNode.IsExpanded = wasExpanded;
        }
    }
}