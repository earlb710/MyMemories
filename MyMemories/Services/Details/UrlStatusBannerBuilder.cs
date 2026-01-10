using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds URL status banners for display in the details panel.
/// </summary>
public class UrlStatusBannerBuilder
{
    private readonly StackPanel _detailsPanel;

    public UrlStatusBannerBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Shows URL status banner at the top of the details panel for non-accessible URLs.
    /// </summary>
    public void ShowUrlStatusBanner(LinkItem linkItem)
    {
        if (linkItem.UrlStatus == UrlStatus.Unknown || linkItem.UrlStatus == UrlStatus.Accessible)
        {
            return;
        }

        var (backgroundColor, borderColor, icon, statusText) = linkItem.UrlStatus switch
        {
            UrlStatus.Error => (
                Microsoft.UI.ColorHelper.FromArgb(40, 255, 193, 7),
                Colors.Gold,
                "\uE7BA",
                "Error"
            ),
            UrlStatus.NotFound => (
                Microsoft.UI.ColorHelper.FromArgb(40, 220, 53, 69),
                Colors.Red,
                "\uE711",
                "Not Found"
            ),
            _ => (
                Microsoft.UI.ColorHelper.FromArgb(40, 108, 117, 125),
                Colors.Gray,
                "\uE783",
                "Unknown"
            )
        };

        var banner = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var bannerContent = new StackPanel { Spacing = 8 };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        headerPanel.Children.Add(new FontIcon
        {
            Glyph = icon,
            FontSize = 16,
            Foreground = new SolidColorBrush(borderColor)
        });

        headerPanel.Children.Add(new TextBlock
        {
            Text = $"URL Status: {statusText}",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White)
        });

        bannerContent.Children.Add(headerPanel);

        if (!string.IsNullOrWhiteSpace(linkItem.UrlStatusMessage))
        {
            bannerContent.Children.Add(new TextBlock
            {
                Text = $"Message: {linkItem.UrlStatusMessage}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (linkItem.UrlLastChecked.HasValue)
        {
            bannerContent.Children.Add(new TextBlock
            {
                Text = $"Last checked: {linkItem.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontStyle = Windows.UI.Text.FontStyle.Italic
            });
        }

        banner.Child = bannerContent;
        _detailsPanel.Children.Insert(0, banner);
    }
}
