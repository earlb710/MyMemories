using System;
using System.IO;
using System.IO.Compression;
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
    /// Shows link header in the header panel with icon on the left and optional link badge.
    /// </summary>
    public void ShowLinkHeader(string linkTitle, string? description, string icon, bool showLinkBadge = false)
    {
        _headerPanel?.Children.Clear();

        if (_headerPanel == null) return;

        // Create main container with relative positioning
        var containerGrid = new Grid();

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
        containerGrid.Children.Add(horizontalPanel);

        // Add link badge in lower right corner if requested
        if (showLinkBadge)
        {
            var linkBadge = new Border
            {
                Background = new SolidColorBrush(Colors.DodgerBlue),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 8, 4),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new FontIcon 
                        { 
                            Glyph = "\uE71B", // Link icon
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Colors.White)
                        },
                        new TextBlock 
                        { 
                            Text = "Link Only",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Colors.White),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            containerGrid.Children.Add(linkBadge);
        }

        _headerPanel.Children.Add(containerGrid);
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public void ShowCategoryDetails(CategoryItem category, TreeViewNode node)
    {
        _detailsPanel.Children.Clear();

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
        _detailsPanel.Children.Clear();

        Button? createButton = null;
        Button? refreshButton = null;

        // Check if it's a zip file
        bool isZipFile = linkItem.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && 
                       File.Exists(linkItem.Url);

        // Check if it's a Link Only folder (should not show catalog buttons)
        bool isLinkOnlyFolder = linkItem.IsDirectory && 
                               linkItem.FolderType == FolderLinkType.LinkOnly;

        // Add catalog buttons for directories AND zip files at the top (only if we have a valid node)
        // Modified condition: Show buttons for any zip file OR for directories (EXCEPT Link Only folders)
        if (node != null && !isLinkOnlyFolder && ((linkItem.IsDirectory && Directory.Exists(linkItem.Url)) || isZipFile))
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // Check if catalog already exists
            bool hasCatalog = HasCatalogEntries(node);

            if (!hasCatalog)
            {
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
                    try
                    {
                        await onCreateCatalog();
                    }
                    catch
                    {
                        // Silently handle errors
                    }
                };
                buttonPanel.Children.Add(createButton);
            }
            else
            {
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
                    try
                    {
                        await onRefreshCatalog();
                    }
                    catch
                    {
                        // Silently handle errors
                    }
                };
                buttonPanel.Children.Add(refreshButton);
            }

            _detailsPanel.Children.Add(buttonPanel);

            // Add Auto-Refresh Checkbox (only if catalog exists and NOT a zip file)
            if (hasCatalog && !isZipFile)
            {
                var autoRefreshCheckBox = new CheckBox
                {
                    Content = "Auto-refresh catalog on startup",
                    IsChecked = linkItem.AutoRefreshCatalog,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                autoRefreshCheckBox.Checked += async (s, e) =>
                {
                    linkItem.AutoRefreshCatalog = true;
                    // Trigger save through callback if needed
                    if (node != null)
                    {
                        // We'll need to add a callback parameter for this
                    }
                };

                autoRefreshCheckBox.Unchecked += (s, e) =>
                {
                    linkItem.AutoRefreshCatalog = false;
                };

                _detailsPanel.Children.Add(autoRefreshCheckBox);
            }
        }

        // Show "Link Only" info banner for Link Only folders
        if (isLinkOnlyFolder)
        {
            var infoBanner = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215)),
                BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon 
                        { 
                            Glyph = "\uE71B", // Link icon
                            FontSize = 16,
                            Foreground = new SolidColorBrush(Colors.DodgerBlue)
                        },
                        new TextBlock
                        {
                            Text = "This is a Link Only folder. Use it to open the folder directly without cataloging its contents.",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Colors.White),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            _detailsPanel.Children.Add(infoBanner);
        }

        // Show catalog statistics if this folder has catalog entries
        if (node != null && (linkItem.IsDirectory || isZipFile) && HasCatalogEntries(node))
        {
            AddCatalogStatistics(node, isZipFile);
        }

        // Always show the path/URL section with proper handling
        if (!string.IsNullOrWhiteSpace(linkItem.Url))
        {
            var pathLabel = isZipFile ? "Zip File Path" : (linkItem.IsDirectory ? "Directory Path" : "Path/URL");
            AddSection(pathLabel, linkItem.Url, isSelectable: true);
        }
        else
        {
            // Show warning if no path specified
            AddWarning("⚠️ No path or URL specified for this link");
        }

        // Add timestamp information
        AddTimestamps(linkItem);

        await AddFileSystemInfoAsync(linkItem, isZipFile);

        return (createButton, refreshButton);
    }

    /// <summary>
    /// Adds catalog statistics including total file count and total file size.
    /// </summary>
    private void AddCatalogStatistics(TreeViewNode node, bool isZipFile = false)
    {
        var allCatalogEntries = node.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .Select(child => child.Content as LinkItem)
            .Where(link => link != null)
            .ToList();

        if (allCatalogEntries.Count == 0)
            return;

        // Count only FILES (exclude subdirectories) to match the tree label
        var fileEntries = allCatalogEntries.Where(link => !link!.IsDirectory).ToList();
        var directoryEntries = allCatalogEntries.Where(link => link!.IsDirectory).ToList();

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = isZipFile ? "Zip Archive Contents" : "Catalog Statistics",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };

        // Show file count (matching tree label)
        statsPanel.Children.Add(CreateStatLine($"📊 Total Files: {fileEntries.Count}"));

        // Show directory count separately
        if (directoryEntries.Count > 0)
        {
            statsPanel.Children.Add(CreateStatLine($"📁 Subdirectories: {directoryEntries.Count}"));
        }

        // Calculate total size (only for files)
        ulong totalSize = 0;
        int accessibleFiles = 0;

        foreach (var fileEntry in fileEntries)
        {
            try
            {
                // For zip entries, use the FileSize property directly
                if (isZipFile && fileEntry!.FileSize.HasValue)
                {
                    totalSize += fileEntry.FileSize.Value;
                    accessibleFiles++;
                }
                else if (File.Exists(fileEntry!.Url))
                {
                    var fileInfo = new FileInfo(fileEntry.Url);
                    totalSize += (ulong)fileInfo.Length;
                    accessibleFiles++;
                }
            }
            catch
            {
                // Skip files that can't be accessed
            }
        }

        if (accessibleFiles > 0)
        {
            statsPanel.Children.Add(CreateStatLine($"💾 Total Size: {FileViewerService.FormatFileSize(totalSize)}"));

            if (accessibleFiles < fileEntries.Count)
            {
                statsPanel.Children.Add(new TextBlock
                {
                    Text = $"⚠️ {fileEntries.Count - accessibleFiles} file(s) could not be accessed",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Orange)
                });
            }
        }

        _detailsPanel.Children.Add(statsPanel);
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

    private void AddSection(string title, string content, bool isGrayedOut = false, bool isSelectable = false)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var contentTextBlock = new TextBlock
        {
            Text = content,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = isSelectable,
            Margin = new Thickness(0, 0, 0, 16)
        };

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

    private async Task AddFileSystemInfoAsync(LinkItem linkItem, bool isZipFile = false)
    {
        try
        {
            if (isZipFile && File.Exists(linkItem.Url))
            {
                AddZipFileInfo(linkItem.Url);
            }
            else if (linkItem.IsDirectory && Directory.Exists(linkItem.Url))
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

    private void AddZipFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);

        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Zip Archive Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var infoPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 16) };
        
        // Count files and directories in the zip archive
        try
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                int fileCount = 0;
                int dirCount = 0;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        dirCount++;
                    }
                    else if (!string.IsNullOrEmpty(entry.Name))
                    {
                        fileCount++;
                    }
                }

                infoPanel.Children.Add(CreateStatLine($"📄 Files in Archive: {fileCount}"));
                if (dirCount > 0)
                {
                    infoPanel.Children.Add(CreateStatLine($"📁 Folders in Archive: {dirCount}"));
                }
            }
        }
        catch (Exception ex)
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"⚠️ Could not read archive contents: {ex.Message}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Orange),
                TextWrapping = TextWrapping.Wrap
            });
        }

        infoPanel.Children.Add(CreateStatLine($"📦 Archive Size: {FileViewerService.FormatFileSize((ulong)fileInfo.Length)}"));
        infoPanel.Children.Add(CreateStatLine($"📂 Extension: {fileInfo.Extension}"));
        infoPanel.Children.Add(CreateStatLine($"📅 Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"📝 Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"));
        infoPanel.Children.Add(CreateStatLine($"👁️ Last Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}"));

        _detailsPanel.Children.Add(infoPanel);
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

    private async Task<bool> CheckZipHasManifestAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            return false;

        try
        {
            return await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipFilePath);
                return archive.GetEntry("_MANIFEST.txt") != null;
            });
        }
        catch
        {
            return false;
        }
    }
}