using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories;

public sealed partial class MainWindow
{
    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(child);
        return parent switch
        {
            null => null,
            T typedParent => typedParent,
            _ => FindParent<T>(parent)
        };
    }
}