using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using MyMemories.Utilities;
using System;
using System.IO;

namespace MyMemories.Converters;

/// <summary>
/// Converter that creates a complete tooltip for categories including file path for root categories.
/// </summary>
public class CategoryTooltipConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TreeViewNode node || node.Content is not CategoryItem category)
            return null;

        bool isRootCategory = node.Parent == null;
        
        var tooltipPanel = new StackPanel
        {
            Spacing = 4,
            MaxWidth = 400
        };

        // Category name
        tooltipPanel.Children.Add(new TextBlock
        {
            Text = category.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // Description
        if (!string.IsNullOrWhiteSpace(category.Description))
        {
            tooltipPanel.Children.Add(new TextBlock
            {
                Text = category.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
        }

        // Tags
        if (!string.IsNullOrWhiteSpace(category.TagDisplayText))
        {
            tooltipPanel.Children.Add(new TextBlock
            {
                Text = category.TagDisplayText,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
        }

        // File location for root categories
        if (isRootCategory)
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMemories",
                "Categories");

            var fileName = FileUtilities.SanitizeFileName(category.Name) + ".json";
            var filePath = Path.Combine(appDataFolder, fileName);

            // Add separator
            tooltipPanel.Children.Add(new Border
            {
                Height = 1,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Opacity = 0.3,
                Margin = new Thickness(0, 4, 0, 4)
            });

            // File name
            var fileNamePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            fileNamePanel.Children.Add(new FontIcon
            {
                Glyph = "\uE8A5", // Document icon
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGray)
            });

            fileNamePanel.Children.Add(new TextBlock
            {
                Text = fileName,
                FontSize = 11,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGray)
            });

            tooltipPanel.Children.Add(fileNamePanel);

            // Full path
            var pathPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            pathPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE8B7", // Folder icon
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });

            pathPanel.Children.Add(new TextBlock
            {
                Text = filePath,
                FontSize = 10,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });

            tooltipPanel.Children.Add(pathPanel);
        }

        return tooltipPanel;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
