using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MyMemories;

public partial class MainWindow
{
    /// <summary>
    /// Creates a visual element for a tree node with icon and optional badge.
    /// </summary>
    private FrameworkElement CreateNodeContent(object content)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // For LinkItem, show icon with potential badge
        if (content is LinkItem linkItem)
        {
            // Create icon container with badge overlay
            var iconGrid = new Grid
            {
                Width = 20,
                Height = 20
            };

            // Primary icon (emoji)
            var primaryIcon = new TextBlock
            {
                Text = linkItem.GetIcon(),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconGrid.Children.Add(primaryIcon);

            // Check if folder has changed and add badge
            if (linkItem.IsDirectory && 
                !linkItem.IsCatalogEntry && 
                linkItem.LastCatalogUpdate.HasValue &&
                Directory.Exists(linkItem.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(linkItem.Url);
                    if (dirInfo.LastWriteTime > linkItem.LastCatalogUpdate.Value)
                    {
                        // Add warning badge icon
                        var badgeIcon = new FontIcon
                        {
                            Glyph = "\uE7BA", // Warning icon
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(0, 0, -2, -2)
                        };
                        
                        // Set badge color to orange/red
                        badgeIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                        
                        // Add tooltip
                        ToolTipService.SetToolTip(badgeIcon, "Folder has changed since last catalog");
                        
                        iconGrid.Children.Add(badgeIcon);
                    }
                }
                catch
                {
                    // Ignore errors accessing directory
                }
            }

            stackPanel.Children.Add(iconGrid);

            // Add text with file count if applicable
            var displayText = linkItem.Title;
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry && linkItem.CatalogFileCount > 0)
            {
                displayText = $"{linkItem.Title} ({linkItem.CatalogFileCount} file{(linkItem.CatalogFileCount != 1 ? "s" : "")})";
            }

            var textBlock = new TextBlock
            {
                Text = displayText,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }
        // For CategoryItem, just show icon and name
        else if (content is CategoryItem categoryItem)
        {
            var iconText = new TextBlock
            {
                Text = categoryItem.Icon,
                FontSize = 16
            };
            stackPanel.Children.Add(iconText);

            var textBlock = new TextBlock
            {
                Text = categoryItem.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }

        return stackPanel;
    }

    /// <summary>
    /// Refreshes the visual content of a tree node.
    /// </summary>
    public void RefreshNodeVisual(TreeViewNode node)
    {
        if (node.Content != null)
        {
            // Force update by recreating the visual
            var content = node.Content;
            node.Content = null;
            node.Content = content;
        }
    }

    /// <summary>
    /// Finds a parent element of a specific type in the visual tree.
    /// </summary>
    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

        while (parentObject != null)
        {
            if (parentObject is T parent)
                return parent;

            parentObject = VisualTreeHelper.GetParent(parentObject);
        }

        return null;
    }
}