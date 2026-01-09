using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Validates configuration directories on startup and offers to create missing ones.
    /// </summary>
    /// <returns>True if validation passed or user fixed issues; False if critical errors remain.</returns>
    private async Task<bool> ValidateConfigurationDirectoriesAsync()
    {
        if (_configService == null)
            return false;

        var issues = new List<(string Type, string Path, string Issue)>();
        var workingDir = _configService.WorkingDirectory;
        var logDir = _configService.LogDirectory;

        // Validate working directory
        var workingValidation = PathValidationUtilities.ValidateDirectoryPath(workingDir, allowEmpty: false);
        if (!workingValidation.IsValid)
        {
            issues.Add(("Working Directory", workingDir, workingValidation.ErrorMessage ?? "Invalid path"));
        }
        else if (!Directory.Exists(workingDir))
        {
            issues.Add(("Working Directory", workingDir, "Directory does not exist"));
        }

        // Validate log directory if set
        if (!string.IsNullOrEmpty(logDir))
        {
            var logValidation = PathValidationUtilities.ValidateDirectoryPath(logDir, allowEmpty: true);
            if (!logValidation.IsValid)
            {
                issues.Add(("Log Directory", logDir, logValidation.ErrorMessage ?? "Invalid path"));
            }
            else if (!Directory.Exists(logDir))
            {
                issues.Add(("Log Directory", logDir, "Directory does not exist"));
            }
        }

        // Check for write access if directories exist
        if (Directory.Exists(workingDir))
        {
            if (!await TestDirectoryWriteAccessAsync(workingDir))
            {
                issues.Add(("Working Directory", workingDir, "No write access"));
            }
        }

        if (!string.IsNullOrEmpty(logDir) && Directory.Exists(logDir))
        {
            if (!await TestDirectoryWriteAccessAsync(logDir))
            {
                issues.Add(("Log Directory", logDir, "No write access"));
            }
        }

        // If no issues, return success
        if (!issues.Any())
            return true;

        // Build issue message
        var issueMessage = "Configuration validation found the following issues:\n\n";
        var canAutoFix = true;

        foreach (var issue in issues)
        {
            issueMessage += $"• {issue.Type}: {issue.Issue}\n  Path: {issue.Path}\n\n";
            
            // Can't auto-fix invalid paths or permission issues
            if (issue.Issue.Contains("Invalid") || issue.Issue.Contains("write access"))
            {
                canAutoFix = false;
            }
        }

        if (canAutoFix)
        {
            issueMessage += "Would you like to create the missing directories?";

            var fixDialog = new ContentDialog
            {
                Title = "Configuration Issues Detected",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = issueMessage,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                PrimaryButtonText = "Create Directories",
                SecondaryButtonText = "Open Settings",
                CloseButtonText = "Continue Anyway",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await fixDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Try to create missing directories
                var failedDirs = new List<string>();

                foreach (var issue in issues.Where(i => i.Issue == "Directory does not exist"))
                {
                    if (!await PathValidationUtilities.EnsureDirectoryExistsAsync(issue.Path))
                    {
                        failedDirs.Add($"{issue.Type}: {issue.Path}");
                    }
                    else
                    {
                        StatusText.Text = $"Created {issue.Type.ToLower()}: {issue.Path}";
                        
                        // Log the directory creation
                        if (_configService.IsLoggingEnabled())
                        {
                            await _configService.LogErrorAsync($"Created missing {issue.Type.ToLower()} during startup validation");
                        }
                    }
                }

                if (failedDirs.Any())
                {
                    await ShowErrorDialogAsync(
                        "Failed to Create Directories",
                        $"Could not create the following directories:\n\n{string.Join("\n", failedDirs)}\n\n" +
                        "Please check permissions or manually create these directories."
                    );
                    return false;
                }

                return true; // Successfully created directories
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Open settings dialog
                await ShowDirectorySetupDialogAsync();
                
                // Revalidate after settings change
                return await ValidateConfigurationDirectoriesAsync();
            }
            else
            {
                // Continue anyway - log warning
                StatusText.Text = "?? Warning: Configuration issues detected but ignored";
                
                if (_configService.IsLoggingEnabled())
                {
                    await _configService.LogErrorAsync("Configuration validation issues ignored by user");
                }
                
                return true; // Allow app to continue
            }
        }
        else
        {
            // Can't auto-fix - show error and offer settings
            issueMessage += "\nThese issues require manual correction.\n\n" +
                           "Please update your configuration in Settings.";

            var errorDialog = new ContentDialog
            {
                Title = "Configuration Errors",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = issueMessage,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                PrimaryButtonText = "Open Settings",
                CloseButtonText = "Continue Anyway",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await errorDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ShowDirectorySetupDialogAsync();
                
                // Revalidate after settings change
                return await ValidateConfigurationDirectoriesAsync();
            }
            else
            {
                StatusText.Text = "?? Warning: Running with invalid configuration";
                return true; // Allow app to continue (risky)
            }
        }
    }

    /// <summary>
    /// Tests if the application has write access to a directory.
    /// </summary>
    private async Task<bool> TestDirectoryWriteAccessAsync(string directoryPath)
    {
        try
        {
            var testFile = Path.Combine(directoryPath, $".writetest_{Guid.NewGuid()}.tmp");
            
            await Task.Run(() =>
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            });
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}
