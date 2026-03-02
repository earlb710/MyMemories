using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Dialogs;
using MyMemories.Services;
using System;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Partial class for email-related functionality in MainWindow.
/// Handles email account management and email browser integration.
/// </summary>
public sealed partial class MainWindow
{
    private EmailAccountService? _emailAccountService;

    /// <summary>
    /// Initializes the email account service. Called during app initialization.
    /// </summary>
    private async Task InitializeEmailServiceAsync()
    {
        if (_configService == null) return;

        _emailAccountService = new EmailAccountService(_configService.WorkingDirectory);
        await _emailAccountService.LoadAsync();
    }

    /// <summary>
    /// Opens the email browser dialog for browsing, searching, and archiving emails.
    /// </summary>
    private async void MenuFile_EmailBrowser_Click(object sender, RoutedEventArgs e)
    {
        await ShowEmailBrowserAsync();
    }

    /// <summary>
    /// Opens the email account management dialog.
    /// </summary>
    private async void MenuConfig_EmailAccounts_Click(object sender, RoutedEventArgs e)
    {
        await ShowEmailAccountManagementAsync();
    }

    private async Task ShowEmailBrowserAsync()
    {
        if (_emailAccountService == null)
        {
            await InitializeEmailServiceAsync();
        }

        if (_emailAccountService == null)
        {
            await ShowErrorDialogAsync("Error", "Email service could not be initialized.");
            return;
        }

        var browser = new EmailBrowserDialog(Content.XamlRoot, this, _emailAccountService, _configService!.WorkingDirectory);
        await browser.ShowAsync();
    }

    private async Task ShowEmailAccountManagementAsync()
    {
        if (_emailAccountService == null)
        {
            await InitializeEmailServiceAsync();
        }

        if (_emailAccountService == null)
        {
            await ShowErrorDialogAsync("Error", "Email service could not be initialized.");
            return;
        }

        while (true)
        {
            var accounts = _emailAccountService.GetAccounts();

            var listPanel = new StackPanel { Spacing = 8, MinWidth = 400 };

            if (accounts.Count == 0)
            {
                listPanel.Children.Add(new TextBlock
                {
                    Text = "No email accounts configured.",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
            else
            {
                foreach (var acct in accounts)
                {
                    var acctPanel = new Grid();
                    acctPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    acctPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    acctPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var infoPanel = new StackPanel();
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = acct.DisplayName,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"{acct.EmailAddress} ({acct.Provider})",
                        FontSize = 12,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                    });
                    if (acct.LastConnectedDate.HasValue)
                    {
                        infoPanel.Children.Add(new TextBlock
                        {
                            Text = $"Last connected: {acct.LastConnectedDate.Value:yyyy-MM-dd HH:mm}",
                            FontSize = 11,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                        });
                    }

                    var editBtn = new Button
                    {
                        Content = new FontIcon { Glyph = "\uE70F", FontSize = 12 },
                        Padding = new Thickness(6),
                        Margin = new Thickness(4, 0, 0, 0),
                        Tag = acct.Id
                    };
                    editBtn.Click += async (s, args) =>
                    {
                        var accountId = (string)((Button)s).Tag;
                        var account = _emailAccountService.GetAccount(accountId);
                        if (account != null)
                        {
                            var dialog = new EmailAccountDialog(Content.XamlRoot);
                            var updated = await dialog.ShowEditAsync(account);
                            if (updated != null)
                            {
                                await _emailAccountService.UpdateAccountAsync(updated);
                            }
                        }
                    };

                    var deleteBtn = new Button
                    {
                        Content = new FontIcon { Glyph = "\uE74D", FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) },
                        Padding = new Thickness(6),
                        Margin = new Thickness(4, 0, 0, 0),
                        Tag = acct.Id
                    };
                    deleteBtn.Click += async (s, args) =>
                    {
                        var accountId = (string)((Button)s).Tag;
                        var confirmDialog = new ContentDialog
                        {
                            Title = "Remove Account",
                            Content = $"Remove email account '{acct.DisplayName}'?\n\nThis only removes the account from MyMemories. Your emails will not be affected.",
                            PrimaryButtonText = "Remove",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Close,
                            XamlRoot = Content.XamlRoot
                        };
                        var confirmResult = await confirmDialog.ShowAsync();
                        if (confirmResult == ContentDialogResult.Primary)
                        {
                            await _emailAccountService.RemoveAccountAsync(accountId);
                        }
                    };

                    Grid.SetColumn(infoPanel, 0);
                    Grid.SetColumn(editBtn, 1);
                    Grid.SetColumn(deleteBtn, 2);
                    acctPanel.Children.Add(infoPanel);
                    acctPanel.Children.Add(editBtn);
                    acctPanel.Children.Add(deleteBtn);

                    var border = new Border
                    {
                        Child = acctPanel,
                        Padding = new Thickness(12, 8, 12, 8),
                        BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4)
                    };
                    listPanel.Children.Add(border);
                }
            }

            var scrollViewer = new ScrollViewer
            {
                Content = listPanel,
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var managementDialog = new ContentDialog
            {
                Title = "\U0001F4E7 Email Accounts",
                Content = scrollViewer,
                PrimaryButtonText = "Add Account",
                CloseButtonText = "Done",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await managementDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var addDialog = new EmailAccountDialog(Content.XamlRoot);
                var newAccount = await addDialog.ShowAddAsync();
                if (newAccount != null)
                {
                    await _emailAccountService.AddAccountAsync(newAccount);
                }
                // Loop back to show the updated list
                continue;
            }

            break; // User clicked "Done"
        }
    }
}
