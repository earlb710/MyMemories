using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace MyMemories.Converters;

/// <summary>
/// Converter that returns red color for the "Archived" node, otherwise returns default foreground.
/// </summary>
public class ArchiveNodeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
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
