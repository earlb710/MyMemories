using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds category details content for the details panel.
/// </summary>
public class CategoryDetailsBuilder
{
    private readonly StackPanel _detailsPanel;

    // Segoe MDL2 Assets glyphs
    private const string CalendarGlyph = "\uE787";    // Calendar
    private const string EditGlyph = "\uE70F";        // Edit/Modified
    private const string LinkGlyph = "\uE71B";        // Link
    private const string FileGlyph = "\uE8A5";        // Document
    private const string FolderGlyph = "\uE8B7";      // Folder
    private const string GlobeGlyph = "\uE774";       // Globe/URL
    private const string BookmarkGlyph = "\uE8A4";    // Bookmark
    private const string PathGlyph = "\uE8DA";        // Path

    public CategoryDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public async Task<Button?> ShowCategoryDetailsAsync(CategoryItem category, TreeViewNode node, 
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null, Func<Task>? onSyncBookmarks = null,
        Func<string, Task>? onClearArchive = null)
    {
        _detailsPanel.Children.Clear();

        Button? refreshButton = null;

        // Special handling for Archive node
        if (category.IsArchiveNode)
        {
            AddArchiveNodeDetails(category, node, onClearArchive);
            return null;
        }

        if (category.IsBookmarkImport)
        {
            await AddBookmarkImportInfoAsync(category);

            if (!string.IsNullOrEmpty(category.SourceBookmarksPath))
            {
                var buttonPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                // Sync button (detect changes)
                if (onSyncBookmarks != null)
                {
                    var syncButton = new Button
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                new FontIcon { Glyph = "\uE895" }, // Sync icon
                                new TextBlock { Text = "Sync", VerticalAlignment = VerticalAlignment.Center }
                            }
                        }
                    };

                    ToolTipService.SetToolTip(syncButton, 
                        $"Detect changes between {category.SourceBrowserName ?? "browser"} and MyMemories.\nShows new, modified, and deleted bookmarks.");

                    syncButton.Click += async (s, e) =>
                    {
                        try { await onSyncBookmarks(); }
                        catch { }
                    };

                    buttonPanel.Children.Add(syncButton);
                }

                // Refresh button (full re-import)
                if (onRefreshBookmarks != null)
                {
                    refreshButton = new Button
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                new FontIcon { Glyph = "\uE72C" }, // Refresh icon
                                new TextBlock { Text = "Refresh All", VerticalAlignment = VerticalAlignment.Center }
                            }
                        }
                    };

                    ToolTipService.SetToolTip(refreshButton, 
                        $"Re-imports all bookmarks from {category.SourceBrowserName ?? "browser"}.\nReplaces or adds to existing bookmarks.");

                    refreshButton.Click += async (s, e) =>
                    {
                        try { await onRefreshBookmarks(); }
                        catch { }
                    };

                    buttonPanel.Children.Add(refreshButton);
                }

                _detailsPanel.Children.Add(buttonPanel);
            }
        }

        if (category.IsBookmarkCategory && onRefreshUrlState != null)
        {
            var refreshUrlStateButton = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE895" },
                        new TextBlock { Text = "Refresh URL State", VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Margin = new Thickness(0, 0, 0, 16)
            };

            ToolTipService.SetToolTip(refreshUrlStateButton, "Checks accessibility of all URLs in this category and marks them with status indicators (green=accessible, yellow=error, red=not found)");

            refreshUrlStateButton.Click += async (s, e) =>
            {
                try { await onRefreshUrlState(); }
                catch { }
            };

            _detailsPanel.Children.Add(refreshUrlStateButton);
        }

        // Add ratings display if ratings exist
        if (category.Ratings.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[CategoryDetailsBuilder] Category has {category.Ratings.Count} ratings");
            
            var ratingsPanel = RatingManagementService.Instance?.CreateRatingsDetailsPanel(category.Ratings);
            
            System.Diagnostics.Debug.WriteLine($"[CategoryDetailsBuilder] ratingsPanel Children.Count = {ratingsPanel?.Children.Count ?? -1}");
            
            if (ratingsPanel != null && ratingsPanel.Children.Count > 0)
            {
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Ratings",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                _detailsPanel.Children.Add(ratingsPanel);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CategoryDetailsBuilder] Ratings panel was null or empty!");
            }
        }

        AddCategoryTimestamps(category);
        AddStatistics(node);

        if (node.Children.Count > 0)
        {
            AddLinksList(node);
        }

        return refreshButton;
    }

    private async Task AddBookmarkImportInfoAsync(CategoryItem category)
    {
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Bookmark Import Information",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var importPanel = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215)),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var infoStack = new StackPanel { Spacing = 8 };

        if (category.SourceBrowserType.HasValue && !string.IsNullOrEmpty(category.SourceBrowserName))
        {
            var browserPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };

            browserPanel.Children.Add(new FontIcon
            {
                Glyph = GetBrowserGlyph(category.SourceBrowserType.Value),
                FontSize = 24,
                Foreground = new SolidColorBrush(Colors.White)
            });

            browserPanel.Children.Add(new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Source: {category.SourceBrowserName}",
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White)
                    }
                }
            });

            infoStack.Children.Add(browserPanel);
        }

        if (category.LastBookmarkImportDate.HasValue)
        {
            infoStack.Children.Add(CreateIconTextRow(CalendarGlyph, $"Last Import: {category.LastBookmarkImportDate.Value:yyyy-MM-dd HH:mm:ss}", Colors.LightGray));
        }

        if (category.ImportedBookmarkCount.HasValue)
        {
            infoStack.Children.Add(CreateIconTextRow(BookmarkGlyph, $"Imported Bookmarks: {category.ImportedBookmarkCount.Value}", Colors.LightGray));
        }

        if (!string.IsNullOrEmpty(category.SourceBookmarksPath))
        {
            infoStack.Children.Add(CreateIconTextRow(PathGlyph, "Source Path:", Colors.White, isBold: true));

            infoStack.Children.Add(new TextBlock
            {
                Text = category.SourceBookmarksPath,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.LightGray),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });
        }

        importPanel.Child = infoStack;
        _detailsPanel.Children.Add(importPanel);
    }

    private StackPanel CreateIconTextRow(string glyph, string text, Windows.UI.Color foregroundColor, bool isBold = false)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        row.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 12,
            Foreground = new SolidColorBrush(foregroundColor)
        });

        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = isBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = new SolidColorBrush(foregroundColor),
            VerticalAlignment = VerticalAlignment.Center
        });

        return row;
    }

    private string GetBrowserGlyph(BrowserType browserType)
    {
        return browserType switch
        {
            BrowserType.Chrome => "\uE774",
            BrowserType.Edge => "\uE737",
            BrowserType.Brave => "\uE8A1",
            BrowserType.Vivaldi => "\uE773",
            BrowserType.Opera => "\uE8A5",
            BrowserType.Firefox => "\uE7E8",
            _ => "\uE774"
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
        timestampsPanel.Children.Add(CreateIconStatLine(CalendarGlyph, $"Created: {category.CreatedDate:yyyy-MM-dd HH:mm:ss}"));
        timestampsPanel.Children.Add(CreateIconStatLine(EditGlyph, $"Modified: {category.ModifiedDate:yyyy-MM-dd HH:mm:ss}"));

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
        var stats = CalculateDetailedStatistics(node);

        statsPanel.Children.Add(CreateIconStatLine(LinkGlyph, $"Total Links: {stats.TotalLinks}"));

        if (stats.FileCount > 0)
        {
            var fileSizeText = stats.FileTotalSize > 0
                ? $" ({FileViewerService.FormatFileSize(stats.FileTotalSize)})"
                : "";
            statsPanel.Children.Add(CreateIconStatLine(FileGlyph, $"Files: {stats.FileCount}{fileSizeText}"));
        }
        else
        {
            statsPanel.Children.Add(CreateIconStatLine(FileGlyph, "Files: 0"));
        }

        if (stats.DirectoryCount > 0)
        {
            var dirDetails = new List<string>();
            if (stats.DirectoryNestedFileCount > 0)
            {
                dirDetails.Add($"{stats.DirectoryNestedFileCount} files");
            }
            if (stats.DirectoryTotalSize > 0)
            {
                dirDetails.Add(FileViewerService.FormatFileSize(stats.DirectoryTotalSize));
            }

            var dirDetailText = dirDetails.Count > 0
                ? $" ({string.Join(", ", dirDetails)})"
                : "";
            statsPanel.Children.Add(CreateIconStatLine(FolderGlyph, $"Directories: {stats.DirectoryCount}{dirDetailText}"));
        }
        else
        {
            statsPanel.Children.Add(CreateIconStatLine(FolderGlyph, "Directories: 0"));
        }

        statsPanel.Children.Add(CreateIconStatLine(GlobeGlyph, $"URLs: {stats.UrlCount}"));

        _detailsPanel.Children.Add(statsPanel);
    }

    /// <summary>
    /// Creates a stat line with an icon and text.
    /// </summary>
    private StackPanel CreateIconStatLine(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 12 },
                new TextBlock { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }

    private (int TotalLinks, int FileCount, ulong FileTotalSize, int DirectoryCount, int DirectoryNestedFileCount, ulong DirectoryTotalSize, int UrlCount) CalculateDetailedStatistics(TreeViewNode node)
    {
        int fileCount = 0;
        ulong fileTotalSize = 0;
        int directoryCount = 0;
        int directoryNestedFileCount = 0;
        ulong directoryTotalSize = 0;
        int urlCount = 0;

        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                if (link.IsCatalogEntry)
                    continue;

                if (link.IsDirectory)
                {
                    directoryCount++;

                    if (!string.IsNullOrEmpty(link.Url) && Directory.Exists(link.Url))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(link.Url);
                            var dirName = dirInfo.Name.ToLowerInvariant();
                            if (dirName == "node_modules" || dirName == ".git" || dirName == "bin" || dirName == "obj")
                            {
                                continue;
                            }

                            var files = dirInfo.GetFiles();
                            directoryNestedFileCount += files.Length;

                            foreach (var file in files)
                            {
                                directoryTotalSize += (ulong)file.Length;
                            }
                        }
                        catch { }
                    }
                }
                else if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && !uri.IsFile)
                {
                    urlCount++;
                }
                else
                {
                    fileCount++;

                    if (!string.IsNullOrEmpty(link.Url) && File.Exists(link.Url))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(link.Url);
                            fileTotalSize += (ulong)fileInfo.Length;
                        }
                        catch { }
                    }
                }
            }
        }

        int totalLinks = fileCount + directoryCount + urlCount;
        return (totalLinks, fileCount, fileTotalSize, directoryCount, directoryNestedFileCount, directoryTotalSize, urlCount);
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
                linksListPanel.Children.Add(DetailsUIHelpers.CreateLinkCard(link));
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

        // Title
        linkInfo.Children.Add(new TextBlock
        {
            Text = link.ToString(),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Tag badges row
        if (link.TagIds.Count > 0)
        {
            var tagsPanel = TagManagementService.Instance?.CreateTagBadgesPanel(link.TagIds, fontSize: 10, spacing: 4);
            if (tagsPanel != null && tagsPanel.Children.Count > 0)
            {
                linkInfo.Children.Add(tagsPanel);
            }
        }

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
    
    /// <summary>
    /// Adds special details view for the Archive node with Clear Archive button.
    /// </summary>
    private void AddArchiveNodeDetails(CategoryItem category, TreeViewNode node, Func<string, Task>? onClearArchive)
    {
        // Header
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Archive",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.Red),
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        // Description
        _detailsPanel.Children.Add(new TextBlock
        {
            Text = "Archived items are soft-deleted and can be restored or permanently deleted.",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.Gray),
            Margin = new Thickness(0, 0, 0, 16)
        });
        
        // Statistics
        var itemCount = node.Children.Count;
        var statsPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 16) };
        
        statsPanel.Children.Add(new TextBlock
        {
            Text = $"Archived Items: {itemCount}",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        
        // Count items by age
        var now = DateTime.Now;
        int olderThanDay = 0, olderThanWeek = 0, olderThanMonth = 0;
        
        foreach (var child in node.Children)
        {
            DateTime? archivedDate = null;
            
            if (child.Content is CategoryItem cat)
                archivedDate = cat.ArchivedDate;
            else if (child.Content is LinkItem link)
                archivedDate = link.ArchivedDate;
            
            if (archivedDate.HasValue)
            {
                var age = now - archivedDate.Value;
                if (age.TotalDays > 30) olderThanMonth++;
                else if (age.TotalDays > 7) olderThanWeek++;
                else if (age.TotalDays > 1) olderThanDay++;
            }
        }
        
        if (itemCount > 0)
        {
            var ageInfo = new TextBlock
            {
                Text = $"• Older than 1 day: {olderThanDay + olderThanWeek + olderThanMonth}\n" +
                       $"• Older than 1 week: {olderThanWeek + olderThanMonth}\n" +
                       $"• Older than 1 month: {olderThanMonth}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(8, 0, 0, 0)
            };
            statsPanel.Children.Add(ageInfo);
        }
        
        _detailsPanel.Children.Add(statsPanel);
        
        // Clear Archive button with dropdown
        if (onClearArchive != null && itemCount > 0)
        {
            var clearButton = new DropDownButton
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE74D", Foreground = new SolidColorBrush(Colors.Red) }, // Delete icon
                        new TextBlock { Text = "Clear Archive", VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
            
            var flyout = new MenuFlyout();
            
            // Everything
            var clearAllItem = new MenuFlyoutItem
            {
                Text = "Everything",
                Icon = new FontIcon { Glyph = "\uE74D" }
            };
            clearAllItem.Click += async (s, e) => await onClearArchive("all");
            flyout.Items.Add(clearAllItem);
            
            flyout.Items.Add(new MenuFlyoutSeparator());
            
            // Older than a day
            var clearDayItem = new MenuFlyoutItem
            {
                Text = $"Older than 1 day ({olderThanDay + olderThanWeek + olderThanMonth} items)",
                Icon = new FontIcon { Glyph = "\uE787" } // Calendar
            };
            clearDayItem.Click += async (s, e) => await onClearArchive("day");
            flyout.Items.Add(clearDayItem);
            
            // Older than a week
            var clearWeekItem = new MenuFlyoutItem
            {
                Text = $"Older than 1 week ({olderThanWeek + olderThanMonth} items)",
                Icon = new FontIcon { Glyph = "\uE787" }
            };
            clearWeekItem.Click += async (s, e) => await onClearArchive("week");
            flyout.Items.Add(clearWeekItem);
            
            // Older than a month
            var clearMonthItem = new MenuFlyoutItem
            {
                Text = $"Older than 1 month ({olderThanMonth} items)",
                Icon = new FontIcon { Glyph = "\uE787" }
            };
            clearMonthItem.Click += async (s, e) => await onClearArchive("month");
            flyout.Items.Add(clearMonthItem);
            
            clearButton.Flyout = flyout;
            _detailsPanel.Children.Add(clearButton);
        }
        else if (itemCount == 0)
        {
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Archive is empty.",
                FontSize = 13,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }
        
        // Show list of archived items with dates
        if (itemCount > 0)
        {
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Archived Items",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 8)
            });
            
            foreach (var child in node.Children)
            {
                string? name = null;
                DateTime? archivedDate = null;
                string icon = "??";
                
                if (child.Content is CategoryItem cat)
                {
                    name = cat.Name;
                    archivedDate = cat.ArchivedDate;
                    icon = cat.Icon;
                }
                else if (child.Content is LinkItem link)
                {
                    name = link.Title;
                    archivedDate = link.ArchivedDate;
                    icon = link.GetIcon();
                }
                
                if (!string.IsNullOrEmpty(name))
                {
                    var itemPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    
                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = icon,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Red)
                    });
                    
                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = name,
                        FontSize = 13
                    });
                    
                    if (archivedDate.HasValue)
                    {
                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = $"({archivedDate.Value:yyyy-MM-dd HH:mm})",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Colors.Gray),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    
                    _detailsPanel.Children.Add(itemPanel);
                }
            }
        }
    }
}
