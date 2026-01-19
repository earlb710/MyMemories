using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace MyMemories.Converters;

/// <summary>
/// Converter that returns special colors for system nodes:
/// - Red for Archive-related nodes (the main "Archived (n)" node, archived items)
/// - Blue for Searches node
/// </summary>
public class ArchiveNodeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Check if value is CategoryItem with special flags
        if (value is CategoryItem category)
        {
            // Blue for Searches node
            if (category.IsSearchesNode)
            {
                return new SolidColorBrush(Colors.DodgerBlue);
            }
            
            // Red for: Archive node, or any item with ArchivedDate, or icon is "A" (archived rating)
            if (category.IsArchiveNode || category.ArchivedDate.HasValue || category.Icon == "A")
            {
                return new SolidColorBrush(Colors.Red);
            }
        }
        
        // Also check by name for backward compatibility
        if (value is string name && name.StartsWith("Archived"))
        {
            // Return red color for Archive node (name includes count: "Archived (n)")
            return new SolidColorBrush(Colors.Red);
        }
        
        // Return default foreground color for all other nodes
        return new SolidColorBrush(Colors.Black); // Will use theme color in practice
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
