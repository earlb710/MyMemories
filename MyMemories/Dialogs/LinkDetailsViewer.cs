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
            Content = "📋",
            FontSize = 16,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Top
        };
        
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
        
        string folderTypeText = link.FolderType switch
        {
            FolderLinkType.LinkOnly => "🔗 Link Only",
            FolderLinkType.CatalogueFiles => "📂 Catalogue Files",
            FolderLinkType.FilteredCatalogue => "🗂️ Filtered Catalogue",
            _ => "Link Only"
        };
        
        panel.Children.Add(new TextBlock
        {
            Text = folderTypeText,
            Margin = new Thickness(0, 0, 0, 8)
        });

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
        panel.Children.Add(new TextBlock
        {
            Text = $"📅 Created: {link.CreatedDate:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"📝 Modified: {link.ModifiedDate:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 8)
        });
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
                panel.Children.Add(new TextBlock
                {
                    Text = $"📦 Size: {FileUtilities.FormatFileSize((ulong)fileInfo.Length)}",
                    Margin = new Thickness(0, 0, 0, 4)
                });
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
        panel.Children.Add(new TextBlock
        {
            Text = $"📅 Created: {created:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"📝 Modified: {modified:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"👁️ Accessed: {accessed:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 8)
        });
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