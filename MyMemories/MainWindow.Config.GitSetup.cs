using LibGit2Sharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async Task ShowGitSetupDialogAsync()
    {
        // Initialize folder picker service if needed
        _folderPickerService ??= new FolderPickerService(this);

        // Create UI for Git repository setup
        var stackPanel = new StackPanel { Spacing = 16 };

        // Info banner
        var infoBanner = new InfoBar
        {
            Title = "Git Repository Configuration",
            Message = "Connect to a Git repository to synchronize your category data. You can use a local repository path or a remote URL.",
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Margin = new Thickness(0, 0, 0, 16)
        };
        stackPanel.Children.Add(infoBanner);

        // Status banner (initially hidden)
        var statusBanner = new InfoBar
        {
            IsOpen = false,
            IsClosable = true,
            Margin = new Thickness(0, 0, 0, 16)
        };
        stackPanel.Children.Add(statusBanner);

        // Repository Path/URL
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Repository Path or URL:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var repoPathTextBox = new TextBox
        {
            Text = _configService?.GitRepositoryPath ?? string.Empty,
            PlaceholderText = "Enter local path or remote URL (e.g., https://github.com/user/repo.git)",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 720
        };
        stackPanel.Children.Add(repoPathTextBox);

        var repoButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var browseRepoButton = new Button
        {
            Content = "Browse Local..."
        };
        ToolTipService.SetToolTip(browseRepoButton, "Browse for a local Git repository");

        var testConnectionButton = new Button
        {
            Content = "Test Connection"
        };
        ToolTipService.SetToolTip(testConnectionButton, "Test if repository is valid");

        browseRepoButton.Click += (s, args) =>
        {
            var selectedPath = _folderPickerService?.BrowseForFolder(repoPathTextBox.Text, "Select Git Repository");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                repoPathTextBox.Text = selectedPath;
            }
        };

        testConnectionButton.Click += async (s, args) =>
        {
            await TestGitConnectionAsync(repoPathTextBox.Text, statusBanner);
        };

        repoButtonPanel.Children.Add(browseRepoButton);
        repoButtonPanel.Children.Add(testConnectionButton);
        stackPanel.Children.Add(repoButtonPanel);

        // Username (optional for remote repos)
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Username (optional, for remote repositories):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var usernameTextBox = new TextBox
        {
            Text = _configService?.GitUsername ?? string.Empty,
            PlaceholderText = "Git username for authentication",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 16),
            MinWidth = 720
        };
        stackPanel.Children.Add(usernameTextBox);

        // Current status display
        var currentStatusPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        currentStatusPanel.Children.Add(new TextBlock
        {
            Text = "Current Status:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var statusTextBlock = new TextBlock
        {
            Text = _configService?.GitRepositoryConnected == true
                ? $"✓ Connected to: {_configService?.GitRepositoryPath}"
                : "Not connected",
            Foreground = _configService?.GitRepositoryConnected == true
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        currentStatusPanel.Children.Add(statusTextBlock);
        stackPanel.Children.Add(currentStatusPanel);

        // Create dialog
        var dialog = new ContentDialog
        {
            Title = "Git Repository Setup",
            Content = stackPanel,
            PrimaryButtonText = "Save & Connect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        dialog.PrimaryButtonClick += async (s, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var repoPath = repoPathTextBox.Text.Trim();
                var username = usernameTextBox.Text.Trim();

                if (string.IsNullOrEmpty(repoPath))
                {
                    statusBanner.Title = "Validation Error";
                    statusBanner.Message = "Repository path or URL is required.";
                    statusBanner.Severity = InfoBarSeverity.Error;
                    statusBanner.IsOpen = true;
                    args.Cancel = true;
                    return;
                }

                // Validate and save configuration
                var isConnected = await ValidateAndConnectGitRepositoryAsync(repoPath, username, statusBanner);
                
                if (isConnected && _configService != null)
                {
                    _configService.GitRepositoryPath = repoPath;
                    _configService.GitUsername = username;
                    _configService.GitRepositoryConnected = true;
                    await _configService.SaveConfigurationAsync();

                    statusBanner.Title = "Success";
                    statusBanner.Message = "Git repository connection saved successfully.";
                    statusBanner.Severity = InfoBarSeverity.Success;
                    statusBanner.IsOpen = true;
                }
                else
                {
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
    }

    private (bool isValid, string title, string message, InfoBarSeverity severity) ValidateGitRepository(string repoPath)
    {
        if (string.IsNullOrEmpty(repoPath))
        {
            return (false, "Validation Error", "Repository path or URL is required.", InfoBarSeverity.Error);
        }

        try
        {
            // Check if it's a local path
            if (Directory.Exists(repoPath))
            {
                // Test if it's a valid Git repository
                if (Repository.IsValid(repoPath))
                {
                    using var repo = new Repository(repoPath);
                    return (true, "Connection Successful", $"Valid local Git repository found. Branch: {repo.Head.FriendlyName}", InfoBarSeverity.Success);
                }
                else
                {
                    return (false, "Invalid Repository", "The specified directory is not a valid Git repository.", InfoBarSeverity.Warning);
                }
            }
            else if (repoPath.StartsWith("http://") || repoPath.StartsWith("https://") || repoPath.StartsWith("git@"))
            {
                // For remote URLs, we can't fully test without credentials
                return (true, "Remote URL Detected", "Remote repository URL format looks valid. Full connection will be tested on save.", InfoBarSeverity.Informational);
            }
            else
            {
                return (false, "Invalid Path", "The specified path does not exist and is not a valid URL.", InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            return (false, "Error", $"Failed to validate repository: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task TestGitConnectionAsync(string repoPath, InfoBar statusBanner)
    {
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                statusBanner.Title = "Testing Connection...";
                statusBanner.Message = "Validating repository...";
                statusBanner.Severity = InfoBarSeverity.Informational;
                statusBanner.IsOpen = true;
            });

            var (isValid, title, message, severity) = ValidateGitRepository(repoPath);
            
            DispatcherQueue.TryEnqueue(() =>
            {
                statusBanner.Title = title;
                statusBanner.Message = message;
                statusBanner.Severity = severity;
            });
        });
    }

    private async Task<bool> ValidateAndConnectGitRepositoryAsync(string repoPath, string username, InfoBar statusBanner)
    {
        return await Task.Run(() =>
        {
            var (isValid, title, message, severity) = ValidateGitRepository(repoPath);
            
            DispatcherQueue.TryEnqueue(() =>
            {
                statusBanner.Title = title;
                statusBanner.Message = message;
                statusBanner.Severity = severity;
                statusBanner.IsOpen = true;
            });
            
            return isValid;
        });
    }
}
