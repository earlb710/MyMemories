using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Displays detailed information about a link item.
/// </summary>
public class LinkDetailsViewer
{
    private readonly XamlRoot _xamlRoot;

    // Segoe MDL2 Assets glyphs for icons
    private const string CalendarGlyph = "\uE787";    // Calendar
    private const string EditGlyph = "\uE70F";        // Edit/Modified
    private const string ViewGlyph = "\uE7B3";        // View/Accessed
    private const string PackageGlyph = "\uE7B8";     // Package/Size
    private const string LinkGlyph = "\uE71B";        // Link
    private const string FolderGlyph = "\uE8B7";      // Folder
    private const string FilterGlyph = "\uE71C";      // Filter

    public LinkDetailsViewer(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    /// <summary>
    /// Shows the link details dialog.
    /// </summary>
    public async Task<bool> ShowAsync(LinkItem link)
    {
        var detailsPanel = BuildDetailsPanel(link);

        var dialog = new ContentDialog
        {
            Title = "Link Details",
            Content = new ScrollViewer
            {
                Content = detailsPanel,
                MaxHeight = 600
            },
            CloseButtonText = "Close",
            SecondaryButtonText = "Edit",
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Secondary;
    }

    private StackPanel BuildDetailsPanel(LinkItem link)
    {
        var detailsPanel = new StackPanel { Spacing = 12 };

        AddTitleSection(detailsPanel, link);
        AddTagsSection(detailsPanel, link);
        AddUrlSection(detailsPanel, link);
        
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            AddDescriptionSection(detailsPanel, link);
        }

        AddTypeSection(detailsPanel, link);
        
        if (link.IsDirectory)
        {
            AddFolderTypeSection(detailsPanel, link);
        }

        AddLinkTimestampsSection(detailsPanel, link);
        AddFileSystemTimestampsSection(detailsPanel, link);

        return detailsPanel;
    }

    private void AddTitleSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel("Title:"));
        panel.Children.Add(new TextBlock
        {
            Text = link.Title,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddTagsSection(StackPanel panel, LinkItem link)
    {
        if (link.TagIds.Count == 0)
            return;

        var tagService = Services.TagManagementService.Instance;
        if (tagService == null)
            return;

        panel.Children.Add(DialogHelpers.CreateLabel("Tags:"));
        
        var tagsPanel = tagService.CreateTagBadgesPanel(link.TagIds, fontSize: 12, spacing: 8);
        tagsPanel.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(tagsPanel);
    }

    private void AddUrlSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel(
            link.IsDirectory ? "Directory Path:" : "Path/URL:"));
        
        var urlPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };
        
        urlPanel.Children.Add(new TextBlock
        {
            Text = link.Url,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        
        var copyButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE8C8", FontSize = 14 }, // Copy glyph
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(copyButton, "Copy to clipboard");
        
        copyButton.Click += (s, e) => CopyToClipboard(link.Url);
        urlPanel.Children.Add(copyButton);
        panel.Children.Add(urlPanel);
    }

    private void AddDescriptionSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel("Description:"));
        panel.Children.Add(new TextBlock
        {
            Text = link.Description,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddTypeSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel("Type:"));
        panel.Children.Add(new TextBlock
        {
            Text = link.IsDirectory ? "Directory" : "File/URL",
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddFolderTypeSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel("Folder Type:"));
        
        var folderTypePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        
        string glyph;
        string text;
        switch (link.FolderType)
        {
            case FolderLinkType.CatalogueFiles:
                glyph = FolderGlyph;
                text = "Catalogue Files";
                break;
            case FolderLinkType.FilteredCatalogue:
                glyph = FilterGlyph;
                text = "Filtered Catalogue";
                break;
            default:
                glyph = LinkGlyph;
                text = "Link Only";
                break;
        }
        
        folderTypePanel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        folderTypePanel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        folderTypePanel.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(folderTypePanel);

        if (link.FolderType == FolderLinkType.FilteredCatalogue && 
            !string.IsNullOrWhiteSpace(link.FileFilters))
        {
            panel.Children.Add(DialogHelpers.CreateLabel("File Filters:"));
            panel.Children.Add(new TextBlock
            {
                Text = link.FileFilters,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
    }

    private void AddLinkTimestampsSection(StackPanel panel, LinkItem link)
    {
        panel.Children.Add(DialogHelpers.CreateLabel("Link Timestamps:"));
        panel.Children.Add(CreateTimestampRow(CalendarGlyph, "Created:", link.CreatedDate));
        panel.Children.Add(CreateTimestampRow(EditGlyph, "Modified:", link.ModifiedDate, bottomMargin: 8));
    }

    private void AddFileSystemTimestampsSection(StackPanel panel, LinkItem link)
    {
        if (string.IsNullOrEmpty(link.Url))
            return;

        try
        {
            if (link.IsDirectory && Directory.Exists(link.Url))
            {
                var dirInfo = new DirectoryInfo(link.Url);
                panel.Children.Add(DialogHelpers.CreateLabel("Directory Timestamps:"));
                AddTimestampInfo(panel, dirInfo.CreationTime, dirInfo.LastWriteTime, dirInfo.LastAccessTime);
            }
            else if (File.Exists(link.Url))
            {
                var fileInfo = new FileInfo(link.Url);
                panel.Children.Add(DialogHelpers.CreateLabel("File Timestamps:"));
                panel.Children.Add(CreateIconTextRow(PackageGlyph, $"Size: {FileUtilities.FormatFileSize((ulong)fileInfo.Length)}"));
                AddTimestampInfo(panel, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime);
            }
        }
        catch
        {
            // File/directory not accessible - skip
        }
    }

    private void AddTimestampInfo(StackPanel panel, DateTime created, DateTime modified, DateTime accessed)
    {
        panel.Children.Add(CreateTimestampRow(CalendarGlyph, "Created:", created));
        panel.Children.Add(CreateTimestampRow(EditGlyph, "Modified:", modified));
        panel.Children.Add(CreateTimestampRow(ViewGlyph, "Accessed:", accessed, bottomMargin: 8));
    }

    private StackPanel CreateTimestampRow(string glyph, string label, DateTime dateTime, int bottomMargin = 4)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, bottomMargin)
        };
        
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12 });
        row.Children.Add(new TextBlock 
        { 
            Text = $"{label} {dateTime:yyyy-MM-dd HH:mm:ss}",
            VerticalAlignment = VerticalAlignment.Center
        });
        
        return row;
    }

    private StackPanel CreateIconTextRow(string glyph, string text, int bottomMargin = 4)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, bottomMargin)
        };
        
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12 });
        row.Children.Add(new TextBlock 
        { 
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        
        return row;
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
        catch
        {
            // Silently fail if clipboard access is denied
        }
    }
}