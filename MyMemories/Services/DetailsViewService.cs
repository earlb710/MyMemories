using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace MyMemories.Services;

/// <summary>
/// Service for displaying node details in the details panel.
/// </summary>
public class DetailsViewService
{
    private readonly StackPanel _detailsPanel;
    private StackPanel? _headerPanel;

    public DetailsViewService(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    public void SetHeaderPanel(StackPanel headerPanel)
    {
        _headerPanel = headerPanel;
    }

    /// <summary>
    /// Shows file header information with name, description, and size.
    /// </summary>
    public async Task ShowFileHeaderAsync(string fileName, string? description, StorageFile file, BitmapImage? bitmap = null)
    {
        _headerPanel?.Children.Clear();
        
        if (_headerPanel == null) return;

        // File name and size
        var properties = await file.GetBasicPropertiesAsync();
        var fileSize = FileViewerService.FormatFileSize(properties.Size);
        
        var titleText = fileName;
        
        // Add image dimensions if it's an image
        if (bitmap != null)
        {
            titleText += $" ({bitmap.PixelWidth}x{bitmap.PixelHeight})";
        }
        
        titleText += $" - {fileSize}";

        _headerPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // Description if provided
        if (!string.IsNullOrWhiteSpace(description))
        {
            _headerPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }
    }

    /// <summary>
    /// Shows category header in the header panel with icon on the left.
    /// </summary>
    public void ShowCategoryHeader(string categoryName, string? description, string icon)
    {
        _headerPanel?.Children.Clear();
        
        if (_headerPanel == null) return;

        // Create horizontal layout with icon on left
        var horizontalPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        // Add large icon on the left
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 48,
            VerticalAlignment = VerticalAlignment.Top
        };
        horizontalPanel.Children.Add(iconText);

        // Add text content on the right
        var textPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        textPanel.Children.Add(new TextBlock
        {
            Text = categoryName,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        horizontalPanel.Children.Add(textPanel);
        _headerPanel.Children.Add(horizontalPanel);
    }

    /// <summary>
    /// Shows link header in the header panel with icon on the left.
    /// </summary>
    public void ShowLinkHeader(string linkTitle, string? description, string icon)
    {
        _headerPanel?.Children.Clear();
        
        if (_headerPanel == null) return;

        // Create horizontal layout with icon on left
        var horizontalPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        // Add large icon on the left
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 48,
            VerticalAlignment = VerticalAlignment.Top
        };
        horizontalPanel.Children.Add(iconText);

        // Add text content on the right
        var textPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        textPanel.Children.Add(new TextBlock
        {
            Text = linkTitle,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        horizontalPanel.Children.Add(textPanel);
        _headerPanel.Children.Add(horizontalPanel);
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public void ShowCategoryDetails(CategoryItem category, TreeViewNode node)
    {
        _detailsPanel.Children.Clear();

        // Remove the centered icon and title since they're now in the header

        // Add timestamps for category
        AddCategoryTimestamps(category);

        AddStatistics(node);
        
        if (node.Children.Count > 0)
        {
            AddLinksList(node);
        }
    }

    /// <summary>
    /// Shows link details with file information and catalog buttons for directories.
    /// </summary>
    public async Task<(Button? createButton, Button? refreshButton)> ShowLinkDetailsAsync(LinkItem linkItem, TreeViewNode? node, Func<Task> onCreateCatalog, Func<Task> onRefreshCatalog)
    {
        Debug.WriteLine($"[DetailsViewService] ShowLinkDetailsAsync called for: {linkItem.Title}");
        _detailsPanel.Children.Clear();

        Button? createButton = null;
        Button? refreshButton = null;

        // Add catalog buttons for directories at the top (only if we have a valid node)
        if (node != null && linkItem.IsDirectory && Directory.Exists(linkItem.Url))
        {
            Debug.WriteLine($"[DetailsViewService] Directory detected, checking for catalog entries");
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Check if catalog already exists
            bool hasCatalog = HasCatalogEntries(node);
            Debug.WriteLine($"[DetailsViewService] Has catalog: {hasCatalog}");

            if (!hasCatalog)
            {
                Debug.WriteLine($"[DetailsViewService] Creating 'Create Catalog' button");
                createButton = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE8B7" }, // AddToIcon
                            new TextBlock { Text = "Create Catalog", VerticalAlignment = VerticalAlignment.Center }
                        }
                    }
                };
                createButton.Click += async (s, e) =>
                {
                    Debug.WriteLine($"[DetailsViewService] Create Catalog button clicked!");
                    try
                    {
                        await onCreateCatalog();
                        Debug.WriteLine($"[DetailsViewService] onCreateCatalog completed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DetailsViewService] Error in onCreateCatalog: {ex.Message}");
                        Debug.WriteLine($"[DetailsViewService] Stack trace: {ex.StackTrace}");
                    }
                };
                buttonPanel.Children.Add(createButton);
                Debug.WriteLine($"[DetailsViewService] Create Catalog button added to panel");
            }
            else
            {
                Debug.WriteLine($"[DetailsViewService] Creating 'Refresh Catalog' button");
                refreshButton = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE72C" }, // SyncIcon
                            new TextBlock { Text = "Refresh Catalog", VerticalAlignment = VerticalAlignment.Center }
                        }
                    }
                };
                refreshButton.Click += async (s, e) =>
                {
                    Debug.WriteLine($"[DetailsViewService] Refresh Catalog button clicked!");
                    try
                    {
                        await onRefreshCatalog();
                        Debug.WriteLine($"[DetailsViewService] onRefreshCatalog completed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DetailsViewService] Error in onRefreshCatalog: {ex.Message}");
                    }
                };
                buttonPanel.Children.Add(refreshButton);
            }

            _detailsPanel.Children.Add(buttonPanel);
            Debug.WriteLine($"[DetailsViewService] Button panel added to details panel");
        }
        else
        {
            Debug.WriteLine($"[DetailsViewService] Catalog buttons NOT created. node={node != null}, IsDirectory={linkItem.IsDirectory}, Exists={Directory.Exists(linkItem.Url)}");
        }

        // Always show the path/URL section with proper handling
        if (!string.IsNullOrWhiteSpace(linkItem.Url))
        {
            var pathLabel = linkItem.IsDirectory ? "Directory Path" : "Path/URL";
            AddSection(pathLabel, linkItem.Url, isSelectable: true);
        }
        else
        {
            // Show warning if no path specified
            AddWarning("⚠️ No path or URL specified for this link");
        }

        // Add timestamp information
        AddTimestamps(linkItem);

        await AddFileSystemInfoAsync(linkItem);

        return (createButton, refreshButton);
    }

    /// <summary>
    /// Helper method to check if node has catalog entries.
    /// </summary>
    private bool HasCatalogEntries(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link && link.IsCatalogEntry)
            {
                return true;
            }
        }
        return false;
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
            // Let the system handle the default foreground color
        });

        var contentTextBlock = new TextBlock
        {
            Text = content,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = isSelectable,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // Only set foreground if we want it grayed out, otherwise use default
        if (isGrayedOut)
        {
            contentTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        _detailsPanel.Children.Add(contentTextBlock);
    }

    private void AddTimestamps(LinkItem linkItem)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Timestamps",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var timestampsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        timestampsPanel.Children.Add(CreateStatLine($"📅 Created: {linkItem.CreatedDate:yyyy-MM-dd HH:mm:ss}"));
        timestampsPanel.Children.Add(CreateStatLine($"📝 Modified: {linkItem.ModifiedDate:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(timestampsPanel);
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

    /// <summary>
    /// Adds category timestamps section to the details panel.
    /// </summary>
    private void AddCategoryTimestamps(CategoryItem category)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Timestamps",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var timestampsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        timestampsPanel.Children.Add(CreateStatLine($"📅 Created: {category.CreatedDate:yyyy-MM-dd HH:mm:ss}"));
        timestampsPanel.Children.Add(CreateStatLine($"📝 Modified: {category.ModifiedDate:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(timestampsPanel);
    }
}