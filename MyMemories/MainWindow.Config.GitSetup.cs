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
    private GitConfigService? _gitConfigService;
    
    private async Task ShowGitSetupDialogAsync()
    {
        // Initialize folder picker service if needed
        _folderPickerService ??= new FolderPickerService(this);
        
        // Initialize git config service if needed
        if (_gitConfigService == null)
        {
            _gitConfigService = new GitConfigService();
            await _gitConfigService.LoadAsync();
        }

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
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Load existing repository names
        if (_gitConfigService?.Repositories != null)
        {
            foreach (var repo in _gitConfigService.Repositories)
            {
                repoNameComboBox.Items.Add(repo.Name);
            }
            if (repoNameComboBox.Items.Count > 0)
            {
                repoNameComboBox.SelectedIndex = 0;
            }
        }

        var addButton = new Button
        {
            Content = new SymbolIcon(Symbol.Add),
            Width = 50,
            Height = 32
        };
        ToolTipService.SetToolTip(addButton, "Clear fields for new repository");

        var removeButton = new Button
        {
            Content = new SymbolIcon(Symbol.Remove),
            Width = 50,
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
            HorizontalAlignment = HorizontalAlignment.Stretch
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
            Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        stackPanel.Children.Add(usernameTextBox);

        // Password (optional for remote repos)
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Password (optional, for remote repositories):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var passwordBox = new PasswordBox
        {
            PlaceholderText = "Git password for authentication",
            Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        stackPanel.Children.Add(passwordBox);

        // Default Branch
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Branch:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var branchComboBox = new ComboBox
        {
            PlaceholderText = "Select branch to clone",
            IsEditable = false,
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        stackPanel.Children.Add(branchComboBox);

        // Current Branch display (shown when repository is cloned)
        var currentBranchTextBlock = new TextBlock
        {
            Text = string.Empty,
            FontSize = 13,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGreen),
            Margin = new Thickness(0, 0, 0, 8),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        stackPanel.Children.Add(currentBranchTextBlock);

        var fetchBranchesButton = new Button
        {
            Content = "Fetch Branches",
            Margin = new Thickness(0, 0, 0, 16)
        };
        ToolTipService.SetToolTip(fetchBranchesButton, "Fetch available branches from repository");
        stackPanel.Children.Add(fetchBranchesButton);

        // Clone/Pull button
        var cloneButton = new Button
        {
            Content = "Clone Repository",
            Margin = new Thickness(0, 0, 0, 8),
            IsEnabled = false
        };
        ToolTipService.SetToolTip(cloneButton, "Clone the repository to local git directory");
        stackPanel.Children.Add(cloneButton);

        // Commit SHA display (shown after clone button)
        var commitShaTextBlock = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(commitShaTextBlock);

        // Clone status banner (initially hidden, below clone button)
        var cloneStatusBanner = new InfoBar
        {
            IsOpen = false,
            IsClosable = true,
            Margin = new Thickness(0, 0, 0, 16)
        };
        stackPanel.Children.Add(cloneStatusBanner);

        // Helper method to update clone button state and labels
        void UpdateCloneButtonState()
        {
            cloneButton.IsEnabled = !string.IsNullOrWhiteSpace(repoNameComboBox.Text) && 
                                   !string.IsNullOrWhiteSpace(repoPathTextBox.Text) &&
                                   branchComboBox.SelectedItem != null;
            fetchBranchesButton.IsEnabled = !string.IsNullOrWhiteSpace(repoPathTextBox.Text);

            // Update fetch branches button label
            if (branchComboBox.Items.Count > 0)
            {
                fetchBranchesButton.Content = "Refresh Branches";
                ToolTipService.SetToolTip(fetchBranchesButton, "Refresh available branches from repository");
            }
            else
            {
                fetchBranchesButton.Content = "Fetch Branches";
                ToolTipService.SetToolTip(fetchBranchesButton, "Fetch available branches from repository");
            }

            // Update clone/pull button label, current branch display, and commit SHA display
            var selectedRepoName = repoNameComboBox.Text.Trim();
            if (!string.IsNullOrEmpty(selectedRepoName) && _gitConfigService != null)
            {
                var repoConfig = _gitConfigService.GetRepository(selectedRepoName);
                if (repoConfig != null && repoConfig.IsCloned && !string.IsNullOrEmpty(repoConfig.LocalClonePath))
                {
                    var selectedBranchDisplay = branchComboBox.SelectedItem as string;
                    var selectedBranch = ExtractBranchName(selectedBranchDisplay); // Extract branch name without SHA
                    var currentCheckedOutBranch = repoConfig.CurrentCheckedOutBranch;

                    // Display current branch and commit SHA
                    if (!string.IsNullOrEmpty(currentCheckedOutBranch))
                    {
                        var commitSha = GetCurrentCommitSha(repoConfig.LocalClonePath);
                        if (!string.IsNullOrEmpty(commitSha))
                        {
                            currentBranchTextBlock.Text = $"Current Branch: {currentCheckedOutBranch} ({commitSha})";
                        }
                        else if (!string.IsNullOrEmpty(repoConfig.CurrentBranchCommit))
                        {
                            currentBranchTextBlock.Text = $"Current Branch: {currentCheckedOutBranch} ({repoConfig.CurrentBranchCommit})";
                        }
                        else
                        {
                            currentBranchTextBlock.Text = $"Current Branch: {currentCheckedOutBranch}";
                        }
                    }
                    else
                    {
                        currentBranchTextBlock.Text = string.Empty;
                    }

                    // Only enable pull button if selected branch is different from current checked-out branch
                    if (!string.IsNullOrEmpty(selectedBranch) && !string.IsNullOrEmpty(currentCheckedOutBranch) && selectedBranch != currentCheckedOutBranch)
                    {
                        cloneButton.Content = "Pull Changes";
                        ToolTipService.SetToolTip(cloneButton, $"Pull changes from branch '{selectedBranch}'");
                        cloneButton.IsEnabled = true;
                    }
                    else if (!string.IsNullOrEmpty(selectedBranch) && selectedBranch == currentCheckedOutBranch)
                    {
                        // Same branch selected as current - disable pull button
                        cloneButton.Content = "Pull Changes";
                        ToolTipService.SetToolTip(cloneButton, "Already on this branch");
                        cloneButton.IsEnabled = false;
                    }
                    else
                    {
                        cloneButton.Content = "Pull Changes";
                        ToolTipService.SetToolTip(cloneButton, "Select a different branch to pull");
                        cloneButton.IsEnabled = false;
                    }

                    // Display commit SHA below button
                    var commitShaForDisplay = GetCurrentCommitSha(repoConfig.LocalClonePath);
                    if (!string.IsNullOrEmpty(commitShaForDisplay))
                    {
                        commitShaTextBlock.Text = $"Current commit: {commitShaForDisplay}";
                    }
                    else if (!string.IsNullOrEmpty(repoConfig.CurrentBranchCommit))
                    {
                        commitShaTextBlock.Text = $"Current commit: {repoConfig.CurrentBranchCommit}";
                    }
                    else
                    {
                        commitShaTextBlock.Text = string.Empty;
                    }
                }
                else
                {
                    cloneButton.Content = "Clone Repository";
                    ToolTipService.SetToolTip(cloneButton, "Clone the repository to local git directory");
                    currentBranchTextBlock.Text = string.Empty;
                    commitShaTextBlock.Text = string.Empty;
                    cloneButton.IsEnabled = !string.IsNullOrWhiteSpace(repoNameComboBox.Text) && 
                                           !string.IsNullOrWhiteSpace(repoPathTextBox.Text) &&
                                           branchComboBox.SelectedItem != null;
                }
            }
            else
            {
                cloneButton.Content = "Clone Repository";
                ToolTipService.SetToolTip(cloneButton, "Clone the repository to local git directory");
                currentBranchTextBlock.Text = string.Empty;
                commitShaTextBlock.Text = string.Empty;
            }
        }

        // Enable/disable buttons based on input
        repoNameComboBox.SelectionChanged += (s, args) => UpdateCloneButtonState();
        repoNameComboBox.TextSubmitted += (s, args) => UpdateCloneButtonState();
        repoPathTextBox.TextChanged += (s, args) => UpdateCloneButtonState();
        branchComboBox.SelectionChanged += (s, args) => UpdateCloneButtonState();

        // Fetch branches button click handler
        fetchBranchesButton.Click += async (s, args) =>
        {
            var repoPath = repoPathTextBox.Text.Trim();
            var username = usernameTextBox.Text.Trim();
            
            branchComboBox.Items.Clear();
            cloneStatusBanner.IsOpen = false;
            
            await FetchRemoteBranchesAsync(repoPath, username, branchComboBox, cloneStatusBanner);
            
            // Update button state after fetching branches
            UpdateCloneButtonState();
        };

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
            Text = _gitConfigService?.Repositories?.Count > 0
                ? $"{_gitConfigService.Repositories.Count} repository(ies) configured"
                : "No repositories configured",
            Foreground = _gitConfigService?.Repositories?.Count > 0
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        currentStatusPanel.Children.Add(statusTextBlock);
        stackPanel.Children.Add(currentStatusPanel);

        // Event handler for repository selection change
        repoNameComboBox.SelectionChanged += (s, args) =>
        {
            if (repoNameComboBox.SelectedItem is string selectedName)
            {
                var repoConfig = _gitConfigService?.GetRepository(selectedName);
                if (repoConfig != null)
                {
                    repoPathTextBox.Text = repoConfig.Path;
                    usernameTextBox.Text = repoConfig.Username;
                    passwordBox.Password = repoConfig.Password;
                    
                    // Load available branches if they were fetched
                    branchComboBox.Items.Clear();
                    if (repoConfig.AvailableBranches != null && repoConfig.AvailableBranches.Count > 0)
                    {
                        foreach (var branch in repoConfig.AvailableBranches)
                        {
                            branchComboBox.Items.Add(branch);
                        }
                        
                        // Select the previously selected branch if available
                        if (!string.IsNullOrEmpty(repoConfig.SelectedBranch) && 
                            branchComboBox.Items.Contains(repoConfig.SelectedBranch))
                    {
                        branchComboBox.SelectedItem = repoConfig.SelectedBranch;
                    }
                    else if (!string.IsNullOrEmpty(repoConfig.DefaultBranch) &&
                             branchComboBox.Items.Contains(repoConfig.DefaultBranch))
                    {
                        branchComboBox.SelectedItem = repoConfig.DefaultBranch;
                    }
                    else if (branchComboBox.Items.Count > 0)
                    {
                        branchComboBox.SelectedIndex = 0;
                    }
                    else if (!string.IsNullOrEmpty(repoConfig.DefaultBranch))
                    {
                        // If no branches fetched yet, show default branch
                        branchComboBox.Items.Add(repoConfig.DefaultBranch);
                        branchComboBox.SelectedIndex = 0;
                    }
                    }
                    
                    UpdateCloneButtonState();
                }
                else
                {
                    // Clear fields when repository not found
                    repoPathTextBox.Text = string.Empty;
                    usernameTextBox.Text = string.Empty;
                    passwordBox.Password = string.Empty;
                    branchComboBox.Items.Clear();
                    UpdateCloneButtonState();
                }
            }
            else
            {
                // Clear fields when no valid repository is selected
                repoPathTextBox.Text = string.Empty;
                usernameTextBox.Text = string.Empty;
                passwordBox.Password = string.Empty;
                branchComboBox.Items.Clear();
                UpdateCloneButtonState();
            }
        };

        // Manually trigger field population for initially selected repository
        if (repoNameComboBox.SelectedIndex >= 0 && repoNameComboBox.SelectedItem is string initialSelectedName)
        {
            var repoConfig = _gitConfigService?.GetRepository(initialSelectedName);
            if (repoConfig != null)
            {
                repoPathTextBox.Text = repoConfig.Path;
                usernameTextBox.Text = repoConfig.Username;
                passwordBox.Password = repoConfig.Password;
                
                // Load available branches if they were fetched
                branchComboBox.Items.Clear();
                if (repoConfig.AvailableBranches != null && repoConfig.AvailableBranches.Count > 0)
                {
                    foreach (var branch in repoConfig.AvailableBranches)
                    {
                        branchComboBox.Items.Add(branch);
                    }
                    
                    // Select the previously selected branch if available
                    if (!string.IsNullOrEmpty(repoConfig.SelectedBranch) && 
                        branchComboBox.Items.Contains(repoConfig.SelectedBranch))
                    {
                        branchComboBox.SelectedItem = repoConfig.SelectedBranch;
                    }
                    else if (!string.IsNullOrEmpty(repoConfig.DefaultBranch) &&
                             branchComboBox.Items.Contains(repoConfig.DefaultBranch))
                    {
                        branchComboBox.SelectedItem = repoConfig.DefaultBranch;
                    }
                    else if (branchComboBox.Items.Count > 0)
                    {
                        branchComboBox.SelectedIndex = 0;
                    }
                    else if (!string.IsNullOrEmpty(repoConfig.DefaultBranch))
                    {
                        // If no branches fetched yet, show default branch
                        branchComboBox.Items.Add(repoConfig.DefaultBranch);
                        branchComboBox.SelectedIndex = 0;
                    }
                }
                
                UpdateCloneButtonState();
            }
        }

        // Add button click handler - clears all fields for new repository entry
        addButton.Click += (s, args) =>
        {
            // Clear all fields including name
            repoNameComboBox.Text = string.Empty;
            repoNameComboBox.SelectedIndex = -1;
            repoPathTextBox.Text = string.Empty;
            usernameTextBox.Text = string.Empty;
            passwordBox.Password = string.Empty;
            branchComboBox.Items.Clear();
            UpdateCloneButtonState();

            statusBanner.Title = "New Repository";
            statusBanner.Message = "Enter repository details and click Save to create.";
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
                if (_gitConfigService?.Repositories != null)
                {
                    _gitConfigService.RemoveRepository(selectedName);
                    await _gitConfigService.SaveAsync();

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

                    statusTextBlock.Text = _gitConfigService.Repositories.Count > 0
                        ? $"{_gitConfigService.Repositories.Count} repository(ies) configured"
                        : "No repositories configured";

                    statusBanner.Title = "Repository Removed";
                    statusBanner.Message = $"Repository '{selectedName}' has been removed.";
                    statusBanner.Severity = InfoBarSeverity.Success;
                    statusBanner.IsOpen = true;
                }
            }
        };

        // Clone/Pull button click handler
        cloneButton.Click += async (s, args) =>
        {
            var repoName = repoNameComboBox.Text.Trim();
            var repoPath = repoPathTextBox.Text.Trim();
            var username = usernameTextBox.Text.Trim();
            var password = passwordBox.Password.Trim();
            var selectedBranchDisplay = branchComboBox.SelectedItem as string;
            var selectedBranch = ExtractBranchName(selectedBranchDisplay); // Extract branch name without SHA

            if (string.IsNullOrEmpty(repoName))
            {
                cloneStatusBanner.Title = "Validation Error";
                cloneStatusBanner.Message = "Please enter a repository name.";
                cloneStatusBanner.Severity = InfoBarSeverity.Error;
                cloneStatusBanner.IsOpen = true;
                return;
            }

            if (string.IsNullOrEmpty(repoPath))
            {
                cloneStatusBanner.Title = "Validation Error";
                cloneStatusBanner.Message = "Please enter a repository path or URL.";
                cloneStatusBanner.Severity = InfoBarSeverity.Error;
                cloneStatusBanner.IsOpen = true;
                return;
            }

            if (string.IsNullOrEmpty(selectedBranch))
            {
                cloneStatusBanner.Title = "Validation Error";
                cloneStatusBanner.Message = "Please select a branch.";
                cloneStatusBanner.Severity = InfoBarSeverity.Error;
                cloneStatusBanner.IsOpen = true;
                return;
            }

            // Check if repository is already cloned
            var repoConfig = _gitConfigService?.GetRepository(repoName);
            if (repoConfig != null && repoConfig.IsCloned && !string.IsNullOrEmpty(repoConfig.LocalClonePath))
            {
                // Save changes first before pulling
                // Update repository configuration with current values
                repoConfig.Path = repoPath;
                repoConfig.Username = username;
                repoConfig.Password = password;
                if (repoConfig.AvailableBranches.Count > 0)
                {
                    // Save available branches (extract branch names without SHA)
                    repoConfig.AvailableBranches = branchComboBox.Items.Cast<string>().Select(b => ExtractBranchName(b)).ToList();
                }
                
                _gitConfigService?.AddOrUpdateRepository(repoName, repoConfig);
                await _gitConfigService!.SaveAsync();

                // Pull changes
                await PullChangesAsync(repoName, repoConfig.LocalClonePath, username, password, selectedBranch, cloneStatusBanner);
                
                // Update commit SHA display after pull
                UpdateCloneButtonState();
            }
            else
            {
                // Clone the repository
                await CloneGitRepositoryAsync(repoName, repoPath, username, password, selectedBranch, cloneStatusBanner);
                
                // Update button state after clone
                UpdateCloneButtonState();
            }
        };

        // Wrap content in ScrollViewer for better layout
        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 700
        };

        // Create dialog
        var dialog = new ContentDialog
        {
            Title = "Git Repository Setup",
            Content = scrollViewer,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            MinWidth = 800,
            MaxWidth = 1000,
            MinHeight = 600
        };

        dialog.PrimaryButtonClick += async (s, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var repoName = repoNameComboBox.Text.Trim();
                var repoPath = repoPathTextBox.Text.Trim();
                var username = usernameTextBox.Text.Trim();
                var password = passwordBox.Password.Trim();
                var selectedBranchDisplay = branchComboBox.SelectedItem as string;
                
                // Extract branch name without SHA
                var selectedBranch = ExtractBranchName(selectedBranchDisplay);
                
                if (string.IsNullOrEmpty(selectedBranch))
                {
                    selectedBranch = "main"; // Default if not selected
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
                
                if (isConnected && _gitConfigService != null)
                {
                    // Get all fetched branches from the ComboBox (extract branch names without SHA)
                    var availableBranches = new List<string>();
                    foreach (var item in branchComboBox.Items)
                    {
                        if (item is string branchDisplay)
                        {
                            availableBranches.Add(ExtractBranchName(branchDisplay));
                        }
                    }
                    
                    // Check if repository already exists to preserve clone info
                    GitRepositoryConfig repoConfig;
                    var existingRepo = _gitConfigService.GetRepository(repoName);
                    if (existingRepo != null)
                    {
                        // Update existing repository, preserving clone status
                        repoConfig = existingRepo;
                        repoConfig.Path = repoPath;
                        repoConfig.Username = username;
                        repoConfig.Password = password;
                        repoConfig.Connected = true;
                        repoConfig.DefaultBranch = selectedBranch;
                        repoConfig.SelectedBranch = selectedBranch;
                        repoConfig.AvailableBranches = availableBranches;
                    }
                    else
                    {
                        // Create new repository
                        repoConfig = new GitRepositoryConfig
                        {
                            Path = repoPath,
                            Username = username,
                            Password = password,
                            Connected = true,
                            DefaultBranch = selectedBranch,
                            SelectedBranch = selectedBranch,
                            AvailableBranches = availableBranches
                        };
                    }
                    
                    _gitConfigService.AddOrUpdateRepository(repoName, repoConfig);
                    await _gitConfigService.SaveAsync();

                    // Add to ComboBox if it's a new repository
                    if (!repoNameComboBox.Items.Contains(repoName))
                    {
                        repoNameComboBox.Items.Add(repoName);
                    }
                    repoNameComboBox.SelectedItem = repoName;
                    removeButton.IsEnabled = true;

                    // Update status display
                    statusTextBlock.Text = _gitConfigService.Repositories.Count > 0
                        ? $"{_gitConfigService.Repositories.Count} repository(ies) configured"
                        : "No repositories configured";
                    statusTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);

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

    private async Task CloneGitRepositoryAsync(string repoName, string repoUrl, string username, string password, string defaultBranch, InfoBar statusBanner)
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
                    if (!TryDeleteDirectory(repoDirectory, out string deleteError))
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            statusBanner.Title = "Error";
                            statusBanner.Message = deleteError;
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
                        return new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = password ?? string.Empty
                        };
                    };
                }

                // Clone the repository
                try
                {
                    Repository.Clone(repoUrl, repoDirectory, cloneOptions);

                    // Update configuration with clone info and commit SHA
                    if (_gitConfigService != null)
                    {
                        var repoConfig = _gitConfigService.GetRepository(repoName);
                        if (repoConfig != null)
                        {
                            repoConfig.IsCloned = true;
                            repoConfig.LocalClonePath = repoDirectory;
                            repoConfig.SelectedBranch = defaultBranch;
                            repoConfig.CurrentCheckedOutBranch = defaultBranch; // Set current checked-out branch
                            
                            // Get and store the current commit SHA
                            try
                            {
                                using (var clonedRepo = new Repository(repoDirectory))
                                {
                                    repoConfig.CurrentBranchCommit = clonedRepo.Head?.Tip?.Sha?.Substring(0, 8) ?? string.Empty;
                                }
                            }
                            catch
                            {
                                // Ignore errors getting commit SHA
                            }
                            
                            _gitConfigService.AddOrUpdateRepository(repoName, repoConfig);
                            await _gitConfigService.SaveAsync();
                        }
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

    private async Task FetchRemoteBranchesAsync(string repoUrl, string username, ComboBox branchComboBox, InfoBar statusBanner)
    {
        await Task.Run(() =>
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Fetching Branches...";
                    statusBanner.Message = "Retrieving available branches from repository...";
                    statusBanner.Severity = InfoBarSeverity.Informational;
                    statusBanner.IsOpen = true;
                });

                var branches = new List<string>();
                var branchDisplayTexts = new List<string>(); // Display text with SHA

                // For local repositories
                if (Directory.Exists(repoUrl) && Repository.IsValid(repoUrl))
                {
                    using var repo = new Repository(repoUrl);
                    foreach (var branch in repo.Branches)
                    {
                        if (!branch.IsRemote)
                        {
                            branches.Add(branch.FriendlyName);
                            var commitSha = branch.Tip?.Sha?.Substring(0, 8) ?? "unknown";
                            branchDisplayTexts.Add($"{branch.FriendlyName} ({commitSha})");
                        }
                    }
                }
                // For remote repositories
                else if (repoUrl.StartsWith("http://") || repoUrl.StartsWith("https://") || repoUrl.StartsWith("git@"))
                {
                    // List remote references
                    try
                    {
                        var refs = Repository.ListRemoteReferences(repoUrl);
                        foreach (var reference in refs)
                        {
                            // Only include branch references (heads)
                            if (reference.CanonicalName.StartsWith("refs/heads/"))
                            {
                                var branchName = reference.CanonicalName.Substring("refs/heads/".Length);
                                branches.Add(branchName);
                                var commitSha = reference.TargetIdentifier?.Substring(0, 8) ?? "unknown";
                                branchDisplayTexts.Add($"{branchName} ({commitSha})");
                            }
                        }
                    }
                    catch (LibGit2SharpException gitEx)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            statusBanner.Title = "Fetch Failed";
                            statusBanner.Message = $"Could not fetch branches: {gitEx.Message}. Repository may require authentication.";
                            statusBanner.Severity = InfoBarSeverity.Warning;
                        });
                        return;
                    }
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        statusBanner.Title = "Invalid Repository";
                        statusBanner.Message = "Please enter a valid repository path or URL.";
                        statusBanner.Severity = InfoBarSeverity.Error;
                    });
                    return;
                }

                // Update UI with branches (display with SHA)
                DispatcherQueue.TryEnqueue(() =>
                {
                    branchComboBox.Items.Clear();
                    foreach (var displayText in branchDisplayTexts)
                    {
                        branchComboBox.Items.Add(displayText);
                    }

                    if (branches.Count > 0)
                    {
                        // Select main, master, or first branch
                        var defaultIndex = branches.FindIndex(b => b.Equals("main", StringComparison.OrdinalIgnoreCase));
                        if (defaultIndex < 0)
                        {
                            defaultIndex = branches.FindIndex(b => b.Equals("master", StringComparison.OrdinalIgnoreCase));
                        }
                        branchComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

                        statusBanner.Title = "Branches Loaded";
                        statusBanner.Message = $"Found {branches.Count} branch(es). Select a branch to clone.";
                        statusBanner.Severity = InfoBarSeverity.Success;
                    }
                    else
                    {
                        statusBanner.Title = "No Branches Found";
                        statusBanner.Message = "No branches were found in this repository.";
                        statusBanner.Severity = InfoBarSeverity.Warning;
                    }
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Error";
                    statusBanner.Message = $"Failed to fetch branches: {ex.Message}";
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

    /// <summary>
    /// Safely delete a directory by removing read-only attributes first.
    /// </summary>
    private bool TryDeleteDirectory(string directoryPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            if (!Directory.Exists(directoryPath))
                return true;

            // Remove read-only attributes from all files
            var directoryInfo = new DirectoryInfo(directoryPath);
            foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                }
                catch
                {
                    // Ignore individual file attribute errors
                }
            }

            // Remove read-only attribute from directories
            foreach (var dir in directoryInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                try
                {
                    dir.Attributes = FileAttributes.Normal;
                }
                catch
                {
                    // Ignore individual directory attribute errors
                }
            }

            // Now attempt to delete the directory
            Directory.Delete(directoryPath, true);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = $"Access denied: {ex.Message}. The directory may be in use by another process. Please close any applications that may be accessing the repository and try again.";
            return false;
        }
        catch (IOException ex)
        {
            errorMessage = $"I/O error: {ex.Message}. The directory may be in use. Please close any applications accessing the repository.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to delete directory: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Get the current commit SHA from a local repository.
    /// </summary>
    private string? GetCurrentCommitSha(string localClonePath)
    {
        try
        {
            if (!Directory.Exists(localClonePath))
                return null;

            using var repo = new Repository(localClonePath);
            return repo.Head?.Tip?.Sha?.Substring(0, 8); // Return short SHA (first 8 characters)
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pull changes from a remote repository.
    /// </summary>
    private async Task PullChangesAsync(string repoName, string localClonePath, string username, string password, string branch, InfoBar statusBanner)
    {
        await Task.Run(() =>
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Pulling Changes...";
                    statusBanner.Message = $"Pulling changes from branch '{branch}'...";
                    statusBanner.Severity = InfoBarSeverity.Informational;
                    statusBanner.IsOpen = true;
                });

                if (!Directory.Exists(localClonePath))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        statusBanner.Title = "Error";
                        statusBanner.Message = "Local clone path does not exist. Please clone the repository first.";
                        statusBanner.Severity = InfoBarSeverity.Error;
                    });
                    return;
                }

                using (var repo = new Repository(localClonePath))
                {
                    // Checkout the selected branch if different from current
                    var currentBranch = repo.Head.FriendlyName;
                    if (currentBranch != branch)
                    {
                        try
                        {
                            var localBranch = repo.Branches[branch];
                            if (localBranch == null)
                            {
                                // Create local tracking branch
                                var remoteBranch = repo.Branches[$"origin/{branch}"];
                                if (remoteBranch != null)
                                {
                                    localBranch = repo.CreateBranch(branch, remoteBranch.Tip);
                                    repo.Branches.Update(localBranch,
                                        b => b.TrackedBranch = remoteBranch.CanonicalName);
                                }
                            }

                            if (localBranch != null)
                            {
                                LibGit2Sharp.Commands.Checkout(repo, localBranch);
                            }
                        }
                        catch (Exception ex)
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                statusBanner.Title = "Checkout Failed";
                                statusBanner.Message = $"Failed to checkout branch '{branch}': {ex.Message}";
                                statusBanner.Severity = InfoBarSeverity.Error;
                            });
                            return;
                        }
                    }

                    // Setup pull options
                    var pullOptions = new PullOptions
                    {
                        FetchOptions = new FetchOptions()
                    };

                    // Add credentials if provided
                    if (!string.IsNullOrEmpty(username))
                    {
                        pullOptions.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) =>
                        {
                            return new UsernamePasswordCredentials
                            {
                                Username = username,
                                Password = password ?? string.Empty
                            };
                        };
                    }

                    // Create signature for merge commit
                    var signature = new Signature("MyMemories", "mymemories@local", DateTimeOffset.Now);

                    // Pull changes
                    try
                    {
                        var result = LibGit2Sharp.Commands.Pull(repo, signature, pullOptions);

                        // Update commit SHA in config
                        if (_gitConfigService != null)
                        {
                            var repoConfig = _gitConfigService.GetRepository(repoName);
                            if (repoConfig != null)
                            {
                                repoConfig.CurrentBranchCommit = repo.Head?.Tip?.Sha?.Substring(0, 8) ?? string.Empty;
                                repoConfig.SelectedBranch = branch;
                                repoConfig.CurrentCheckedOutBranch = branch; // Update current checked-out branch after pull
                                _gitConfigService.AddOrUpdateRepository(repoName, repoConfig);
                                Task.Run(async () => await _gitConfigService.SaveAsync()).Wait();
                            }
                        }

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            statusBanner.Title = "Pull Successful";
                            statusBanner.Message = result.Status == MergeStatus.UpToDate
                                ? "Repository is already up to date."
                                : $"Successfully pulled changes. Status: {result.Status}";
                            statusBanner.Severity = InfoBarSeverity.Success;
                        });
                    }
                    catch (LibGit2SharpException gitEx)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            statusBanner.Title = "Pull Failed";
                            statusBanner.Message = $"Git error: {gitEx.Message}";
                            statusBanner.Severity = InfoBarSeverity.Error;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusBanner.Title = "Error";
                    statusBanner.Message = $"Failed to pull changes: {ex.Message}";
                    statusBanner.Severity = InfoBarSeverity.Error;
                });
            }
        });
    }

    /// <summary>
    /// Extracts the branch name from a display text that may contain commit SHA in format "branchName (commitSHA)"
    /// </summary>
    private string ExtractBranchName(string displayText)
    {
        if (string.IsNullOrEmpty(displayText))
            return displayText;

        // Check if the text contains " (" which indicates SHA is appended
        var parenIndex = displayText.LastIndexOf(" (");
        if (parenIndex > 0 && displayText.EndsWith(")"))
        {
            return displayText.Substring(0, parenIndex);
        }

        return displayText;
    }
}
