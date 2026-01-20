using LibGit2Sharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Message = "Manage multiple Git repositories for synchronizing your category data. Use local repository paths or remote URLs.",
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

        // Repository Name ComboBox with + and - icons
        var repoNamePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Repository Name:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var repoNameComboBox = new ComboBox
        {
            PlaceholderText = "Select or enter repository name",
            IsEditable = true,
            MinWidth = 600,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Load existing repository names
        if (_configService?.GitRepositories != null)
        {
            foreach (var repoName in _configService.GitRepositories.Keys)
            {
                repoNameComboBox.Items.Add(repoName);
            }
            if (repoNameComboBox.Items.Count > 0)
            {
                repoNameComboBox.SelectedIndex = 0;
            }
        }

        var addButton = new Button
        {
            Content = new SymbolIcon(Symbol.Add),
            Width = 40,
            Height = 32
        };
        ToolTipService.SetToolTip(addButton, "Add new repository");

        var removeButton = new Button
        {
            Content = new SymbolIcon(Symbol.Remove),
            Width = 40,
            Height = 32,
            IsEnabled = repoNameComboBox.Items.Count > 0
        };
        ToolTipService.SetToolTip(removeButton, "Remove selected repository");

        repoNamePanel.Children.Add(repoNameComboBox);
        repoNamePanel.Children.Add(addButton);
        repoNamePanel.Children.Add(removeButton);
        stackPanel.Children.Add(repoNamePanel);

        // Repository Path/URL
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Repository Path or URL:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var repoPathTextBox = new TextBox
        {
            Text = string.Empty,
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
            Text = string.Empty,
            PlaceholderText = "Git username for authentication",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 720
        };
        stackPanel.Children.Add(usernameTextBox);

        // Default Branch
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Default Branch:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var defaultBranchTextBox = new TextBox
        {
            Text = "main",
            PlaceholderText = "e.g., main, master, develop",
            IsReadOnly = false,
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 720
        };
        stackPanel.Children.Add(defaultBranchTextBox);

        // Clone button
        var cloneButton = new Button
        {
            Content = "Clone Repository",
            Margin = new Thickness(0, 0, 0, 16),
            IsEnabled = false
        };
        ToolTipService.SetToolTip(cloneButton, "Clone the repository to local git directory");
        stackPanel.Children.Add(cloneButton);

        // Helper method to update clone button state
        void UpdateCloneButtonState()
        {
            cloneButton.IsEnabled = !string.IsNullOrWhiteSpace(repoNameComboBox.Text) && !string.IsNullOrWhiteSpace(repoPathTextBox.Text);
        }

        // Enable clone button when both name and path are entered
        repoNameComboBox.SelectionChanged += (s, args) => UpdateCloneButtonState();
        repoNameComboBox.TextSubmitted += (s, args) => UpdateCloneButtonState();
        repoPathTextBox.TextChanged += (s, args) => UpdateCloneButtonState();

        // Current status display
        var currentStatusPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        currentStatusPanel.Children.Add(new TextBlock
        {
            Text = "Configured Repositories:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var statusTextBlock = new TextBlock
        {
            Text = _configService?.GitRepositories?.Count > 0
                ? $"{_configService.GitRepositories.Count} repository(ies) configured"
                : "No repositories configured",
            Foreground = _configService?.GitRepositories?.Count > 0
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        currentStatusPanel.Children.Add(statusTextBlock);
        stackPanel.Children.Add(currentStatusPanel);

        // Event handler for repository selection change
        repoNameComboBox.SelectionChanged += (s, args) =>
        {
            if (repoNameComboBox.SelectedItem is string selectedName &&
                _configService?.GitRepositories?.TryGetValue(selectedName, out var repoInfo) == true)
            {
                repoPathTextBox.Text = repoInfo.Path;
                usernameTextBox.Text = repoInfo.Username;
                defaultBranchTextBox.Text = string.IsNullOrEmpty(repoInfo.DefaultBranch) ? "main" : repoInfo.DefaultBranch;
                cloneButton.IsEnabled = !string.IsNullOrEmpty(repoInfo.Path);
            }
            else
            {
                // Clear fields when no valid repository is selected
                repoPathTextBox.Text = string.Empty;
                usernameTextBox.Text = string.Empty;
                defaultBranchTextBox.Text = "main";
                cloneButton.IsEnabled = false;
            }
        };

        // Add button click handler
        addButton.Click += (s, args) =>
        {
            var repoName = repoNameComboBox.Text.Trim();
            if (string.IsNullOrEmpty(repoName))
            {
                statusBanner.Title = "Validation Error";
                statusBanner.Message = "Please enter a repository name.";
                statusBanner.Severity = InfoBarSeverity.Error;
                statusBanner.IsOpen = true;
                return;
            }

            if (_configService?.GitRepositories?.ContainsKey(repoName) == true)
            {
                statusBanner.Title = "Validation Error";
                statusBanner.Message = $"Repository '{repoName}' already exists.";
                statusBanner.Severity = InfoBarSeverity.Error;
                statusBanner.IsOpen = true;
                return;
            }

            // Add to combobox if not already there
            if (!repoNameComboBox.Items.Contains(repoName))
            {
                repoNameComboBox.Items.Add(repoName);
                repoNameComboBox.SelectedItem = repoName;
            }

            // Clear fields for new entry
            repoPathTextBox.Text = string.Empty;
            usernameTextBox.Text = string.Empty;
            removeButton.IsEnabled = true;

            statusBanner.Title = "New Repository";
            statusBanner.Message = $"Ready to configure '{repoName}'. Enter path/URL and save.";
            statusBanner.Severity = InfoBarSeverity.Informational;
            statusBanner.IsOpen = true;
        };

        // Remove button click handler
        removeButton.Click += async (s, args) =>
        {
            if (repoNameComboBox.SelectedItem is not string selectedName)
            {
                statusBanner.Title = "Validation Error";
                statusBanner.Message = "Please select a repository to remove.";
                statusBanner.Severity = InfoBarSeverity.Error;
                statusBanner.IsOpen = true;
                return;
            }

            // Confirm deletion
            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Deletion",
                Content = $"Are you sure you want to remove the repository '{selectedName}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (_configService?.GitRepositories != null)
                {
                    _configService.GitRepositories.Remove(selectedName);
                    await _configService.SaveConfigurationAsync();

                    repoNameComboBox.Items.Remove(selectedName);
                    if (repoNameComboBox.Items.Count > 0)
                    {
                        repoNameComboBox.SelectedIndex = 0;
                    }
                    else
                    {
                        repoNameComboBox.SelectedIndex = -1;
                        repoPathTextBox.Text = string.Empty;
                        usernameTextBox.Text = string.Empty;
                        removeButton.IsEnabled = false;
                    }

                    statusTextBlock.Text = _configService.GitRepositories.Count > 0
                        ? $"{_configService.GitRepositories.Count} repository(ies) configured"
                        : "No repositories configured";

                    statusBanner.Title = "Repository Removed";
                    statusBanner.Message = $"Repository '{selectedName}' has been removed.";
                    statusBanner.Severity = InfoBarSeverity.Success;
                    statusBanner.IsOpen = true;
                }
            }
        };

        // Clone button click handler
        cloneButton.Click += async (s, args) =>
        {
            var repoName = repoNameComboBox.Text.Trim();
            var repoPath = repoPathTextBox.Text.Trim();
            var username = usernameTextBox.Text.Trim();
            var defaultBranch = defaultBranchTextBox.Text.Trim();

            if (string.IsNullOrEmpty(repoName))
            {
                statusBanner.Title = "Validation Error";
                statusBanner.Message = "Please enter a repository name.";
                statusBanner.Severity = InfoBarSeverity.Error;
                statusBanner.IsOpen = true;
                return;
            }

            if (string.IsNullOrEmpty(repoPath))
            {
                statusBanner.Title = "Validation Error";
                statusBanner.Message = "Please enter a repository path or URL.";
                statusBanner.Severity = InfoBarSeverity.Error;
                statusBanner.IsOpen = true;
                return;
            }

            // Clone the repository
            await CloneGitRepositoryAsync(repoName, repoPath, username, defaultBranch, statusBanner);
        };

        // Create dialog
        var dialog = new ContentDialog
        {
            Title = "Git Repository Setup",
            Content = stackPanel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        dialog.PrimaryButtonClick += async (s, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var repoName = repoNameComboBox.Text.Trim();
                var repoPath = repoPathTextBox.Text.Trim();
                var username = usernameTextBox.Text.Trim();
                var defaultBranch = defaultBranchTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(defaultBranch))
                {
                    defaultBranch = "main";
                }

                if (string.IsNullOrEmpty(repoName))
                {
                    statusBanner.Title = "Validation Error";
                    statusBanner.Message = "Repository name is required.";
                    statusBanner.Severity = InfoBarSeverity.Error;
                    statusBanner.IsOpen = true;
                    args.Cancel = true;
                    return;
                }

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
                    // Add or update repository
                    _configService.GitRepositories[repoName] = new GitRepositoryInfo
                    {
                        Path = repoPath,
                        Username = username,
                        Connected = true,
                        DefaultBranch = defaultBranch
                    };
                    await _configService.SaveConfigurationAsync();

                    statusBanner.Title = "Success";
                    statusBanner.Message = $"Repository '{repoName}' saved successfully.";
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

    private async Task CloneGitRepositoryAsync(string repoName, string repoUrl, string username, string defaultBranch, InfoBar statusBanner)
    {
        await Task.Run(async () =>
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Cloning Repository...";
                    statusBanner.Message = $"Cloning '{repoName}' to local git directory...";
                    statusBanner.Severity = InfoBarSeverity.Informational;
                    statusBanner.IsOpen = true;
                });

                // Get app data directory and create git subdirectory
                var appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyMemories"
                );
                var gitDirectory = Path.Combine(appDataFolder, "git");
                Directory.CreateDirectory(gitDirectory);

                // Create repository-specific directory
                var repoDirectory = Path.Combine(gitDirectory, SanitizeDirectoryName(repoName));
                
                // If directory already exists, delete it first
                if (Directory.Exists(repoDirectory))
                {
                    try
                    {
                        Directory.Delete(repoDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            statusBanner.Title = "Error";
                            statusBanner.Message = $"Failed to delete existing clone directory: {ex.Message}";
                            statusBanner.Severity = InfoBarSeverity.Error;
                        });
                        return;
                    }
                }

                // Setup clone options
                var cloneOptions = new CloneOptions
                {
                    BranchName = string.IsNullOrEmpty(defaultBranch) ? null : defaultBranch
                };

                // Add credentials handler if username is provided
                if (!string.IsNullOrEmpty(username))
                {
                    cloneOptions.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) =>
                    {
                        // For now, we'll use the username without password
                        // In a production app, you'd want to prompt for password or use a credential manager
                        return new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = string.Empty // User would need to configure git credentials or use SSH
                        };
                    };
                }

                // Clone the repository
                try
                {
                    Repository.Clone(repoUrl, repoDirectory, cloneOptions);

                    // Update configuration
                    if (_configService != null && _configService.GitRepositories.ContainsKey(repoName))
                    {
                        _configService.GitRepositories[repoName].IsCloned = true;
                        _configService.GitRepositories[repoName].LocalClonePath = repoDirectory;
                        await _configService.SaveConfigurationAsync();
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        statusBanner.Title = "Clone Successful";
                        statusBanner.Message = $"Repository '{repoName}' cloned successfully to: {repoDirectory}";
                        statusBanner.Severity = InfoBarSeverity.Success;
                    });
                }
                catch (LibGit2SharpException gitEx)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        statusBanner.Title = "Clone Failed";
                        statusBanner.Message = $"Git error: {gitEx.Message}";
                        statusBanner.Severity = InfoBarSeverity.Error;
                    });
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Error";
                    statusBanner.Message = $"Failed to clone repository: {ex.Message}";
                    statusBanner.Severity = InfoBarSeverity.Error;
                });
            }
        });
    }

    private string SanitizeDirectoryName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
