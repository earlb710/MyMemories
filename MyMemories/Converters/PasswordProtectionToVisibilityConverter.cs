using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace MyMemories;

/// <summary>
/// Converts PasswordProtectionType to Visibility.
/// Shows the badge if password protection is enabled (GlobalPassword or OwnPassword).
/// </summary>
public class PasswordProtectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PasswordProtectionType protectionType)
        {
            return protectionType != PasswordProtectionType.None 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
