using Microsoft.UI.Xaml.Data;
using System;

namespace MyMemories;

/// <summary>
/// Converts PasswordProtectionType to a descriptive tooltip string.
/// </summary>
public class PasswordProtectionToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PasswordProtectionType passwordType)
        {
            return passwordType switch
            {
                PasswordProtectionType.GlobalPassword => "Protected with global password",
                PasswordProtectionType.OwnPassword => "Protected with own password",
                _ => "Not protected"
            };
        }

        return "Not protected";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
