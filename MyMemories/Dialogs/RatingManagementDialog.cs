using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for managing rating definitions (create, edit, delete).
/// Supports multiple named templates (collections of rating definitions).
/// </summary>
public class RatingManagementDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly RatingManagementService _ratingService;
    private ContentDialog? _mainDialog;
    private ComboBox? _templateComboBox;

    public RatingManagementDialog(XamlRoot xamlRoot, RatingManagementService ratingService)
    {
        _xamlRoot = xamlRoot;
        _ratingService = ratingService;
    }

    /// <summary>
    /// Shows the rating management dialog with refreshed content.
    /// </summary>
    public async Task RefreshAndShowDialogAsync()
    {
        while (true)
        {
            var (panel, action) = BuildMainPanel();

            _mainDialog = new ContentDialog
            {
                Title = "? Rating Management",
                Content = panel,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot
            };

            var result = await _mainDialog.ShowAsync();
            _mainDialog = null;

            // Handle actions that require reopening the dialog
            if (action.ActionType == RatingActionType.None)
            {
                break; // User clicked Close
            }
            else if (action.ActionType == RatingActionType.Add)
            {
                var newDefinition = await ShowRatingEditorAsync(null);
                if (newDefinition != null)
                {
                    _ratingService.AddDefinition(newDefinition);
                    await _ratingService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == RatingActionType.Edit && action.Definition != null)
            {
                var editedDefinition = await ShowRatingEditorAsync(action.Definition);
                if (editedDefinition != null)
                {
                    _ratingService.UpdateDefinition(editedDefinition);
                    await _ratingService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == RatingActionType.Delete && action.Definition != null)
            {
                var confirmed = await ShowDeleteConfirmationAsync(action.Definition);
                if (confirmed)
                {
                    _ratingService.RemoveDefinition(action.Definition.Id);
                    await _ratingService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == RatingActionType.AddTemplate)
            {
                var templateName = await ShowAddTemplateDialogAsync();
                if (!string.IsNullOrWhiteSpace(templateName))
                {
                    _ratingService.CreateTemplate(templateName);
                    await _ratingService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == RatingActionType.DeleteTemplate && action.TemplateName != null)
            {
                var confirmed = await ShowDeleteTemplateConfirmationAsync(action.TemplateName);
                if (confirmed)
                {
                    _ratingService.DeleteTemplate(action.TemplateName);
                    await _ratingService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == RatingActionType.SwitchTemplate && action.TemplateName != null)
            {
                // TemplateName can be empty string for "Default" template
                _ratingService.SwitchTemplate(action.TemplateName);
                await _ratingService.SaveAsync();
                // Loop to show dialog again with new template
            }
        }
    }

    private (StackPanel Panel, RatingAction Action) BuildMainPanel()
    {
        var action = new RatingAction { ActionType = RatingActionType.None };
        
        var mainPanel = new StackPanel { Spacing = 10, MinWidth = 480 };

        // Rating Template section
        var templateSection = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };
        
        templateSection.Children.Add(new TextBlock
        {
            Text = "Rating Template",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12
        });

        // Use horizontal StackPanel for template row
        var templateRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        // Template ComboBox
        _templateComboBox = new ComboBox
        {
            MinWidth = 150,
            MaxWidth = 250
        };

        // Populate with existing templates
        var templates = _ratingService.GetTemplateNames();
        var currentTemplate = _ratingService.CurrentTemplateName;
        int selectedIndex = 0;

        for (int i = 0; i < templates.Count; i++)
        {
            var templateName = templates[i];
            var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
            _templateComboBox.Items.Add(new ComboBoxItem
            {
                Content = displayName,
                Tag = templateName
            });

            if (templateName == currentTemplate)
            {
                selectedIndex = i;
            }
        }

        // If no templates, add Default
        if (_templateComboBox.Items.Count == 0)
        {
            _templateComboBox.Items.Add(new ComboBoxItem
            {
                Content = "Default",
                Tag = ""
            });
        }

        _templateComboBox.SelectedIndex = selectedIndex;

        // Handle template selection change
        _templateComboBox.SelectionChanged += (s, e) =>
        {
            if (_templateComboBox.SelectedItem is ComboBoxItem item && item.Tag is string templateName)
            {
                // Compare with current template - both could be empty string for Default
                if (templateName != _ratingService.CurrentTemplateName)
                {
                    action.ActionType = RatingActionType.SwitchTemplate;
                    action.TemplateName = templateName; // Can be empty string for Default
                    _mainDialog?.Hide();
                }
            }
        };

        templateRow.Children.Add(_templateComboBox);

        // Plus button (add new template)
        var addTemplateButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE710", FontSize = 12 }, // Plus
            Padding = new Thickness(8, 4, 8, 4)
        };
        ToolTipService.SetToolTip(addTemplateButton, "Create new rating template");
        templateRow.Children.Add(addTemplateButton);

        addTemplateButton.Click += (s, e) =>
        {
            action.ActionType = RatingActionType.AddTemplate;
            _mainDialog?.Hide();
        };

        // Minus button (delete current template)
        bool canDeleteTemplate = !string.IsNullOrEmpty(currentTemplate) && templates.Count > 1;
        
        var deleteTemplateButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE738", FontSize = 12 }, // Minus
            Padding = new Thickness(8, 4, 8, 4),
            IsEnabled = canDeleteTemplate
        };
        ToolTipService.SetToolTip(deleteTemplateButton, canDeleteTemplate 
            ? "Delete current rating template" 
            : "Cannot delete the Default template");
        templateRow.Children.Add(deleteTemplateButton);

        deleteTemplateButton.Click += (s, e) =>
        {
            action.ActionType = RatingActionType.DeleteTemplate;
            action.TemplateName = _ratingService.CurrentTemplateName;
            _mainDialog?.Hide();
        };

        templateSection.Children.Add(templateRow);
        mainPanel.Children.Add(templateSection);

        // Separator
        mainPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            Margin = new Thickness(0, 2, 0, 2)
        });

        // Header with count and add button
        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var countText = new TextBlock
        {
            Text = $"Rating Types: {_ratingService.DefinitionCount}/{RatingManagementService.MaxDefinitionCount}",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        };
        headerRow.Children.Add(countText);

        // Spacer
        headerRow.Children.Add(new Border { Width = 20 });

        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 12 },
                    new TextBlock { Text = "Add", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }
                }
            },
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = !_ratingService.IsAtMaxCapacity,
            Padding = new Thickness(10, 4, 10, 4)
        };
        headerRow.Children.Add(addButton);

        addButton.Click += (s, e) =>
        {
            action.ActionType = RatingActionType.Add;
            _mainDialog?.Hide();
        };

        mainPanel.Children.Add(headerRow);

        // Rating definitions list container
        var definitionsPanel = new StackPanel { Spacing = 6 };
        
        if (_ratingService.DefinitionCount == 0)
        {
            definitionsPanel.Children.Add(new TextBlock
            {
                Text = "No rating types. Click 'Add' to create one.",
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 16, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var definition in _ratingService.Definitions)
            {
                var definitionItem = CreateDefinitionItemUI(definition, action);
                definitionsPanel.Children.Add(definitionItem);
            }
        }

        var scrollViewer = new ScrollViewer
        {
            Content = definitionsPanel,
            MaxHeight = 220,
            MinHeight = 60
        };
        mainPanel.Children.Add(scrollViewer);

        // Info text
        mainPanel.Children.Add(new TextBlock
        {
            Text = "?? Templates let you save different sets of rating types.",
            FontSize = 11,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        return (mainPanel, action);
    }

    private async Task<string?> ShowAddTemplateDialogAsync()
    {
        var nameTextBox = new TextBox
        {
            PlaceholderText = "Enter template name",
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel { Spacing = 8, MinWidth = 300 };
        panel.Children.Add(new TextBlock
        {
            Text = "Create a new rating template with a blank set of rating types.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12
        });
        panel.Children.Add(DialogHelpers.CreateLabel("Template Name: *", new Thickness(0, 8, 0, 0)));
        panel.Children.Add(nameTextBox);

        var dialog = new ContentDialog
        {
            Title = "Create New Template",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = false
        };

        nameTextBox.TextChanged += (s, e) =>
        {
            var name = nameTextBox.Text.Trim();
            var isValid = !string.IsNullOrWhiteSpace(name);
            
            // Check for duplicate template names
            if (isValid && _ratingService.TemplateExists(name))
            {
                isValid = false;
            }
            
            dialog.IsPrimaryButtonEnabled = isValid;
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return nameTextBox.Text.Trim();
        }

        return null;
    }

    private async Task<bool> ShowDeleteTemplateConfirmationAsync(string templateName)
    {
        var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
        var ratingCount = _ratingService.DefinitionCount;

        var confirmDialog = new ContentDialog
        {
            Title = "Delete Rating Template",
            Content = $"Are you sure you want to delete the template '{displayName}'?\n\n" +
                     $"This will remove {ratingCount} rating type(s) in this template.\n\n" +
                     "This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _xamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private Border CreateDefinitionItemUI(RatingDefinition definition, RatingAction action)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 4)
        };

        // Use a simple horizontal StackPanel for better control
        var rowPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // Star icon
        var iconBorder = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Gold),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon
            {
                Glyph = "\uE735", // FavoriteStar
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        rowPanel.Children.Add(iconBorder);

        // Name (takes up available space)
        var nameText = new TextBlock
        {
            Text = definition.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MinWidth = 100,
            MaxWidth = 200
        };
        rowPanel.Children.Add(nameText);

        // Score range badge
        var rangeBadge = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{definition.MinScore} to {definition.MaxScore}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DimGray)
            }
        };
        rowPanel.Children.Add(rangeBadge);

        // Spacer to push buttons to the right
        var spacer = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        rowPanel.Children.Add(spacer);

        // Edit button
        var editButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 12 }, // Edit icon
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(editButton, "Edit rating type");
        
        editButton.Click += (s, e) =>
        {
            action.ActionType = RatingActionType.Edit;
            action.Definition = definition;
            _mainDialog?.Hide();
        };
        rowPanel.Children.Add(editButton);

        // Remove button (trash icon)
        var removeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 }, // Trash/Delete icon
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(removeButton, "Remove rating type");
        
        removeButton.Click += (s, e) =>
        {
            action.ActionType = RatingActionType.Delete;
            action.Definition = definition;
            _mainDialog?.Hide();
        };
        rowPanel.Children.Add(removeButton);

        border.Child = rowPanel;
        return border;
    }

    private async Task<bool> ShowDeleteConfirmationAsync(RatingDefinition definition)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Rating Type",
            Content = $"Are you sure you want to delete the rating type '{definition.Name}'?\n\nExisting ratings using this type will become orphaned.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _xamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<RatingDefinition?> ShowRatingEditorAsync(RatingDefinition? existingDefinition)
    {
        bool isEdit = existingDefinition != null;
        
        var nameTextBox = new TextBox
        {
            Text = existingDefinition?.Name ?? string.Empty,
            PlaceholderText = "Enter rating type name (required)",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var descriptionTextBox = new TextBox
        {
            Text = existingDefinition?.Description ?? string.Empty,
            PlaceholderText = "Enter description (optional) - e.g., 'How useful is this resource?'",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // Score range inputs
        var minScoreBox = new NumberBox
        {
            Value = existingDefinition?.MinScore ?? -10,
            Minimum = -100,
            Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100
        };

        var maxScoreBox = new NumberBox
        {
            Value = existingDefinition?.MaxScore ?? 10,
            Minimum = -100,
            Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100
        };

        var rangePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var minPanel = new StackPanel { Spacing = 4 };
        minPanel.Children.Add(new TextBlock { Text = "Minimum Score:", FontSize = 12 });
        minPanel.Children.Add(minScoreBox);
        rangePanel.Children.Add(minPanel);

        var maxPanel = new StackPanel { Spacing = 4 };
        maxPanel.Children.Add(new TextBlock { Text = "Maximum Score:", FontSize = 12 });
        maxPanel.Children.Add(maxScoreBox);
        rangePanel.Children.Add(maxPanel);

        // Score range preview with proper icons
        var previewPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        previewPanel.Children.Add(new TextBlock
        {
            Text = "Score Preview:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12
        });

        var scoresPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        void UpdatePreview()
        {
            scoresPanel.Children.Clear();
            
            int min = (int)minScoreBox.Value;
            int max = (int)maxScoreBox.Value;
            
            // Show sample scores within the range
            int[] sampleScores;
            if (min >= 0)
            {
                sampleScores = new[] { min, (min + max) / 2, max };
            }
            else if (max <= 0)
            {
                sampleScores = new[] { min, (min + max) / 2, max };
            }
            else
            {
                sampleScores = new[] { min, 0, max };
            }

            foreach (var score in sampleScores)
            {
                var color = RatingManagementService.GetScoreColor(score);
                
                var badge = new Border
                {
                    Background = new SolidColorBrush(color),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = RatingManagementService.GetScoreIconGlyph(score),
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                            },
                            new TextBlock
                            {
                                Text = RatingManagementService.FormatScore(score),
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                            }
                        }
                    }
                };
                
                scoresPanel.Children.Add(badge);
            }
        }

        UpdatePreview();
        minScoreBox.ValueChanged += (s, e) => UpdatePreview();
        maxScoreBox.ValueChanged += (s, e) => UpdatePreview();

        previewPanel.Children.Add(scoresPanel);

        var panel = new StackPanel { MinWidth = 400 };
        panel.Children.Add(DialogHelpers.CreateLabel("Rating Type Name: *", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(nameTextBox);
        panel.Children.Add(DialogHelpers.CreateLabel("Description:", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(descriptionTextBox);
        panel.Children.Add(DialogHelpers.CreateLabel("Score Range:", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(rangePanel);
        panel.Children.Add(previewPanel);

        var dialog = new ContentDialog
        {
            Title = isEdit ? "Edit Rating Type" : "Add New Rating Type",
            Content = panel,
            PrimaryButtonText = isEdit ? "Save" : "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(existingDefinition?.Name)
        };

        nameTextBox.TextChanged += (s, e) =>
        {
            var name = nameTextBox.Text.Trim();
            var isValid = !string.IsNullOrWhiteSpace(name);
            
            // Check for duplicate names (excluding current definition if editing)
            if (isValid && _ratingService.DefinitionNameExists(name, existingDefinition?.Id))
            {
                isValid = false;
            }
            
            // Validate score range
            if (isValid && minScoreBox.Value >= maxScoreBox.Value)
            {
                isValid = false;
            }
            
            dialog.IsPrimaryButtonEnabled = isValid;
        };

        // Also validate on score changes
        minScoreBox.ValueChanged += (s, e) =>
        {
            var name = nameTextBox.Text.Trim();
            var isValid = !string.IsNullOrWhiteSpace(name) && 
                          !_ratingService.DefinitionNameExists(name, existingDefinition?.Id) &&
                          minScoreBox.Value < maxScoreBox.Value;
            dialog.IsPrimaryButtonEnabled = isValid;
        };

        maxScoreBox.ValueChanged += (s, e) =>
        {
            var name = nameTextBox.Text.Trim();
            var isValid = !string.IsNullOrWhiteSpace(name) && 
                          !_ratingService.DefinitionNameExists(name, existingDefinition?.Id) &&
                          minScoreBox.Value < maxScoreBox.Value;
            dialog.IsPrimaryButtonEnabled = isValid;
        };

        // Trigger initial validation
        nameTextBox.Text = nameTextBox.Text;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return new RatingDefinition
            {
                Id = existingDefinition?.Id ?? Guid.NewGuid().ToString(),
                Name = nameTextBox.Text.Trim(),
                Description = descriptionTextBox.Text.Trim(),
                MinScore = (int)minScoreBox.Value,
                MaxScore = (int)maxScoreBox.Value,
                CreatedDate = existingDefinition?.CreatedDate ?? DateTime.Now,
                ModifiedDate = DateTime.Now
            };
        }

        return null;
    }

    private enum RatingActionType
    {
        None,
        Add,
        Edit,
        Delete,
        AddTemplate,
        DeleteTemplate,
        SwitchTemplate
    }

    private class RatingAction
    {
        public RatingActionType ActionType { get; set; }
        public RatingDefinition? Definition { get; set; }
        public string? TemplateName { get; set; }
    }
}
