using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for assigning, viewing, and editing ratings on categories and links.
/// Shows all rating types from the template with checkboxes to include/exclude.
/// </summary>
public class RatingAssignmentDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly RatingManagementService _ratingService;

    public RatingAssignmentDialog(XamlRoot xamlRoot, RatingManagementService ratingService)
    {
        _xamlRoot = xamlRoot;
        _ratingService = ratingService;
    }

    /// <summary>
    /// Shows the rating assignment dialog for an item.
    /// </summary>
    /// <param name="itemName">Name of the item being rated.</param>
    /// <param name="currentRatings">Current list of ratings on the item.</param>
    /// <returns>Updated list of ratings, or null if cancelled.</returns>
    public async Task<List<RatingValue>?> ShowAsync(string itemName, List<RatingValue> currentRatings)
    {
        if (_ratingService.DefinitionCount == 0)
        {
            var errorDialog = new ContentDialog
            {
                Title = "No Rating Types",
                Content = "No rating types have been defined in this template.\n\nGo to Configuration > Rating Management to create rating types first.",
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };
            await errorDialog.ShowAsync();
            return null;
        }

        // Build a dictionary of current ratings by qualified name or legacy GUID
        var ratingsByQualifiedName = new Dictionary<string, RatingValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var rating in currentRatings)
        {
            // Get the definition to map legacy GUIDs to qualified names
            var def = _ratingService.GetDefinition(rating.Rating);
            if (def != null)
            {
                var qualifiedName = _ratingService.GetQualifiedName(def);
                ratingsByQualifiedName[qualifiedName] = rating;
            }
            else
            {
                // Keep original rating name for orphaned ratings
                ratingsByQualifiedName[rating.Rating] = rating;
            }
        }

        // Create rating row controls for each definition
        var ratingRows = new List<RatingRowControl>();

        var mainPanel = new StackPanel { Spacing = 8, MinWidth = 500 };

        // Header
        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Rate '{itemName}'",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 8)
        });

        mainPanel.Children.Add(new TextBlock
        {
            Text = "Check the ratings you want to include, then set the score using the slider.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Create a row for each rating definition
        foreach (var definition in _ratingService.Definitions)
        {
            var qualifiedName = _ratingService.GetQualifiedName(definition);
            var existingRating = ratingsByQualifiedName.GetValueOrDefault(qualifiedName);
            var row = CreateRatingRow(definition, existingRating);
            ratingRows.Add(row);
            mainPanel.Children.Add(row.Container);
        }

        // Summary text
        var summaryText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 12, 0, 0)
        };
        UpdateSummary(summaryText, ratingRows);
        mainPanel.Children.Add(summaryText);

        // Update summary when checkboxes change
        foreach (var row in ratingRows)
        {
            row.CheckBox.Checked += (s, e) => UpdateSummary(summaryText, ratingRows);
            row.CheckBox.Unchecked += (s, e) => UpdateSummary(summaryText, ratingRows);
        }

        var dialog = new ContentDialog
        {
            Title = "Assign Ratings",
            Content = new ScrollViewer
            {
                Content = mainPanel,
                MaxHeight = 500
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Build the result list from checked rows using qualified names
            var resultRatings = new List<RatingValue>();
            
            foreach (var row in ratingRows)
            {
                if (row.CheckBox.IsChecked == true)
                {
                    var qualifiedName = _ratingService.GetQualifiedName(row.Definition);
                    var existingRating = ratingsByQualifiedName.GetValueOrDefault(qualifiedName);
                    
                    resultRatings.Add(new RatingValue
                    {
                        Rating = qualifiedName,
                        Score = (int)row.Slider.Value,
                        Reason = row.ReasonTextBox?.Text?.Trim() ?? string.Empty,
                        CreatedDate = existingRating?.CreatedDate ?? DateTime.Now,
                        ModifiedDate = DateTime.Now
                    });
                }
            }

            return resultRatings;
        }

        return null;
    }

    private void UpdateSummary(TextBlock summaryText, List<RatingRowControl> rows)
    {
        var checkedCount = rows.Count(r => r.CheckBox.IsChecked == true);
        summaryText.Text = checkedCount > 0
            ? $"{checkedCount} rating(s) will be saved"
            : "No ratings selected";
    }

    private RatingRowControl CreateRatingRow(RatingDefinition definition, RatingValue? existingRating)
    {
        var isChecked = existingRating != null;
        var currentScore = existingRating?.Score ?? Math.Max(0, definition.MinScore);
        currentScore = Math.Clamp(currentScore, definition.MinScore, definition.MaxScore);

        // Main container
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var mainStack = new StackPanel { Spacing = 8 };

        // Row 1: Checkbox + Name + Score Badge
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Score badge

        // Checkbox
        var checkBox = new CheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(checkBox, 0);
        headerRow.Children.Add(checkBox);

        // Name and description
        var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        namePanel.Children.Add(new TextBlock
        {
            Text = definition.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        });
        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            namePanel.Children.Add(new TextBlock
            {
                Text = definition.Description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }
        Grid.SetColumn(namePanel, 1);
        headerRow.Children.Add(namePanel);

        // Score badge
        var scoreBadge = new Border
        {
            MinWidth = 50,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(RatingManagementService.GetScoreColor(currentScore)),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 4,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = RatingManagementService.GetScoreIconGlyph(currentScore),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                    },
                    new TextBlock
                    {
                        Text = RatingManagementService.FormatScore(currentScore),
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                    }
                }
            }
        };
        Grid.SetColumn(scoreBadge, 2);
        headerRow.Children.Add(scoreBadge);

        mainStack.Children.Add(headerRow);

        // Row 2: Slider (only visible when checked)
        var sliderPanel = new StackPanel
        {
            Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(28, 0, 0, 0) // Indent to align with name
        };

        // Slider with range labels
        var sliderRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var minLabel = new TextBlock
        {
            Text = definition.MinScore.ToString(),
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(minLabel, 0);
        sliderRow.Children.Add(minLabel);

        var slider = new Slider
        {
            Minimum = definition.MinScore,
            Maximum = definition.MaxScore,
            Value = currentScore,
            StepFrequency = 1,
            TickFrequency = 1,
            TickPlacement = TickPlacement.BottomRight,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(slider, 1);
        sliderRow.Children.Add(slider);

        var maxLabel = new TextBlock
        {
            Text = definition.MaxScore.ToString(),
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(maxLabel, 2);
        sliderRow.Children.Add(maxLabel);

        sliderPanel.Children.Add(sliderRow);

        // Optional reason text box
        var reasonTextBox = new TextBox
        {
            Text = existingRating?.Reason ?? string.Empty,
            PlaceholderText = "Reason (optional)",
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            MaxHeight = 60
        };
        sliderPanel.Children.Add(reasonTextBox);
        
        // Timestamp display (if rating exists)
        if (existingRating != null)
        {
            var timestampText = new TextBlock
            {
                FontSize = 9,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 4, 0, 0)
            };
            
            if (existingRating.ModifiedDate > existingRating.CreatedDate.AddSeconds(5))
            {
                timestampText.Text = $"Last modified: {existingRating.ModifiedDate:g}";
            }
            else
            {
                timestampText.Text = $"Created: {existingRating.CreatedDate:g}";
            }
            
            sliderPanel.Children.Add(timestampText);
        }

        mainStack.Children.Add(sliderPanel);

        border.Child = mainStack;

        // Get references to score badge content for updates
        var scoreBadgePanel = (StackPanel)scoreBadge.Child;
        var scoreIcon = (FontIcon)scoreBadgePanel.Children[0];
        var scoreText = (TextBlock)scoreBadgePanel.Children[1];

        // Update score badge when slider changes
        slider.ValueChanged += (s, e) =>
        {
            var score = (int)e.NewValue;
            scoreIcon.Glyph = RatingManagementService.GetScoreIconGlyph(score);
            scoreText.Text = RatingManagementService.FormatScore(score);
            scoreBadge.Background = new SolidColorBrush(RatingManagementService.GetScoreColor(score));
        };

        // Show/hide slider panel based on checkbox
        checkBox.Checked += (s, e) =>
        {
            sliderPanel.Visibility = Visibility.Visible;
            border.BorderBrush = new SolidColorBrush(RatingManagementService.GetScoreColor((int)slider.Value));
        };

        checkBox.Unchecked += (s, e) =>
        {
            sliderPanel.Visibility = Visibility.Collapsed;
            border.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
        };

        // Set initial border color if checked
        if (isChecked)
        {
            border.BorderBrush = new SolidColorBrush(RatingManagementService.GetScoreColor(currentScore));
        }

        // Update border color when slider changes (if checked)
        slider.ValueChanged += (s, e) =>
        {
            if (checkBox.IsChecked == true)
            {
                border.BorderBrush = new SolidColorBrush(RatingManagementService.GetScoreColor((int)e.NewValue));
            }
        };

        return new RatingRowControl
        {
            Container = border,
            Definition = definition,
            CheckBox = checkBox,
            Slider = slider,
            ScoreBadge = scoreBadge,
            ReasonTextBox = reasonTextBox
        };
    }

    private class RatingRowControl
    {
        public required Border Container { get; init; }
        public required RatingDefinition Definition { get; init; }
        public required CheckBox CheckBox { get; init; }
        public required Slider Slider { get; init; }
        public required Border ScoreBadge { get; init; }
        public TextBox? ReasonTextBox { get; init; }
    }
}
