using Microsoft.UI.Xaml.Data;
using System;

namespace MyMemories;

/// <summary>
/// Converts UrlStatus enum to tooltip text with additional details.
/// </summary>
public class UrlStatusToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is UrlStatus status)
        {
            return status switch
            {
                UrlStatus.Accessible => "? URL is accessible\n\nClick to view the webpage",
                UrlStatus.Error => "? URL returned an error\n\nThe server responded with an error status.\nHover over the link item for details.",
                UrlStatus.NotFound => "? URL not found\n\nThe page does not exist or the server is unreachable.\nHover over the link item for details.",
                UrlStatus.Unknown => "? URL status unknown\n\nClick 'Refresh URL State' on the category\nto check URL accessibility.",
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
