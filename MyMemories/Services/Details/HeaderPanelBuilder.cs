using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds header panel content for different item types.
/// </summary>
public class HeaderPanelBuilder
{
    private readonly StackPanel _headerPanel;

    public HeaderPanelBuilder(StackPanel headerPanel)
    {
        _headerPanel = headerPanel;
    }

    /// <summary>
    /// Shows file header information with name, description, and size.
    /// </summary>
    public async Task ShowFileHeaderAsync(string fileName, string? description, StorageFile file, BitmapImage? bitmap = null)
    {
        _headerPanel.Children.Clear();

        var properties = await file.GetBasicPropertiesAsync();
        var fileSize = FileViewerService.FormatFileSize(properties.Size);

        var titleText = fileName;

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
    public void ShowCategoryHeader(string categoryName, string? description, string icon, CategoryItem? category = null)
    {
        _headerPanel.Children.Clear();

        var horizontalPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 48,
            VerticalAlignment = VerticalAlignment.Top
        };
        horizontalPanel.Children.Add(iconText);

        var textPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Title
        textPanel.Children.Add(new TextBlock
        {
            Text = categoryName,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // Tag badges row
        if (category != null && category.TagIds.Count > 0)
        {
            var tagsPanel = TagManagementService.Instance?.CreateTagBadgesPanel(category.TagIds, fontSize: 11, spacing: 6);
            if (tagsPanel != null && tagsPanel.Children.Count > 0)
            {
                textPanel.Children.Add(tagsPanel);
            }
        }

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
    public void ShowLinkHeader(string linkTitle, string? description, string icon, bool showLinkBadge = false, 
        ulong? fileSize = null, DateTime? createdDate = null, DateTime? modifiedDate = null, LinkItem? linkItem = null)
    {
        _headerPanel.Children.Clear();

        var containerGrid = new Grid();

        var horizontalPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 32,
            VerticalAlignment = VerticalAlignment.Top
        };
        horizontalPanel.Children.Add(iconText);

        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleText = linkTitle;
        if (fileSize.HasValue)
        {
            titleText += $" ({FileViewerService.FormatFileSize(fileSize.Value)})";
        }

        // Title
        textPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // Tag badges row
        if (linkItem != null && linkItem.TagIds.Count > 0)
        {
            var tagsPanel = TagManagementService.Instance?.CreateTagBadgesPanel(linkItem.TagIds, fontSize: 10, spacing: 4);
            if (tagsPanel != null && tagsPanel.Children.Count > 0)
            {
                textPanel.Children.Add(tagsPanel);
            }
        }

        if (createdDate.HasValue || modifiedDate.HasValue)
        {
            var timestampParts = new List<string>();
            if (createdDate.HasValue)
            {
                timestampParts.Add($"Created: {createdDate.Value:yyyy-MM-dd HH:mm}");
            }
            if (modifiedDate.HasValue)
            {
                timestampParts.Add($"Modified: {modifiedDate.Value:yyyy-MM-dd HH:mm}");
            }

            textPanel.Children.Add(new TextBlock
            {
                Text = string.Join(" | ", timestampParts),
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        horizontalPanel.Children.Add(textPanel);
        containerGrid.Children.Add(horizontalPanel);

        if (showLinkBadge)
        {
            var linkBadge = new Border
            {
                Background = new SolidColorBrush(Colors.DodgerBlue),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 8, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = "\uE71B",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Colors.White)
                        },
                        new TextBlock
                        {
                            Text = "Link Only",
                            FontSize = 9,
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
}
