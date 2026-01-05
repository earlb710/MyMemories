using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System; // Add this for AsTask() extension

namespace MyMemories.Utilities;

public static class DialogUtilities
{
    /// <summary>
    /// Shows a simple error dialog.
    /// </summary>
    public static async Task ShowErrorAsync(
        XamlRoot xamlRoot, 
        string title, 
        string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync().AsTask(); // Add .AsTask()
    }

    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons.
    /// Returns true if user clicked Yes.
    /// </summary>
    public static async Task<bool> ShowConfirmationAsync(
        XamlRoot xamlRoot, 
        string title, 
        string message,
        string primaryButtonText = "Yes",
        string closeButtonText = "No")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        
        var result = await dialog.ShowAsync().AsTask(); // Add .AsTask()
        return result == ContentDialogResult.Primary;
    }
}