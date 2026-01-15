using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services.Interfaces;

/// <summary>
/// Interface for the DetailsViewService that handles displaying node details in the details panel.
/// </summary>
public interface IDetailsViewService
{
    /// <summary>
    /// Event raised when the user requests to update a URL to its redirect target.
    /// </summary>
    event Action<LinkItem>? UpdateUrlFromRedirectRequested;

    /// <summary>
    /// Sets the header panel for displaying headers.
    /// </summary>
    void SetHeaderPanel(StackPanel headerPanel);

    /// <summary>
    /// Sets up callbacks for line number display in main content viewer.
    /// </summary>
    void SetLineNumberCallbacks(Action<string> showLineNumbers, Action hideLineNumbers, Action? setupScrollSync = null);

    /// <summary>
    /// Sets up the tabbed details view with Summary and Content tabs.
    /// </summary>
    void SetupTabbedView(
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
        TextBlock contentTabNoContentText);

    /// <summary>
    /// Clears all content from the tabbed view but preserves tab selection.
    /// </summary>
    void ClearTabbedView();

    /// <summary>
    /// Clears content from both tabs but preserves tab selection.
    /// </summary>
    void ClearTabbedViewContent();

    /// <summary>
    /// Shows the tabbed view and selects the Summary tab.
    /// </summary>
    void ShowTabbedView();

    /// <summary>
    /// Shows text content in the Content tab.
    /// </summary>
    void ShowContentText(string content);

    /// <summary>
    /// Shows text content with line numbers in the Content tab.
    /// </summary>
    void ShowContentTextWithLineNumbers(string content);

    /// <summary>
    /// Shows an image in the Content tab.
    /// </summary>
    Task ShowContentImageAsync(string filePath);

    /// <summary>
    /// Shows web content (HTML, PDF, URL) in the Content tab.
    /// </summary>
    Task ShowContentWebAsync(string urlOrPath);

    /// <summary>
    /// Shows a message in the Content tab.
    /// </summary>
    void ShowContentMessage(string message);

    /// <summary>
    /// Loads content for a link item into the Content tab.
    /// </summary>
    Task LoadContentForLinkAsync(LinkItem linkItem);

    /// <summary>
    /// Shows file header information with name, description, and size.
    /// </summary>
    Task ShowFileHeaderAsync(string fileName, string? description, StorageFile file, BitmapImage? bitmap = null);

    /// <summary>
    /// Shows category header in the header panel with icon on the left.
    /// </summary>
    void ShowCategoryHeader(string categoryName, string? description, string icon, CategoryItem? category = null);

    /// <summary>
    /// Shows link header in the header panel with icon on the left and optional link badge.
    /// </summary>
    void ShowLinkHeader(string linkTitle, string? description, string icon, bool showLinkBadge = false,
        ulong? fileSize = null, DateTime? createdDate = null, DateTime? modifiedDate = null, LinkItem? linkItem = null);

    /// <summary>
    /// Shows URL status banner at the top of the details panel for non-accessible URLs or redirects.
    /// </summary>
    void ShowUrlStatusBanner(LinkItem linkItem);

    /// <summary>
    /// Shows category details in the Summary tab.
    /// </summary>
    Task<Button?> ShowCategoryDetailsAsync(CategoryItem category, TreeViewNode node,
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null, Func<Task>? onSyncBookmarks = null);

    /// <summary>
    /// Shows link details in the Summary tab with file information and catalog buttons for directories.
    /// </summary>
    Task<(Button? createButton, Button? refreshButton)> ShowLinkDetailsAsync(
        LinkItem linkItem,
        TreeViewNode? node,
        Func<Task> onCreateCatalog,
        Func<Task> onRefreshCatalog,
        Func<Task>? onRefreshArchive = null,
        Func<Task>? onSaveCategory = null);

    /// <summary>
    /// Adds an "Open in Explorer" button for directories.
    /// </summary>
    Task<Button?> AddOpenInExplorerButtonAsync(string path);
}
