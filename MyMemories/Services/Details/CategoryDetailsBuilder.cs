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

    public CategoryDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Shows category details.
    /// </summary>
    public async Task<Button?> ShowCategoryDetailsAsync(CategoryItem category, TreeViewNode node, 
        Func<Task>? onRefreshBookmarks = null, Func<Task>? onRefreshUrlState = null)
    {
        _detailsPanel.Children.Clear();

        Button? refreshButton = null;

        if (category.IsBookmarkImport)
        {
            await AddBookmarkImportInfoAsync(category);

            if (!string.IsNullOrEmpty(category.SourceBookmarksPath) && onRefreshBookmarks != null)
            {
                refreshButton = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE72C" },
                            new TextBlock { Text = "Refresh Bookmarks", VerticalAlignment = VerticalAlignment.Center }
                        }
                    },
                    Margin = new Thickness(0, 0, 0, 16)
                };

                ToolTipService.SetToolTip(refreshButton, $"Re-imports bookmarks from {category.SourceBrowserName ?? "browser"} to update this category with the latest bookmarks");

                refreshButton.Click += async (s, e) =>
                {
                    try { await onRefreshBookmarks(); }
                    catch { }
                };

                _detailsPanel.Children.Add(refreshButton);
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
            infoStack.Children.Add(new TextBlock
            {
                Text = $"?? Last Import: {category.LastBookmarkImportDate.Value:yyyy-MM-dd HH:mm:ss}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.LightGray)
            });
        }

        if (category.ImportedBookmarkCount.HasValue)
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = $"?? Imported Bookmarks: {category.ImportedBookmarkCount.Value}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.LightGray)
            });
        }

        if (!string.IsNullOrEmpty(category.SourceBookmarksPath))
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = $"?? Source Path:",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 4, 0, 0)
            });

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
        timestampsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Created: {category.CreatedDate:yyyy-MM-dd HH:mm:ss}"));
        timestampsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Modified: {category.ModifiedDate:yyyy-MM-dd HH:mm:ss}"));

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

        statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Total Links: {stats.TotalLinks}"));

        if (stats.FileCount > 0)
        {
            var fileSizeText = stats.FileTotalSize > 0
                ? $" ({FileViewerService.FormatFileSize(stats.FileTotalSize)})"
                : "";
            statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Files: {stats.FileCount}{fileSizeText}"));
        }
        else
        {
            statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Files: 0"));
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
            statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Directories: {stats.DirectoryCount}{dirDetailText}"));
        }
        else
        {
            statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? Directories: 0"));
        }

        statsPanel.Children.Add(DetailsUIHelpers.CreateStatLine($"?? URLs: {stats.UrlCount}"));

        _detailsPanel.Children.Add(statsPanel);
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
}
