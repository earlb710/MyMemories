using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

public sealed partial class MainWindow
{
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

    /// <summary>
    /// Creates or refreshes catalog for a link (folder or zip file).
    /// </summary>
    private async Task CreateCatalogAsync(LinkItem link, TreeViewNode node)
    {
        if (!link.IsDirectory)
        {
            return;
        }

        try
        {
            // Check if it's a zip file (ends with .zip and exists as a file)
            bool isZipFile = link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                           File.Exists(link.Url);

            if (isZipFile)
            {
                // Catalog zip file
                StatusText.Text = $"Cataloging zip file '{link.Title}'...";

                // Remove existing catalog entries
                _categoryService!.RemoveCatalogEntries(node);

                // Create new catalog from zip contents
                await CatalogZipFileAsync(link, node);

                // Update timestamps
                link.LastCatalogUpdate = DateTime.Now;

                StatusText.Text = $"Cataloged zip file '{link.Title}'";
            }
            else if (Directory.Exists(link.Url))
            {
                // Catalog normal directory (existing behavior)
                StatusText.Text = $"Cataloging folder '{link.Title}'...";

                _categoryService!.RemoveCatalogEntries(node);
                var catalogEntries = await _categoryService.CreateCatalogEntriesAsync(link.Url, link.CategoryPath);

                link.LastCatalogUpdate = DateTime.Now;

                foreach (var entry in catalogEntries)
                {
                    var entryNode = new TreeViewNode { Content = entry };

                    if (entry.IsDirectory)
                    {
                        await PopulateSubdirectoryAsync(entryNode, entry, link.CategoryPath);
                    }

                    node.Children.Add(entryNode);
                }

                _categoryService.UpdateCatalogFileCount(node);
                StatusText.Text = $"Cataloged {catalogEntries.Count} items from '{link.Title}'";
            }

            // Refresh the node to update display
            var refreshedNode = _treeViewService!.RefreshLinkNode(node, link);

            // Save changes
            if (refreshedNode.Parent != null)
            {
                var rootNode = GetRootCategoryNode(refreshedNode.Parent);
                await _categoryService!.SaveCategoryAsync(rootNode);
            }

            // Update details view if this is the selected node
            if (LinksTreeView.SelectedNode == refreshedNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    refreshedNode,
                    async () => await CreateCatalogAsync(link, refreshedNode),
                    async () => await RefreshCatalogAsync(link, refreshedNode)
                );
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error cataloging: {ex.Message}";

            var errorDialog = new ContentDialog
            {
                Title = "Catalog Error",
                Content = $"Failed to create catalog:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    /// <summary>
    /// Refreshes catalog for a link (folder or zip file).
    /// </summary>
    private async Task RefreshCatalogAsync(LinkItem link, TreeViewNode node)
    {
        await CreateCatalogAsync(link, node); // Reuse the same logic
    }
}