using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for browsing emails via IMAP, viewing messages, searching, and archiving to local storage.
/// </summary>
public class EmailBrowserDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly Window _window;
    private readonly EmailAccountService _accountService;
    private readonly string _workingDirectory;
    private ImapEmailService? _imapService;
    private EmailAccount? _currentAccount;
    private string _currentFolderName = "INBOX";
    private CancellationTokenSource? _cts;

    // UI elements we need to reference
    private ComboBox? _accountCombo;
    private TreeView? _folderTree;
    private ListView? _messageList;
    private WebView2? _messageViewer;
    private TextBlock? _statusText;
    private TextBlock? _messageCountText;
    private ProgressRing? _loadingRing;
    private Button? _archiveButton;
    private TextBox? _searchBox;
    private StackPanel? _messageDetailPanel;
    private TextBlock? _subjectText;
    private TextBlock? _fromText;
    private TextBlock? _dateText;

    private readonly ObservableCollection<EmailMessageSummary> _messages = new();
    private readonly List<EmailFolder> _folders = new();

    public EmailBrowserDialog(XamlRoot xamlRoot, Window window, EmailAccountService accountService, string workingDirectory)
    {
        _xamlRoot = xamlRoot;
        _window = window;
        _accountService = accountService;
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Shows the email browser dialog.
    /// </summary>
    public async Task ShowAsync()
    {
        var accounts = _accountService.GetAccounts();
        if (accounts.Count == 0)
        {
            var noAccountDialog = new ContentDialog
            {
                Title = "No Email Accounts",
                Content = "No email accounts configured. Would you like to add one now?",
                PrimaryButtonText = "Add Account",
                CloseButtonText = "Cancel",
                XamlRoot = _xamlRoot
            };

            var result = await noAccountDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var accountDialog = new EmailAccountDialog(_xamlRoot);
                var newAccount = await accountDialog.ShowAddAsync();
                if (newAccount != null)
                {
                    await _accountService.AddAccountAsync(newAccount);
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        await ShowBrowserDialogAsync();
    }

    private async Task ShowBrowserDialogAsync()
    {
        var accounts = _accountService.GetAccounts();

        // Build the UI
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // --- Top bar: account selector + search ---
        var topBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumnSpan(topBar, 2);

        _accountCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Select account...",
            Margin = new Thickness(0, 0, 8, 0)
        };
        foreach (var acct in accounts)
        {
            _accountCombo.Items.Add(new ComboBoxItem { Content = acct.ToString(), Tag = acct.Id });
        }
        if (accounts.Count > 0) _accountCombo.SelectedIndex = 0;

        _searchBox = new TextBox
        {
            PlaceholderText = "Search emails (subject, from, body)...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 4, 0)
        };
        _searchBox.KeyDown += SearchBox_KeyDown;

        var searchButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE721", FontSize = 14 },
            VerticalAlignment = VerticalAlignment.Center
        };
        searchButton.Click += SearchButton_Click;

        Grid.SetColumn(_accountCombo, 0);
        Grid.SetColumn(_searchBox, 1);
        Grid.SetColumn(searchButton, 2);
        topBar.Children.Add(_accountCombo);
        topBar.Children.Add(_searchBox);
        topBar.Children.Add(searchButton);
        mainGrid.Children.Add(topBar);

        // --- Left panel: folder tree ---
        _folderTree = new TreeView
        {
            Margin = new Thickness(0, 0, 8, 0),
            SelectionMode = TreeViewSelectionMode.Single
        };
        _folderTree.SelectionChanged += FolderTree_SelectionChanged;
        Grid.SetRow(_folderTree, 1);
        Grid.SetColumn(_folderTree, 0);
        mainGrid.Children.Add(_folderTree);

        // --- Right panel: message list + preview ---
        var rightPanel = new Grid();
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(rightPanel, 1);
        Grid.SetColumn(rightPanel, 1);

        // Message list
        _messageList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Extended,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _messageList.ItemsSource = _messages;
        _messageList.SelectionChanged += MessageList_SelectionChanged;
        _messageList.ContainerContentChanging += MessageList_ContainerContentChanging;
        rightPanel.Children.Add(_messageList);

        // Splitter
        var splitter = new Border
        {
            Height = 4,
            Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            Margin = new Thickness(0, 4, 0, 4)
        };
        Grid.SetRow(splitter, 1);
        rightPanel.Children.Add(splitter);

        // Message preview area
        var previewPanel = new Grid();
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(previewPanel, 2);

        _messageDetailPanel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 4) };
        _subjectText = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
        _fromText = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
        _dateText = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
        _messageDetailPanel.Children.Add(_subjectText);
        _messageDetailPanel.Children.Add(_fromText);
        _messageDetailPanel.Children.Add(_dateText);
        _messageDetailPanel.Visibility = Visibility.Collapsed;
        previewPanel.Children.Add(_messageDetailPanel);

        _messageViewer = new WebView2 { Visibility = Visibility.Collapsed };
        Grid.SetRow(_messageViewer, 1);
        previewPanel.Children.Add(_messageViewer);

        rightPanel.Children.Add(previewPanel);
        mainGrid.Children.Add(rightPanel);

        // --- Bottom bar: status + actions ---
        var bottomBar = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(bottomBar, 2);
        Grid.SetColumnSpan(bottomBar, 2);

        _loadingRing = new ProgressRing { IsActive = false, Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0) };
        _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        _messageCountText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };

        _archiveButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE896", FontSize = 14 },
                    new TextBlock { Text = "Archive Selected", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = false
        };
        _archiveButton.Click += ArchiveButton_Click;

        Grid.SetColumn(_loadingRing, 0);
        Grid.SetColumn(_statusText, 1);
        Grid.SetColumn(_messageCountText, 2);
        Grid.SetColumn(_archiveButton, 3);
        bottomBar.Children.Add(_loadingRing);
        bottomBar.Children.Add(_statusText);
        bottomBar.Children.Add(_messageCountText);
        bottomBar.Children.Add(_archiveButton);
        mainGrid.Children.Add(bottomBar);

        _accountCombo.SelectionChanged += AccountCombo_SelectionChanged;

        var dialog = new ContentDialog
        {
            Title = "\U0001F4E7 Email Browser",
            Content = mainGrid,
            CloseButtonText = "Close",
            XamlRoot = _xamlRoot,
            FullSizeDesired = true
        };

        // Initialize WebView2 before showing
        try
        {
            await _messageViewer.EnsureCoreWebView2Async();
        }
        catch
        {
            // WebView2 may not be available
        }

        // Auto-connect if an account is selected
        if (_accountCombo.SelectedIndex >= 0)
        {
            _ = ConnectToSelectedAccountAsync();
        }

        await dialog.ShowAsync();

        // Cleanup on close
        _cts?.Cancel();
        if (_imapService != null)
        {
            try { await _imapService.DisconnectAsync(); } catch { }
            _imapService.Dispose();
        }
    }

    private async Task ConnectToSelectedAccountAsync()
    {
        if (_accountCombo?.SelectedItem is not ComboBoxItem selected || selected.Tag is not string accountId)
            return;

        var account = _accountService.GetAccount(accountId);
        if (account == null) return;

        _currentAccount = account;
        SetLoading(true, $"Connecting to {account.EmailAddress}...");

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (_imapService != null)
            {
                try { await _imapService.DisconnectAsync(); } catch { }
                _imapService.Dispose();
            }

            _imapService = new ImapEmailService();
            await _imapService.ConnectAsync(account, _cts.Token);

            account.LastConnectedDate = DateTime.Now;
            await _accountService.UpdateAccountAsync(account);

            SetStatus($"Connected to {account.EmailAddress}");
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadFoldersAsync()
    {
        if (_imapService == null || _folderTree == null) return;

        try
        {
            _cts ??= new CancellationTokenSource();
            _folders.Clear();
            _folderTree.RootNodes.Clear();

            var folders = await _imapService.GetFoldersAsync(_cts.Token);
            _folders.AddRange(folders);

            foreach (var folder in folders)
            {
                var node = CreateFolderNode(folder);
                _folderTree.RootNodes.Add(node);
            }

            // Select INBOX by default
            if (_folderTree.RootNodes.Count > 0)
            {
                _folderTree.SelectedNode = _folderTree.RootNodes[0];
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load folders: {ex.Message}");
        }
    }

    private TreeViewNode CreateFolderNode(EmailFolder folder)
    {
        var node = new TreeViewNode { Content = folder, IsExpanded = false };

        foreach (var sub in folder.SubFolders)
        {
            node.Children.Add(CreateFolderNode(sub));
        }

        return node;
    }

    private async Task LoadMessagesAsync(string folderFullName, int startIndex = 0, int count = 50)
    {
        if (_imapService == null) return;

        SetLoading(true, $"Loading messages from {folderFullName}...");
        _currentFolderName = folderFullName;

        try
        {
            _cts ??= new CancellationTokenSource();
            _messages.Clear();

            var summaries = await _imapService.GetMessageSummariesAsync(
                folderFullName, startIndex, count, _cts.Token);

            foreach (var msg in summaries)
            {
                _messages.Add(msg);
            }

            _messageCountText!.Text = $"{_messages.Count} message(s)";
            SetStatus($"Loaded {_messages.Count} messages from {folderFullName}");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load messages: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadMessagePreviewAsync(EmailMessageSummary summary)
    {
        if (_imapService == null || _messageViewer == null) return;

        try
        {
            _cts ??= new CancellationTokenSource();
            var detail = await _imapService.GetMessageDetailAsync(
                summary.FolderFullName, summary.UniqueId, _cts.Token);

            if (detail == null) return;

            _subjectText!.Text = detail.Subject;
            _fromText!.Text = $"From: {detail.From}";
            _dateText!.Text = $"Date: {detail.Date:yyyy-MM-dd HH:mm}";
            if (detail.Attachments.Count > 0)
            {
                _dateText.Text += $"  |  {detail.Attachments.Count} attachment(s)";
            }
            _messageDetailPanel!.Visibility = Visibility.Visible;

            // Show HTML body if available, otherwise text
            if (!string.IsNullOrEmpty(detail.HtmlBody))
            {
                _messageViewer.NavigateToString(detail.HtmlBody);
            }
            else if (!string.IsNullOrEmpty(detail.TextBody))
            {
                var htmlWrapped = $"<html><body><pre style=\"font-family:Segoe UI,sans-serif;white-space:pre-wrap;\">{System.Net.WebUtility.HtmlEncode(detail.TextBody)}</pre></body></html>";
                _messageViewer.NavigateToString(htmlWrapped);
            }
            else
            {
                _messageViewer.NavigateToString("<html><body><p style=\"color:gray;\">No content</p></body></html>");
            }
            _messageViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load message: {ex.Message}");
        }
    }

    private async Task SearchEmailsAsync(string searchText)
    {
        if (_imapService == null || string.IsNullOrWhiteSpace(searchText)) return;

        SetLoading(true, $"Searching for \"{searchText}\"...");

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var criteria = new EmailSearchCriteria
            {
                SubjectContains = searchText,
                FromContains = searchText
            };

            // Search with subject OR from matching - MailKit requires separate queries
            // We'll search subject first, then merge with from results
            var subjectCriteria = new EmailSearchCriteria { SubjectContains = searchText };
            var fromCriteria = new EmailSearchCriteria { FromContains = searchText };

            var subjectResults = await _imapService.SearchAsync(_currentFolderName, subjectCriteria, _cts.Token);
            var fromResults = await _imapService.SearchAsync(_currentFolderName, fromCriteria, _cts.Token);

            // Merge and deduplicate
            var allResults = subjectResults
                .Concat(fromResults)
                .DistinctBy(m => m.UniqueId)
                .OrderByDescending(m => m.Date)
                .ToList();

            _messages.Clear();
            foreach (var msg in allResults)
            {
                _messages.Add(msg);
            }

            _messageCountText!.Text = $"{_messages.Count} result(s)";
            SetStatus($"Found {_messages.Count} messages matching \"{searchText}\"");
        }
        catch (Exception ex)
        {
            SetStatus($"Search failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task ArchiveSelectedEmailsAsync()
    {
        if (_imapService == null || _messageList == null) return;

        var selectedItems = _messageList.SelectedItems
            .OfType<EmailMessageSummary>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            SetStatus("No messages selected for archiving.");
            return;
        }

        // Ask for target directory
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        folderPicker.FileTypeFilter.Add("*");

        // Initialize with window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder == null) return;

        var targetDir = folder.Path;

        SetLoading(true, $"Archiving {selectedItems.Count} email(s)...");

        try
        {
            _cts ??= new CancellationTokenSource();

            // Group by folder
            var byFolder = selectedItems.GroupBy(m => m.FolderFullName);
            var totalResult = new EmailArchiveResult();

            foreach (var group in byFolder)
            {
                var uids = group.Select(m => m.UniqueId).ToList();
                var result = await _imapService.ArchiveEmailsAsync(
                    group.Key, uids, targetDir, _cts.Token);

                totalResult.TotalRequested += result.TotalRequested;
                totalResult.Succeeded += result.Succeeded;
                totalResult.Failed += result.Failed;
                totalResult.SavedFilePaths.AddRange(result.SavedFilePaths);
                totalResult.Errors.AddRange(result.Errors);
            }

            // Show result
            var resultMessage = $"Archived {totalResult.Succeeded} of {totalResult.TotalRequested} email(s) to:\n{targetDir}";
            if (totalResult.Failed > 0)
            {
                resultMessage += $"\n\n{totalResult.Failed} failed:\n" + string.Join("\n", totalResult.Errors.Take(5));
            }

            var resultDialog = new ContentDialog
            {
                Title = "Archive Complete",
                Content = resultMessage,
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };
            await resultDialog.ShowAsync();

            SetStatus($"Archived {totalResult.Succeeded} email(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Archive failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    // --- Event handlers ---

    private async void AccountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ConnectToSelectedAccountAsync();
    }

    private async void FolderTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count > 0)
        {
            // TreeView SelectionChanged provides the Content of the node, not the node itself
            EmailFolder? folder = null;
            if (args.AddedItems[0] is EmailFolder directFolder)
            {
                folder = directFolder;
            }
            else if (args.AddedItems[0] is TreeViewNode node && node.Content is EmailFolder nodeFolder)
            {
                folder = nodeFolder;
            }

            if (folder != null)
            {
                await LoadMessagesAsync(folder.FullName);
            }
        }
    }

    private async void MessageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _archiveButton!.IsEnabled = _messageList!.SelectedItems.Count > 0;

        if (_messageList.SelectedItems.Count == 1 && _messageList.SelectedItem is EmailMessageSummary summary)
        {
            await LoadMessagePreviewAsync(summary);
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_searchBox != null)
        {
            await SearchEmailsAsync(_searchBox.Text);
        }
    }

    private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && _searchBox != null)
        {
            await SearchEmailsAsync(_searchBox.Text);
        }
    }

    private async void ArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        await ArchiveSelectedEmailsAsync();
    }

    // --- Helpers ---

    private void SetLoading(bool isLoading, string? message = null)
    {
        if (_loadingRing != null) _loadingRing.IsActive = isLoading;
        if (message != null && _statusText != null) _statusText.Text = message;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null) _statusText.Text = message;
    }

    /// <summary>
    /// Handles ContainerContentChanging for efficient message list item rendering.
    /// </summary>
    private void MessageList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
            return;

        if (args.Item is EmailMessageSummary msg)
        {
            var grid = new Grid { Padding = new Thickness(4, 6, 4, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Unread indicator
            var readIndicator = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Visibility = msg.IsRead ? Visibility.Collapsed : Visibility.Visible
            };
            Grid.SetColumn(readIndicator, 0);

            // Subject and From
            var textPanel = new StackPanel { Spacing = 2 };
            var subjectBlock = new TextBlock
            {
                Text = msg.Subject,
                FontSize = 13,
                FontWeight = msg.IsRead ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            var fromText = msg.HasAttachments ? $"{msg.From}  \U0001F4CE" : msg.From;
            var fromBlock = new TextBlock
            {
                Text = fromText,
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            textPanel.Children.Add(subjectBlock);
            textPanel.Children.Add(fromBlock);
            Grid.SetColumn(textPanel, 1);

            // Date
            var dateBlock = new TextBlock
            {
                Text = msg.Date.ToString("MMM dd, HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(dateBlock, 2);

            grid.Children.Add(readIndicator);
            grid.Children.Add(textPanel);
            grid.Children.Add(dateBlock);

            args.ItemContainer.Content = grid;
        }
    }
}
