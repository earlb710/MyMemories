using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for adding or editing an IMAP email account.
/// </summary>
public class EmailAccountDialog
{
    private readonly XamlRoot _xamlRoot;

    public EmailAccountDialog(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    /// <summary>
    /// Shows a dialog to add a new email account.
    /// Returns the configured account or null if cancelled.
    /// </summary>
    public async Task<EmailAccount?> ShowAddAsync()
    {
        return await ShowDialogAsync(null);
    }

    /// <summary>
    /// Shows a dialog to edit an existing email account.
    /// Returns the updated account or null if cancelled.
    /// </summary>
    public async Task<EmailAccount?> ShowEditAsync(EmailAccount existing)
    {
        return await ShowDialogAsync(existing);
    }

    private async Task<EmailAccount?> ShowDialogAsync(EmailAccount? existing)
    {
        bool isEdit = existing != null;

        // Provider selection
        var providerCombo = new ComboBox
        {
            Header = "Email Provider",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                "Gmail",
                "Outlook / Office 365",
                "Yahoo",
                "iCloud",
                "Other (Custom IMAP)"
            },
            SelectedIndex = existing != null ? (int)existing.Provider : 0
        };

        var displayNameBox = new TextBox
        {
            Header = "Display Name",
            PlaceholderText = "e.g., My Gmail",
            Text = existing?.DisplayName ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var emailBox = new TextBox
        {
            Header = "Email Address",
            PlaceholderText = "user@gmail.com",
            Text = existing?.EmailAddress ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var usernameBox = new TextBox
        {
            Header = "Username (usually same as email)",
            PlaceholderText = "user@gmail.com",
            Text = existing?.Username ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var passwordBox = new PasswordBox
        {
            Header = "Password / App Password",
            PlaceholderText = "For Gmail, use an App Password",
            Password = existing != null ? EmailCredentialHelper.DecryptPassword(existing.EncryptedPassword) : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var serverBox = new TextBox
        {
            Header = "IMAP Server",
            PlaceholderText = "imap.gmail.com",
            Text = existing?.ImapServer ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var portBox = new NumberBox
        {
            Header = "IMAP Port",
            Value = existing?.ImapPort ?? 993,
            Minimum = 1,
            Maximum = 65535,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var sslCheckBox = new CheckBox
        {
            Content = "Use SSL/TLS",
            IsChecked = existing?.UseSsl ?? true
        };

        var gmailHint = new TextBlock
        {
            Text = "\U0001F4A1 For Gmail: Enable 2-Step Verification, then create an App Password at myaccount.google.com \u2192 Security \u2192 App passwords",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var statusText = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12
        };

        var testButton = new Button
        {
            Content = "Test Connection",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Auto-fill server settings when provider changes
        providerCombo.SelectionChanged += (s, e) =>
        {
            var provider = (EmailProvider)providerCombo.SelectedIndex;
            var (server, port, useSsl) = EmailAccountService.GetProviderSettings(provider);
            if (!string.IsNullOrEmpty(server))
            {
                serverBox.Text = server;
                portBox.Value = port;
                sslCheckBox.IsChecked = useSsl;
            }
            gmailHint.Visibility = provider == EmailProvider.Gmail ? Visibility.Visible : Visibility.Collapsed;
        };

        // Set initial server settings if adding new
        if (!isEdit)
        {
            var initialProvider = (EmailProvider)providerCombo.SelectedIndex;
            var (server, port, useSsl) = EmailAccountService.GetProviderSettings(initialProvider);
            if (!string.IsNullOrEmpty(server))
            {
                serverBox.Text = server;
                portBox.Value = port;
                sslCheckBox.IsChecked = useSsl;
            }
        }

        // Test connection handler
        testButton.Click += async (s, e) =>
        {
            testButton.IsEnabled = false;
            statusText.Text = "Testing connection...";
            statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

            var testAccount = new EmailAccount
            {
                ImapServer = serverBox.Text,
                ImapPort = (int)portBox.Value,
                UseSsl = sslCheckBox.IsChecked ?? true,
                Username = usernameBox.Text,
                EncryptedPassword = EmailCredentialHelper.EncryptPassword(passwordBox.Password)
            };

            using var service = new ImapEmailService();
            var error = await service.TestConnectionAsync(testAccount);

            if (error == null)
            {
                statusText.Text = "\u2705 Connection successful!";
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                statusText.Text = $"\u274C Connection failed: {error}";
                statusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }

            testButton.IsEnabled = true;
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                providerCombo,
                gmailHint,
                displayNameBox,
                emailBox,
                usernameBox,
                passwordBox,
                serverBox,
                portBox,
                sslCheckBox,
                testButton,
                statusText
            }
        };

        var scrollViewer = new ScrollViewer
        {
            Content = panel,
            MaxHeight = 500,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var dialog = new ContentDialog
        {
            Title = isEdit ? "Edit Email Account" : "Add Email Account",
            Content = scrollViewer,
            PrimaryButtonText = isEdit ? "Save" : "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(emailBox.Text) ||
                string.IsNullOrWhiteSpace(usernameBox.Text) ||
                string.IsNullOrWhiteSpace(passwordBox.Password) ||
                string.IsNullOrWhiteSpace(serverBox.Text))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Missing Information",
                    Content = "Please fill in all required fields (email, username, password, server).",
                    CloseButtonText = "OK",
                    XamlRoot = _xamlRoot
                };
                await errorDialog.ShowAsync();
                return null;
            }

            var account = existing ?? new EmailAccount();
            account.Provider = (EmailProvider)providerCombo.SelectedIndex;
            account.DisplayName = displayNameBox.Text;
            account.EmailAddress = emailBox.Text;
            account.Username = usernameBox.Text;
            account.EncryptedPassword = EmailCredentialHelper.EncryptPassword(passwordBox.Password);
            account.ImapServer = serverBox.Text;
            account.ImapPort = (int)portBox.Value;
            account.UseSsl = sslCheckBox.IsChecked ?? true;

            return account;
        }

        return null;
    }
}
