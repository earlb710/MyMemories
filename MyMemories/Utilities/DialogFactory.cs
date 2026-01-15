using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Utilities;

/// <summary>
/// Factory class for creating common dialog patterns with consistent styling.
/// Reduces code duplication across the application.
/// </summary>
public static class DialogFactory
{
    /// <summary>
    /// Creates a simple confirmation dialog with Yes/No buttons.
    /// </summary>
    public static ContentDialog CreateConfirmationDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string primaryButtonText = "Yes",
        string closeButtonText = "No")
    {
        return new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
    }

    /// <summary>
    /// Shows a confirmation dialog and returns true if user clicked Yes/Primary.
    /// </summary>
    public static async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string primaryButtonText = "Yes",
        string closeButtonText = "No")
    {
        var dialog = CreateConfirmationDialog(title, message, xamlRoot, primaryButtonText, closeButtonText);
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Creates an error dialog with OK button.
    /// </summary>
    public static ContentDialog CreateErrorDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        return new ContentDialog
        {
            Title = $"? {title}",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            CloseButtonText = buttonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
    }

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    public static async Task ShowErrorAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        var dialog = CreateErrorDialog(title, message, xamlRoot, buttonText);
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Creates an information dialog with OK button.
    /// </summary>
    public static ContentDialog CreateInfoDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        return new ContentDialog
        {
            Title = $"?? {title}",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            CloseButtonText = buttonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
    }

    /// <summary>
    /// Shows an information dialog.
    /// </summary>
    public static async Task ShowInfoAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        var dialog = CreateInfoDialog(title, message, xamlRoot, buttonText);
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Creates a warning dialog with OK button.
    /// </summary>
    public static ContentDialog CreateWarningDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        return new ContentDialog
        {
            Title = $"?? {title}",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Orange)
            },
            CloseButtonText = buttonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
    }

    /// <summary>
    /// Shows a warning dialog.
    /// </summary>
    public static async Task ShowWarningAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        var dialog = CreateWarningDialog(title, message, xamlRoot, buttonText);
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Creates a success dialog with OK button.
    /// </summary>
    public static ContentDialog CreateSuccessDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        return new ContentDialog
        {
            Title = $"? {title}",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            CloseButtonText = buttonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
    }

    /// <summary>
    /// Shows a success dialog.
    /// </summary>
    public static async Task ShowSuccessAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string buttonText = "OK")
    {
        var dialog = CreateSuccessDialog(title, message, xamlRoot, buttonText);
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Creates a dialog with custom content.
    /// </summary>
    public static ContentDialog CreateCustomDialog(
        string title,
        UIElement content,
        XamlRoot xamlRoot,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = "Cancel",
        ContentDialogButton defaultButton = ContentDialogButton.Close)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            DefaultButton = defaultButton,
            XamlRoot = xamlRoot
        };

        if (!string.IsNullOrEmpty(primaryButtonText))
            dialog.PrimaryButtonText = primaryButtonText;

        if (!string.IsNullOrEmpty(secondaryButtonText))
            dialog.SecondaryButtonText = secondaryButtonText;

        if (!string.IsNullOrEmpty(closeButtonText))
            dialog.CloseButtonText = closeButtonText;

        return dialog;
    }

    /// <summary>
    /// Creates a text input dialog with a TextBox.
    /// </summary>
    public static (ContentDialog dialog, TextBox textBox) CreateTextInputDialog(
        string title,
        string message,
        string placeholderText,
        string defaultValue,
        XamlRoot xamlRoot,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel")
    {
        var textBox = new TextBox
        {
            PlaceholderText = placeholderText,
            Text = defaultValue,
            AcceptsReturn = false
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                textBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        return (dialog, textBox);
    }

    /// <summary>
    /// Shows a text input dialog and returns the entered text (or null if cancelled).
    /// </summary>
    public static async Task<string?> ShowTextInputAsync(
        string title,
        string message,
        string placeholderText,
        string defaultValue,
        XamlRoot xamlRoot,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel")
    {
        var (dialog, textBox) = CreateTextInputDialog(
            title, message, placeholderText, defaultValue, 
            xamlRoot, primaryButtonText, closeButtonText);

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    /// <summary>
    /// Creates a dialog with a list of items to choose from.
    /// </summary>
    public static (ContentDialog dialog, ListView listView) CreateListSelectionDialog<T>(
        string title,
        string message,
        IEnumerable<T> items,
        XamlRoot xamlRoot,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel",
        bool multiSelect = false) where T : class
    {
        var listView = new ListView
        {
            ItemsSource = items.ToList(),
            SelectionMode = multiSelect ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single,
            MaxHeight = 400
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                listView
            }
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        return (dialog, listView);
    }

    /// <summary>
    /// Creates a password input dialog.
    /// </summary>
    public static (ContentDialog dialog, PasswordBox passwordBox) CreatePasswordDialog(
        string title,
        string message,
        XamlRoot xamlRoot,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel",
        string placeholderText = "Enter password")
    {
        var passwordBox = new PasswordBox
        {
            PlaceholderText = placeholderText
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                passwordBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        return (dialog, passwordBox);
    }

    /// <summary>
    /// Shows a password dialog and returns the entered password (or null if cancelled).
    /// </summary>
    public static async Task<string?> ShowPasswordDialogAsync(
        string title,
        string message,
        XamlRoot xamlRoot,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel",
        string placeholderText = "Enter password")
    {
        var (dialog, passwordBox) = CreatePasswordDialog(
            title, message, xamlRoot, primaryButtonText, 
            closeButtonText, placeholderText);

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? passwordBox.Password : null;
    }

    /// <summary>
    /// Creates a progress dialog (non-interactive, for showing progress).
    /// </summary>
    public static ContentDialog CreateProgressDialog(
        string title,
        string message,
        XamlRoot xamlRoot)
    {
        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 50,
            Height = 50
        };

        var panel = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                progressRing,
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

        return new ContentDialog
        {
            Title = title,
            Content = panel,
            XamlRoot = xamlRoot
            // No buttons - this is for showing progress
        };
    }
}
