using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using MyMemories.Services;
using System;
using System.Collections.Generic;

namespace MyMemories;

/// <summary>
/// Converts a list of tag IDs to a StackPanel with colored tag icon badges.
/// </summary>
public class TagIdsToIconsPanelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IEnumerable<string> tagIds)
        {
            var tagService = TagManagementService.Instance;
            if (tagService != null)
            {
                return tagService.CreateTagIconsPanel(tagIds, iconSize: 10, spacing: 2);
            }
        }

        // Return an empty panel if no tags or service not available
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
