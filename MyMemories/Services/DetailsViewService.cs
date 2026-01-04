using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;

namespace MyMemories.Services;

/// <summary>
/// Service for displaying node details in the details panel.
/// </summary>
public class DetailsViewService
{
    private readonly StackPanel _detailsPanel;

    public DetailsViewService(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public void ShowCategoryDetails(CategoryItem category, TreeViewNode node)
    {
        _detailsPanel.Children.Clear();

        AddIcon(category.Icon, 64);
        AddTitle(category.Name);
        AddDivider();
        
        AddSection("Description", 
            string.IsNullOrWhiteSpace(category.Description) 
                ? "(No description provided)" 
                : category.Description,
            isGrayedOut: string.IsNullOrWhiteSpace(category.Description));

        AddStatistics(node);
        
        if (node.Children.Count > 0)
        {
            AddLinksList(node);
        }
    }

    /// <summary>
    /// Shows link details with file information.
    /// </summary>
    public async Task ShowLinkDetailsAsync(LinkItem linkItem)
    {
        _detailsPanel.Children.Clear();

        var linkIcon = linkItem.IsDirectory ? "📁" : "🔗";
        AddIcon(linkIcon, 64);
        AddTitle(linkItem.Title);
        
        var typeText = GetLinkType(linkItem);
        AddTypeBadge(typeText);
        AddDivider();

        AddSection(linkItem.IsDirectory ? "Directory Path" : "Path/URL", linkItem.Url, isSelectable: true);
        AddSection("Description", 
            string.IsNullOrWhiteSpace(linkItem.Description) 
                ? "(No description provided)" 
                : linkItem.Description,
            isGrayedOut: string.IsNullOrWhiteSpace(linkItem.Description));

        await AddFileSystemInfoAsync(linkItem);
    }

    /// <summary>
    /// Adds an "Open in Explorer" button for directories.
    /// </summary>
    public async Task<Button?> AddOpenInExplorerButtonAsync(string path)
    {
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            var openButton = new Button
            {
                Content = "Open in File Explorer",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 16, 0, 0)
            };

            openButton.Click += async (s, e) =>
            {
                try
                {
                    await Windows.System.Launcher.LaunchFolderAsync(folder);
                }
                catch
                {
                    // Silently fail
                }
            };

            _detailsPanel.Children.Add(openButton);
            return openButton;
        }
        catch
        {
            return null;
        }
    }

    private void AddIcon(string icon, int fontSize)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = fontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });
    }

    private void AddTitle(string title)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void AddTypeBadge(string typeText)
    {
        var typeBorder = new Border
        {
            Background = new SolidColorBrush(Colors.DodgerBlue),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4, 12, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        typeBorder.Child = new TextBlock
        {
            Text = typeText,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        _detailsPanel.Children.Add(typeBorder);
    }

    private void AddDivider()
    {
        _detailsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Colors.Gray),
            Margin = new Thickness(0, 16, 0, 16)
        });
    }

    private void AddSection(string title, string content, bool isGrayedOut = false, bool isSelectable = false)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = content,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = isSelectable,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = isGrayedOut ? new SolidColorBrush(Colors.Gray) : null
        });
    }

    private void AddStatistics(TreeViewNode node)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Statistics",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        var (fileCount, dirCount, urlCount) = CountLinkTypes(node);
        
        statsPanel.Children.Add(CreateStatLine($"📊 Total Links: {node.Children.Count}"));
        statsPanel.Children.Add(CreateStatLine($"📄 Files: {fileCount}"));
        statsPanel.Children.Add(CreateStatLine($"📁 Directories: {dirCount}"));
        statsPanel.Children.Add(CreateStatLine($"🌐 URLs: {urlCount}"));

        _detailsPanel.Children.Add(statsPanel);
    }

    private (int files, int dirs, int urls) CountLinkTypes(TreeViewNode node)
    {
        int fileCount = 0, dirCount = 0, urlCount = 0;
        
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                if (link.IsDirectory)
                    dirCount++;
                else if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && !uri.IsFile)
                    urlCount++;
                else
                    fileCount++;
            }
        }
        
        return (fileCount, dirCount, urlCount);
    }

    private void AddLinksList(TreeViewNode node)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Links in this Category",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var linksListPanel = new StackPanel { Spacing = 8 };
        
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                linksListPanel.Children.Add(CreateLinkCard(link));
            }
        }
        
        _detailsPanel.Children.Add(linksListPanel);
    }

    private Border CreateLinkCard(LinkItem link)
    {
        var linkCard = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var linkInfo = new StackPanel { Spacing = 4 };
        linkInfo.Children.Add(new TextBlock
        {
            Text = link.ToString(),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            linkInfo.Children.Add(new TextBlock
            {
                Text = link.Description,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        linkCard.Child = linkInfo;
        return linkCard;
    }

    private async Task AddFileSystemInfoAsync(LinkItem linkItem)
    {
        try
        {
            if (linkItem.IsDirectory && Directory.Exists(linkItem.Url))
            {
                await AddDirectoryInfoAsync(linkItem.Url);
            }
            else if (File.Exists(linkItem.Url))
            {
                AddFileInfo(linkItem.Url);
            }
        }
        catch (Exception ex)
        {
            AddWarning($"⚠️ Unable to access file/directory information: {ex.Message}");
        }
    }

    private async Task AddDirectoryInfoAsync(string path)
    {
        var dirInfo = new DirectoryInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Directory Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        infoPanel.Children.Add(CreateStatLine($"📅 Created: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"📝 Last Modified: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"👁️ Last Accessed: {dirInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        try
        {
            var files = dirInfo.GetFiles();
            var dirs = dirInfo.GetDirectories();
            infoPanel.Children.Add(CreateStatLine($"📄 Contains: {files.Length} file(s), {dirs.Length} folder(s)"));
        }
        catch { }

        _detailsPanel.Children.Add(infoPanel);
    }

    private void AddFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "File Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        infoPanel.Children.Add(CreateStatLine($"📦 Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));
        infoPanel.Children.Add(CreateStatLine($"📂 Extension: {fileInfo.Extension}"));
        infoPanel.Children.Add(CreateStatLine($"📅 Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"📝 Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"👁️ Last Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(infoPanel);
    }

    private void AddWarning(string message)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Orange),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
    }

    private TextBlock CreateStatLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14
        };
    }

    private string GetLinkType(LinkItem linkItem)
    {
        if (linkItem.IsDirectory)
            return "Directory";
        
        if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var uri) && !uri.IsFile)
            return "Web URL";
        
        return "File";
    }
}