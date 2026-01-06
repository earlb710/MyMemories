using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Helper methods for creating dialog UI elements.
/// </summary>
public static class DialogHelpers
{
    /// <summary>
    /// Creates a label text block with consistent styling.
    /// </summary>
    public static TextBlock CreateLabel(string text, Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = margin ?? new Thickness(0, 0, 0, 4)
        };
    }

    /// <summary>
    /// Shows an error dialog with the specified title and message.
    /// </summary>
    public static async Task ShowErrorAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }
}