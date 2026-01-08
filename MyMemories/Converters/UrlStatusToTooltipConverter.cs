using Microsoft.UI.Xaml.Data;
using System;

namespace MyMemories;

/// <summary>
/// Converts UrlStatus enum to tooltip text.
/// </summary>
public class UrlStatusToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is UrlStatus status)
        {
            return status switch
            {
                UrlStatus.Accessible => "URL is accessible",
                UrlStatus.Error => "URL returned an error",
                UrlStatus.NotFound => "URL not found",
                UrlStatus.Unknown => "URL status unknown",
                _ => "URL status unknown"
            };
        }
        return "URL status unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
