using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MyMemories.Services.Details;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services;

/// <summary>
/// Facade service for displaying node details in the details panel.
/// Delegates to specialized builder classes for different content types.
/// </summary>
public class DetailsViewService
{
    private readonly StackPanel _detailsPanel;
    private StackPanel? _headerPanel;
    
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
    /// Note: For web URLs, use ShowLinkHeader with linkItem to show redirect button in header instead.
    /// </summary>
    public void ShowUrlStatusBanner(LinkItem linkItem)
    {
        // Only show error/not found banners in the details panel, not redirect banners
        // Redirect handling is now done via the header button
        if (linkItem.UrlStatus != UrlStatus.Unknown && linkItem.UrlStatus != UrlStatus.Accessible)
        {
            _urlStatusBuilder?.ShowUrlStatusBanner(linkItem);
        }
        // Don't show redirect banner here - it's handled in the header
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public async Task<Button?> ShowCategoryDetailsAsync(CategoryItem category, TreeViewNode node, 
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null, Func<Task>? onSyncBookmarks = null)
    {
        return await _categoryBuilder!.ShowCategoryDetailsAsync(category, node, onRefreshBookmarks, onRefreshUrlState, onSyncBookmarks);
    }

    /// <summary>
    /// Shows link details with file information and catalog buttons for directories.
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
    public async Task<Button?> AddOpenInExplorerButtonAsync(string path)
    {
        return await _linkBuilder!.AddOpenInExplorerButtonAsync(path);
    }
}