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

    /// <summary>
    /// Event raised when the user requests to update a URL to its redirect target.
    /// </summary>
    public event Action<LinkItem>? UpdateUrlFromRedirectRequested;

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

        // Ratings row
        if (category != null && category.Ratings.Count > 0)
        {
            var ratingsPanel = CreateRatingsDisplayPanel(category.Ratings);
            if (ratingsPanel != null)
            {
                textPanel.Children.Add(ratingsPanel);
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

        // Title row with redirect button if applicable
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        titleRow.Children.Add(new TextBlock
        {
            Text = titleText,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Add redirect button if link has a redirect
        if (linkItem != null && linkItem.HasRedirect)
        {
            var redirectButton = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE72A", FontSize = 12 }, // Forward arrow
                        new TextBlock { Text = "Redirect", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Padding = new Thickness(6, 2, 6, 2),
                Background = new SolidColorBrush(Colors.DodgerBlue),
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };

            ToolTipService.SetToolTip(redirectButton, $"This URL redirects to:\n{linkItem.RedirectUrl}\n\nClick to update the bookmark URL.");

            redirectButton.Click += async (s, e) =>
            {
                await ShowRedirectUpdateDialogAsync(linkItem);
            };

            titleRow.Children.Add(redirectButton);
        }

        textPanel.Children.Add(titleRow);

        // Tag badges row
        if (linkItem != null && linkItem.TagIds.Count > 0)
        {
            var tagsPanel = TagManagementService.Instance?.CreateTagBadgesPanel(linkItem.TagIds, fontSize: 10, spacing: 4);
            if (tagsPanel != null && tagsPanel.Children.Count > 0)
            {
                textPanel.Children.Add(tagsPanel);
            }
        }

        // Ratings row
        if (linkItem != null && linkItem.Ratings.Count > 0)
        {
            var ratingsPanel = CreateRatingsDisplayPanel(linkItem.Ratings);
            if (ratingsPanel != null)
            {
                textPanel.Children.Add(ratingsPanel);
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

    /// <summary>
    /// Creates a panel displaying ratings with their scores.
    /// </summary>
    private StackPanel? CreateRatingsDisplayPanel(List<RatingValue> ratings)
    {
        if (ratings == null || ratings.Count == 0)
            return null;

        var ratingService = RatingManagementService.Instance;
        if (ratingService == null)
            return null;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 2, 0, 0)
        };

        foreach (var rating in ratings)
        {
            var definition = ratingService.GetDefinition(rating.RatingDefinitionId);
            if (definition == null)
                continue;

            var backgroundColor = RatingManagementService.GetScoreColor(rating.Score);
            var ratingBadge = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            // Rating icon based on score
            content.Children.Add(new FontIcon
            {
                Glyph = RatingManagementService.GetScoreIconGlyph(rating.Score),
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White)
            });

            // Rating name and score
            content.Children.Add(new TextBlock
            {
                Text = $"{definition.Name}: {RatingManagementService.FormatScore(rating.Score)}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            });

            ratingBadge.Child = content;

            // Add tooltip with reason if available
            if (!string.IsNullOrWhiteSpace(rating.Reason))
            {
                ToolTipService.SetToolTip(ratingBadge, $"{definition.Description}\n\nReason: {rating.Reason}");
            }
            else if (!string.IsNullOrWhiteSpace(definition.Description))
            {
                ToolTipService.SetToolTip(ratingBadge, definition.Description);
            }

            panel.Children.Add(ratingBadge);
        }

        return panel.Children.Count > 0 ? panel : null;
    }

    /// <summary>
    /// Parses a hex color string to a Color.
    /// </summary>
    private static Windows.UI.Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return Windows.UI.Color.FromArgb(
                    255,
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
        }
        catch { }
        
        return Colors.Gold;
    }

    /// <summary>
    /// Shows a dialog with redirect information and option to update the URL.
    /// </summary>
    private async Task ShowRedirectUpdateDialogAsync(LinkItem linkItem)
    {
        if (linkItem == null || !linkItem.HasRedirect)
            return;

        var contentPanel = new StackPanel { Spacing = 12, MinWidth = 400 };

        // Info section
        contentPanel.Children.Add(new TextBlock
        {
            Text = "A redirect was detected for this bookmark:",
            TextWrapping = TextWrapping.Wrap
        });

        // Original URL
        var originalSection = new StackPanel { Spacing = 4 };
        originalSection.Children.Add(new TextBlock
        {
            Text = "Original URL:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12
        });
        originalSection.Children.Add(new TextBox
        {
            Text = linkItem.Url,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11
        });
        contentPanel.Children.Add(originalSection);

        // Arrow indicator
        contentPanel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new FontIcon { Glyph = "\uE74B", FontSize = 20, Foreground = new SolidColorBrush(Colors.DodgerBlue) } // Down arrow
            }
        });

        // Redirect URL
        var redirectSection = new StackPanel { Spacing = 4 };
        redirectSection.Children.Add(new TextBlock
        {
            Text = "Redirects to:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.LimeGreen)
        });
        redirectSection.Children.Add(new TextBox
        {
            Text = linkItem.RedirectUrl ?? "",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11
        });
        contentPanel.Children.Add(redirectSection);

        // Last checked info
        if (linkItem.UrlLastChecked.HasValue)
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = $"Detected: {linkItem.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray),
                FontStyle = Windows.UI.Text.FontStyle.Italic
            });
        }

        var dialog = new ContentDialog
        {
            Title = "URL Redirect Detected",
            Content = contentPanel,
            PrimaryButtonText = "Update URL",
            CloseButtonText = "Keep Original",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _headerPanel.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Directly update the URL here instead of raising an event
            var oldUrl = linkItem.Url;
            linkItem.Url = linkItem.RedirectUrl!;
            linkItem.RedirectUrl = null; // Clear the redirect since we've updated
            linkItem.ModifiedDate = DateTime.Now;
            
            // Raise event to save the changes
            UpdateUrlFromRedirectRequested?.Invoke(linkItem);
        }
    }
}
