using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Link context menu event handlers - URL Summarize feature.
/// </summary>
public sealed partial class MainWindow
{
    // ========================================
    // LINK MENU - SUMMARIZE FEATURE
    // ========================================

    private async void LinkMenu_Summarize_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        // Only works for URL links
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusText.Text = "Summarize is only available for HTTP/HTTPS URLs";
            return;
        }

        // Create cancellation token source
        var cts = new System.Threading.CancellationTokenSource();

        // Show busy dialog with cancel button
        var busyContent = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        busyContent.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 50,
            Height = 50
        });
        
        busyContent.Children.Add(new TextBlock
        {
            Text = $"Summarizing URL...\n{link.Url}",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            MaxWidth = 400
        });

        var busyDialog = new ContentDialog
        {
            Title = "Fetching Summary",
            Content = busyContent,
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        // Handle cancel
        busyDialog.CloseButtonClick += (s, args) =>
        {
            cts.Cancel();
        };

        StatusText.Text = $"Summarizing URL: {link.Url}...";

        // Start the summarize task
        var summaryTask = Task.Run(async () =>
        {
            var webSummaryService = new WebSummaryService();
            return await webSummaryService.SummarizeUrlAsync(link.Url, cts.Token);
        });

        // Show the busy dialog (it will be dismissed when we hide it)
        var dialogTask = busyDialog.ShowAsync();

        // Wait for the summary to complete
        WebPageSummary? summary = null;
        try
        {
            summary = await summaryTask;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Summarize cancelled";
            busyDialog.Hide();
            return;
        }
        catch (Exception ex)
        {
            busyDialog.Hide();
            await ShowErrorDialogAsync("Error", $"An error occurred while summarizing the URL:\n\n{ex.Message}");
            StatusText.Text = $"Error summarizing URL: {ex.Message}";
            return;
        }

        // Hide the busy dialog
        busyDialog.Hide();

        if (summary == null || summary.WasCancelled)
        {
            StatusText.Text = "Summarize cancelled";
            return;
        }

        if (!summary.Success)
        {
            await ShowErrorDialogAsync("Summarize Failed", $"Could not summarize the URL:\n\n{summary.ErrorMessage}");
            StatusText.Text = $"Failed to summarize: {summary.ErrorMessage}";
            return;
        }

        // Build the new description text
        var descriptionBuilder = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(summary.Description))
        {
            descriptionBuilder.AppendLine(summary.Description);
        }

        if (!string.IsNullOrWhiteSpace(summary.ContentSummary) && summary.ContentSummary != summary.Description)
        {
            if (descriptionBuilder.Length > 0)
                descriptionBuilder.AppendLine();
            descriptionBuilder.AppendLine(summary.ContentSummary);
        }

        if (!string.IsNullOrWhiteSpace(summary.Author) || !string.IsNullOrWhiteSpace(summary.PublishedDate))
        {
            if (descriptionBuilder.Length > 0)
                descriptionBuilder.AppendLine();
            if (!string.IsNullOrWhiteSpace(summary.Author))
                descriptionBuilder.AppendLine($"Author: {summary.Author}");
            if (!string.IsNullOrWhiteSpace(summary.PublishedDate))
                descriptionBuilder.AppendLine($"Published: {summary.PublishedDate}");
        }

        var newDescription = descriptionBuilder.ToString().Trim();
        var newKeywords = summary.Keywords.Count > 0 ? string.Join(", ", summary.Keywords) : string.Empty;

        // Build the comparison dialog with Edit Current vs Summary columns
        var mainGrid = new Grid
        {
            ColumnSpacing = 20,
            RowSpacing = 12,
            Width = 900
        };
        
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;

        // Store editable text boxes for saving
        TextBox? currentTitleBox = null;
        TextBox? currentDescriptionBox = null;
        TextBox? currentKeywordsBox = null;
        
        // Store summary text boxes for deferred text setting
        TextBox? summaryTitleBox = null;
        TextBox? summaryDescriptionBox = null;
        TextBox? summaryKeywordsBox = null;

        // Helper to add a comparison row
        void AddComparisonRow(string label, string currentValue, string newValue, ref TextBox? currentTextBoxRef, ref TextBox? summaryTextBoxRef, bool isDescription = false)
        {
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Label spanning both columns
            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, row > 0 ? 8 : 0, 0, 4)
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumnSpan(labelBlock, 2);
            mainGrid.Children.Add(labelBlock);
            row++;

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Set height based on whether this is a description field
            int fieldHeight = isDescription ? 250 : 80;

            // Edit Current value column
            var currentPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            currentPanel.Children.Add(new TextBlock
            {
                Text = "Edit Current:",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            var currentTextBox = new TextBox
            {
                PlaceholderText = "(empty)",
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = fieldHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ScrollViewer.SetVerticalScrollBarVisibility(currentTextBox, ScrollBarVisibility.Auto);
            currentPanel.Children.Add(currentTextBox);
            Grid.SetRow(currentPanel, row);
            Grid.SetColumn(currentPanel, 0);
            mainGrid.Children.Add(currentPanel);

            // Store reference to editable text box
            currentTextBoxRef = currentTextBox;

            // New/Summary value column (read-only for copying)
            var newPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            newPanel.Children.Add(new TextBlock
            {
                Text = "Summary:",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
            });
            var newTextBox = new TextBox
            {
                PlaceholderText = "(empty)",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = fieldHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Foreground = string.IsNullOrWhiteSpace(newValue) 
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) 
                    : null
            };
            ScrollViewer.SetVerticalScrollBarVisibility(newTextBox, ScrollBarVisibility.Auto);
            newPanel.Children.Add(newTextBox);
            Grid.SetRow(newPanel, row);
            Grid.SetColumn(newPanel, 1);
            mainGrid.Children.Add(newPanel);
            
            // Store reference to summary text box
            summaryTextBoxRef = newTextBox;
            
            row++;
        }

        // Add comparison rows (text will be set after controls are added to visual tree)
        AddComparisonRow("Title", link.Title, summary.Title, ref currentTitleBox, ref summaryTitleBox);
        AddComparisonRow("Description", link.Description, newDescription, ref currentDescriptionBox, ref summaryDescriptionBox, isDescription: true);
        AddComparisonRow("Keywords", link.Keywords, newKeywords, ref currentKeywordsBox, ref summaryKeywordsBox);

        // Set text AFTER controls are created to avoid WinUI TextBox truncation issue
        if (currentTitleBox != null) currentTitleBox.Text = link.Title ?? string.Empty;
        if (currentDescriptionBox != null) currentDescriptionBox.Text = link.Description ?? string.Empty;
        if (currentKeywordsBox != null) currentKeywordsBox.Text = link.Keywords ?? string.Empty;
        if (summaryTitleBox != null) summaryTitleBox.Text = summary.Title ?? string.Empty;
        if (summaryDescriptionBox != null) summaryDescriptionBox.Text = newDescription ?? string.Empty;
        if (summaryKeywordsBox != null) summaryKeywordsBox.Text = newKeywords ?? string.Empty;

        // Add metadata info if available
        if (!string.IsNullOrWhiteSpace(summary.SiteName) || !string.IsNullOrWhiteSpace(summary.ContentType))
        {
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var metadataText = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(summary.SiteName))
                metadataText.AppendLine($"?? Site: {summary.SiteName}");
            if (!string.IsNullOrWhiteSpace(summary.ContentType))
                metadataText.AppendLine($"?? Type: {summary.ContentType}");

            var metadataBlock = new TextBlock
            {
                Text = metadataText.ToString().Trim(),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 12, 0, 0)
            };
            Grid.SetRow(metadataBlock, row);
            Grid.SetColumnSpan(metadataBlock, 2);
            mainGrid.Children.Add(metadataBlock);
        }

        // Show the comparison dialog with wider content
        var compareDialog = new ContentDialog
        {
            Title = "URL Summary - Compare & Edit",
            Content = new ScrollViewer
            {
                Content = mainGrid,
                MaxHeight = 700,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            },
            PrimaryButtonText = "Save Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        // Override the default ContentDialog width constraint
        compareDialog.Resources["ContentDialogMaxWidth"] = 1000.0;

        var result = await compareDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Save the edited current values
            if (currentTitleBox != null)
            {
                link.Title = currentTitleBox.Text.Trim();
            }

            if (currentDescriptionBox != null)
            {
                link.Description = currentDescriptionBox.Text.Trim();
            }

            if (currentKeywordsBox != null)
            {
                link.Keywords = currentKeywordsBox.Text.Trim();
            }

            link.ModifiedDate = DateTime.Now;

            var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
            _contextMenuNode = updatedNode;

            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Saved changes to: {link.Title}";
        }
        else
        {
            StatusText.Text = "Summary cancelled";
        }
    }
}
