using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace MyMemories;

/// <summary>
/// Converts PasswordProtectionType to a background color brush.
/// GlobalPassword -> Black, OwnPassword -> Dark Red
/// </summary>
public class PasswordProtectionToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PasswordProtectionType passwordType)
        {
            return passwordType switch
            {
                PasswordProtectionType.GlobalPassword => new SolidColorBrush(Colors.Black),
                PasswordProtectionType.OwnPassword => new SolidColorBrush(Color.FromArgb(255, 139, 0, 0)), // Dark red
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
