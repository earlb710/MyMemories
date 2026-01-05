using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MyMemories;

public sealed partial class MainWindow
{
    private bool _isDraggingSplitter = false;
    private double _startX = 0;
    private double _startWidth = 0;

    private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border splitter)
        {
            _isDraggingSplitter = true;
            
            var pointerPoint = e.GetCurrentPoint(this.Content as UIElement);
            _startX = pointerPoint.Position.X;
            _startWidth = TreeViewColumn.ActualWidth;
            
            splitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter)
        {
            var pointerPoint = e.GetCurrentPoint(this.Content as UIElement);
            var currentX = pointerPoint.Position.X;
            var deltaX = currentX - _startX;
            var newWidth = _startWidth + deltaX;
            
            if (newWidth >= TreeViewColumn.MinWidth && newWidth <= TreeViewColumn.MaxWidth)
            {
                TreeViewColumn.Width = new GridLength(newWidth);
            }
            
            e.Handled = true;
        }
    }

    private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter && sender is Border splitter)
        {
            _isDraggingSplitter = false;
            splitter.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border splitter)
        {
            splitter.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.LightGray) { Opacity = 0.3 };
        }
    }

    private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border splitter && !_isDraggingSplitter)
        {
            splitter.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);
        }
    }
}