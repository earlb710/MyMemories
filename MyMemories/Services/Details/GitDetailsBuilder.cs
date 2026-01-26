using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace MyMemories.Services.Details;

/// <summary>
/// Builds Git repository details content for the details panel.
/// </summary>
public class GitDetailsBuilder
{
    private readonly StackPanel _detailsPanel;

    public GitDetailsBuilder(StackPanel detailsPanel)
    {
        _detailsPanel = detailsPanel;
    }

    /// <summary>
    /// Adds Git repository information to the details panel.
    /// </summary>
    public async Task AddGitRepositoryInfoAsync(LinkItem linkItem)
    {
        try
        {
            string gitDir = Path.Combine(linkItem.Url, ".git");
            if (!Directory.Exists(gitDir))
                return;

            using var repo = new Repository(linkItem.Url);

            // Create Git info section
            _detailsPanel.Children.Add(new TextBlock
            {
                Text = "Git Repository",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                IsTextSelectionEnabled = true
            });

            var gitInfoPanel = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Current branch
            if (!repo.Info.IsHeadDetached && repo.Head != null)
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Branch:", repo.Head.FriendlyName));
            }
            else if (repo.Info.IsHeadDetached)
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Branch:", "HEAD detached"));
            }

            // Latest commit info
            var latestCommit = repo.Head?.Tip;
            if (latestCommit != null)
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Latest Commit:", latestCommit.MessageShort));
                gitInfoPanel.Children.Add(CreateGitInfoLine("Commit SHA:", latestCommit.Sha.Substring(0, 8)));
                gitInfoPanel.Children.Add(CreateGitInfoLine("Author:", $"{latestCommit.Author.Name} <{latestCommit.Author.Email}>"));
                gitInfoPanel.Children.Add(CreateGitInfoLine("Date:", latestCommit.Author.When.DateTime.ToString("yyyy-MM-dd HH:mm:ss")));
            }

            // Remote info
            var remote = repo.Network.Remotes.FirstOrDefault(r => r.Name == "origin");
            if (remote != null)
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Remote URL:", remote.Url));
            }

            // Repository status
            var status = repo.RetrieveStatus();
            int modifiedCount = status.Modified.Count() + status.Added.Count() + status.Removed.Count();
            if (modifiedCount > 0)
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Local Changes:", $"{modifiedCount} file(s) modified"));
            }
            else
            {
                gitInfoPanel.Children.Add(CreateGitInfoLine("Status:", "Working tree clean"));
            }

            _detailsPanel.Children.Add(gitInfoPanel);

            // Add pull button if there's a remote
            if (remote != null && repo.Head?.TrackedBranch != null)
            {
                await AddPullButtonAsync(repo, linkItem.Url);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddGitRepositoryInfoAsync] Error: {ex.Message}");
            DetailsUIHelpers.AddWarning(_detailsPanel, $"Unable to read Git repository information: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a Git info line with label and value.
    /// </summary>
    private StackPanel CreateGitInfoLine(string label, string value)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Width = 120,
            IsTextSelectionEnabled = true
        });

        panel.Children.Add(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.Gray),
            IsTextSelectionEnabled = true
        });

        return panel;
    }

    /// <summary>
    /// Adds a pull button that checks for remote changes and is only enabled if there are changes to pull.
    /// Fetches from remote to compare current branch hash with repository.
    /// </summary>
    private async Task AddPullButtonAsync(Repository repo, string repoPath)
    {
        try
        {
            var pullButton = new Button
            {
                Content = "Pull Changes",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var statusText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray)
            };

            // Initially show checking status
            statusText.Text = "Checking for remote changes...";

            // Check if there are changes to pull
            await Task.Run(() =>
            {
                try
                {
                    var remoteBranch = repo.Head?.TrackedBranch;
                    if (remoteBranch == null)
                    {
                        pullButton.DispatcherQueue.TryEnqueue(() =>
                        {
                            pullButton.IsEnabled = false;
                            statusText.Text = "No tracking branch configured";
                        });
                        return;
                    }

                    // Attempt to fetch from remote to get the latest commit hashes
                    var remote = repo.Network.Remotes["origin"];
                    bool fetchSucceeded = false;
                    string fetchError = string.Empty;
                    
                    if (remote != null)
                    {
                        try
                        {
                            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                            
                            // Track credential attempts to avoid infinite retry loops
                            int fetchCredentialAttempts = 0;
                            
                            var fetchOptions = new FetchOptions
                            {
                                CredentialsProvider = (url, usernameFromUrl, types) =>
                                {
                                    try
                                    {
                                        fetchCredentialAttempts++;
                                        
                                        // Only provide credentials on the first attempt
                                        // Return null on subsequent attempts to signal authentication failure
                                        if (fetchCredentialAttempts > 1)
                                        {
                                            return null;
                                        }
                                        
                                        // Provide empty credentials to support anonymous HTTPS access to public repositories.
                                        // For repositories requiring authentication, users will need to configure Git credentials separately.
                                        return new UsernamePasswordCredentials
                                        {
                                            Username = string.Empty,
                                            Password = string.Empty
                                        };
                                    }
                                    catch (Exception credEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Credential provider error: {credEx.Message}");
                                        return null;
                                    }
                                },
                                OnTransferProgress = progress =>
                                {
                                    return true;
                                }
                            };
                            
                            LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "Checking for updates");
                            fetchSucceeded = true;
                        }
                        catch (Exception ex)
                        {
                            fetchError = ex.Message;
                            System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Fetch failed: {ex.Message}");
                        }
                    }

                    // After fetch attempt, compare commits
                    var localCommit = repo.Head?.Tip;
                    var remoteCommit = remoteBranch?.Tip;

                    if (localCommit == null || remoteCommit == null)
                    {
                        pullButton.DispatcherQueue.TryEnqueue(() =>
                        {
                            pullButton.IsEnabled = false;
                            statusText.Text = fetchSucceeded 
                                ? "Unable to determine commit status" 
                                : $"Unable to check remote (fetch failed: {fetchError})";
                        });
                        return;
                    }

                    bool hasChanges = localCommit.Sha != remoteCommit.Sha;

                    pullButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        pullButton.IsEnabled = hasChanges;
                        if (hasChanges)
                        {
                            var behindBy = repo.ObjectDatabase.CalculateHistoryDivergence(localCommit, remoteCommit).BehindBy ?? 0;
                            statusText.Text = behindBy > 0 
                                ? $"Behind by {behindBy} commit(s)" 
                                : "Remote has new changes";
                        }
                        else
                        {
                            statusText.Text = "Already up to date";
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Check error: {ex.Message}");
                    pullButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        pullButton.IsEnabled = false;
                        statusText.Text = $"Error checking remote: {ex.Message}";
                    });
                }
            });

            pullButton.Click += async (sender, e) =>
            {
                pullButton.IsEnabled = false;
                statusText.Text = "Pulling changes...";
                statusText.Foreground = new SolidColorBrush(Colors.Gray);

                await Task.Run(() =>
                {
                    try
                    {
                        // Track credential attempts to avoid infinite retry loops
                        int pullCredentialAttempts = 0;
                        
                        var pullOptions = new PullOptions
                        {
                            FetchOptions = new FetchOptions
                            {
                                CredentialsProvider = (url, usernameFromUrl, types) =>
                                {
                                    try
                                    {
                                        pullCredentialAttempts++;
                                        
                                        // Only provide credentials on the first attempt
                                        // Return null on subsequent attempts to signal authentication failure
                                        if (pullCredentialAttempts > 1)
                                        {
                                            return null;
                                        }
                                        
                                        // Provide empty credentials to support anonymous HTTPS access to public repositories.
                                        // For repositories requiring authentication, users will need to configure Git credentials separately.
                                        return new UsernamePasswordCredentials
                                        {
                                            Username = string.Empty,
                                            Password = string.Empty
                                        };
                                    }
                                    catch (Exception credEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Pull credential provider error: {credEx.Message}");
                                        return null;
                                    }
                                }
                            }
                        };

                        var signature = new Signature("MyMemories", "noreply@mymemories.local", DateTimeOffset.Now);
                        LibGit2Sharp.Commands.Pull(repo, signature, pullOptions);

                        pullButton.DispatcherQueue.TryEnqueue(() =>
                        {
                            statusText.Text = "Successfully pulled changes";
                            statusText.Foreground = new SolidColorBrush(Colors.Green);
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Pull error: {ex.Message}");
                        pullButton.DispatcherQueue.TryEnqueue(() =>
                        {
                            pullButton.IsEnabled = true;
                            statusText.Text = $"Pull failed: {ex.Message}";
                            statusText.Foreground = new SolidColorBrush(Colors.Red);
                        });
                    }
                });
            };

            _detailsPanel.Children.Add(pullButton);
            _detailsPanel.Children.Add(statusText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddPullButtonAsync] Error: {ex.Message}");
        }
    }
}
