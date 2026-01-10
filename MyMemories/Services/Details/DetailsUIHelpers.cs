using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MyMemories.Services.Details;

/// <summary>
/// Static helper methods for building details panel UI elements.
/// </summary>
public static class DetailsUIHelpers
{
    /// <summary>
    /// Creates a stat line text block.
    /// </summary>
    public static TextBlock CreateStatLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14
        };
    }

    /// <summary>
    /// Adds a section with title and content to the details panel.
    /// </summary>
    public static void AddSection(StackPanel detailsPanel, string title, string content, bool isGrayedOut = false, bool isSelectable = false)
    {
        detailsPanel.Children.Add(new TextBlock
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

        detailsPanel.Children.Add(contentTextBlock);
    }

    /// <summary>
    /// Adds a warning message to the details panel.
    /// </summary>
    public static void AddWarning(StackPanel detailsPanel, string message)
    {
        detailsPanel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Orange),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
    }

    /// <summary>
    /// Creates a link card for displaying a link item in a list.
    /// </summary>
    public static Border CreateLinkCard(LinkItem link)
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

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        titleRow.Children.Add(new TextBlock
        {
            Text = link.ToString(),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        if (link.TagIds.Count > 0)
        {
            var tagText = TagManagementService.Instance?.GetTagDisplayText(link.TagIds);
            if (!string.IsNullOrEmpty(tagText))
            {
                titleRow.Children.Add(new TextBlock
                {
                    Text = tagText,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.DodgerBlue)
                });
            }
        }

        linkInfo.Children.Add(titleRow);

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
