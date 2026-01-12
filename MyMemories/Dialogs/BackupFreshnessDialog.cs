using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for displaying and managing outdated backups.
/// </summary>
public class BackupFreshnessDialog
{
    private readonly XamlRoot _xamlRoot;

    public BackupFreshnessDialog(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    /// <summary>
    /// Shows a dialog listing outdated backups and allows the user to choose which to update.
    /// </summary>
    /// <param name="outdatedBackups">List of outdated backups to display.</param>
    /// <returns>List of backups to update, or null if cancelled/ignored all.</returns>
    public async Task<List<OutdatedBackup>?> ShowAsync(List<OutdatedBackup> outdatedBackups)
    {
        if (outdatedBackups.Count == 0)
            return null;

        var mainPanel = new StackPanel { Spacing = 16, MinWidth = 600 };

        // Header
        var headerPanel = new StackPanel { Spacing = 4 };
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"Found {outdatedBackups.Count} backup(s) that are out of date",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = "Select which backups to update. Unchecked items will be ignored.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        });
        mainPanel.Children.Add(headerPanel);

        // Create checkboxes for each outdated backup
        var checkBoxes = new List<(CheckBox checkBox, OutdatedBackup backup)>();

        var listPanel = new StackPanel { Spacing = 8 };

        foreach (var backup in outdatedBackups)
        {
            var itemPanel = new Grid();
            itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var checkBox = new CheckBox
            {
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 8, 0)
            };
            Grid.SetColumn(checkBox, 0);
            itemPanel.Children.Add(checkBox);

            var detailsPanel = new StackPanel { Spacing = 2 };

            // Item name with type icon
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            namePanel.Children.Add(new FontIcon
            {
                Glyph = backup.ItemType == BackupItemType.Category ? "\uE8D2" : "\uE8B7", // Folder or Package
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = backup.ItemName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = backup.ItemType == BackupItemType.Category ? "(Category)" : "(Zip Archive)",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center
            });
            detailsPanel.Children.Add(namePanel);

            // Backup path
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"Backup: {backup.BackupPath}",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Time difference - use FontIcon instead of emoji
            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            
            if (backup.BackupModified == DateTime.MinValue)
            {
                // Warning icon for missing backup
                statusPanel.Children.Add(new FontIcon
                {
                    Glyph = "\uE7BA", // Warning icon
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    VerticalAlignment = VerticalAlignment.Center
                });
                statusPanel.Children.Add(new TextBlock
                {
                    Text = "Backup does not exist",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            else
            {
                // Clock icon for outdated backup
                statusPanel.Children.Add(new FontIcon
                {
                    Glyph = "\uE823", // Clock icon
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    VerticalAlignment = VerticalAlignment.Center
                });
                
                var diff = backup.TimeDifference;
                string timeDiffText;
                if (diff.TotalDays >= 1)
                    timeDiffText = $"{diff.TotalDays:F0} day(s) behind";
                else if (diff.TotalHours >= 1)
                    timeDiffText = $"{diff.TotalHours:F0} hour(s) behind";
                else
                    timeDiffText = $"{diff.TotalMinutes:F0} minute(s) behind";
                    
                statusPanel.Children.Add(new TextBlock
                {
                    Text = timeDiffText,
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            detailsPanel.Children.Add(statusPanel);

            Grid.SetColumn(detailsPanel, 1);
            itemPanel.Children.Add(detailsPanel);

            listPanel.Children.Add(itemPanel);
            checkBoxes.Add((checkBox, backup));
        }

        // Wrap in ScrollViewer
        var scrollViewer = new ScrollViewer
        {
            Content = listPanel,
            MaxHeight = 350,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        mainPanel.Children.Add(scrollViewer);

        // Select All / Select None buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var selectAllButton = new Button { Content = "Select All" };
        selectAllButton.Click += (s, e) =>
        {
            foreach (var (cb, _) in checkBoxes)
                cb.IsChecked = true;
        };
        buttonPanel.Children.Add(selectAllButton);

        var selectNoneButton = new Button { Content = "Select None" };
        selectNoneButton.Click += (s, e) =>
        {
            foreach (var (cb, _) in checkBoxes)
                cb.IsChecked = false;
        };
        buttonPanel.Children.Add(selectNoneButton);

        mainPanel.Children.Add(buttonPanel);

        var dialog = new ContentDialog
        {
            Title = "Outdated Backups Detected",
            Content = mainPanel,
            PrimaryButtonText = "Update Selected",
            SecondaryButtonText = "Ignore All",
            CloseButtonText = "Remind Later",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Return backups that are checked for update
            var toUpdate = new List<OutdatedBackup>();
            foreach (var (checkBox, backup) in checkBoxes)
            {
                backup.ShouldUpdate = checkBox.IsChecked ?? false;
                if (backup.ShouldUpdate)
                    toUpdate.Add(backup);
            }
            return toUpdate;
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // Ignore all - return empty list
            return new List<OutdatedBackup>();
        }

        // Close/Remind Later - return null (will check again next startup)
        return null;
    }

    /// <summary>
    /// Shows a progress dialog while updating backups and returns the results.
    /// </summary>
    /// <param name="backupsToUpdate">List of backups to update.</param>
    /// <param name="freshnessService">The service to perform the updates.</param>
    /// <returns>Tuple of (succeeded, failed) counts.</returns>
    public async Task<(int succeeded, int failed)> UpdateBackupsWithProgressAsync(
        List<OutdatedBackup> backupsToUpdate, 
        BackupFreshnessService freshnessService)
    {
        if (backupsToUpdate.Count == 0)
            return (0, 0);

        // Create progress dialog content
        var progressPanel = new StackPanel { Spacing = 16, MinWidth = 400 };

        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = backupsToUpdate.Count,
            Value = 0,
            IsIndeterminate = false,
            Height = 8
        };
        progressPanel.Children.Add(progressBar);

        var countText = new TextBlock
        {
            Text = $"0 / {backupsToUpdate.Count}",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        progressPanel.Children.Add(countText);

        var currentItemText = new TextBlock
        {
            Text = "Starting...",
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };
        progressPanel.Children.Add(currentItemText);

        var dialog = new ContentDialog
        {
            Title = "Updating Backups",
            Content = progressPanel,
            XamlRoot = _xamlRoot
        };

        // Start the dialog without awaiting (we'll close it manually)
        var dialogTask = dialog.ShowAsync();

        int succeeded = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < backupsToUpdate.Count; i++)
            {
                var backup = backupsToUpdate[i];

                // Update UI
                progressBar.Value = i;
                countText.Text = $"{i + 1} / {backupsToUpdate.Count}";
                currentItemText.Text = $"Copying: {backup.ItemName}";

                try
                {
                    // Ensure the backup directory exists
                    var backupDir = System.IO.Path.GetDirectoryName(backup.BackupPath);
                    if (!string.IsNullOrEmpty(backupDir) && !System.IO.Directory.Exists(backupDir))
                    {
                        System.IO.Directory.CreateDirectory(backupDir);
                    }

                    // Copy the file
                    await Task.Run(() => System.IO.File.Copy(backup.SourcePath, backup.BackupPath, overwrite: true));
                    succeeded++;
                }
                catch
                {
                    failed++;
                }

                // Small delay to show progress
                await Task.Delay(50);
            }

            // Final update
            progressBar.Value = backupsToUpdate.Count;
            countText.Text = $"{backupsToUpdate.Count} / {backupsToUpdate.Count}";
            currentItemText.Text = "Complete!";

            await Task.Delay(500); // Brief pause to show completion
        }
        finally
        {
            // Close the progress dialog
            dialog.Hide();
        }

        return (succeeded, failed);
    }

    /// <summary>
    /// Shows a summary dialog after updating backups.
    /// </summary>
    public async Task ShowUpdateSummaryAsync(int succeeded, int failed)
    {
        var contentPanel = new StackPanel { Spacing = 12 };

        if (succeeded > 0)
        {
            var successPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            successPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE73E", // Checkmark
                FontSize = 16,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
            });
            successPanel.Children.Add(new TextBlock
            {
                Text = $"Successfully updated {succeeded} backup(s)",
                VerticalAlignment = VerticalAlignment.Center
            });
            contentPanel.Children.Add(successPanel);
        }

        if (failed > 0)
        {
            var failPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            failPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE711", // X mark
                FontSize = 16,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
            });
            failPanel.Children.Add(new TextBlock
            {
                Text = $"Failed to update {failed} backup(s)",
                VerticalAlignment = VerticalAlignment.Center
            });
            contentPanel.Children.Add(failPanel);

            contentPanel.Children.Add(new TextBlock
            {
                Text = "Check if the backup directories are accessible.",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
        }

        var dialog = new ContentDialog
        {
            Title = failed == 0 ? "Backups Updated" : "Backup Update Results",
            Content = contentPanel,
            CloseButtonText = "OK",
            XamlRoot = _xamlRoot
        };

        await dialog.ShowAsync();
    }
}
