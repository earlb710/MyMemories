using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for managing tags (create, edit, delete).
/// </summary>
public class TagManagementDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly TagManagementService _tagService;
    private ContentDialog? _mainDialog;
    
    // Predefined color palette
    private static readonly List<string> ColorPalette = new()
    {
        "#0078D4", // Blue
        "#107C10", // Green
        "#D83B01", // Orange
        "#E81123", // Red
        "#5C2D91", // Purple
        "#008575", // Teal
        "#FF8C00", // Dark Orange
        "#00BCF2", // Light Blue
        "#8E8CD8", // Lavender
        "#009E49", // Bright Green
        "#7A7574", // Gray
        "#B4009E", // Magenta
        "#002050", // Dark Blue
        "#4A5459", // Slate
        "#EAA300", // Gold
        "#498205"  // Forest Green
    };

    public TagManagementDialog(XamlRoot xamlRoot, TagManagementService tagService)
    {
        _xamlRoot = xamlRoot;
        _tagService = tagService;
    }

    /// <summary>
    /// Shows the tag management dialog with refreshed content.
    /// </summary>
    public async Task RefreshAndShowDialogAsync()
    {
        while (true)
        {
            var (panel, action) = BuildMainPanel();

            _mainDialog = new ContentDialog
            {
                Title = "??? Tag Management",
                Content = panel,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot
            };

            var result = await _mainDialog.ShowAsync();
            _mainDialog = null;

            // Handle actions that require reopening the dialog
            if (action.ActionType == TagActionType.None)
            {
                break; // User clicked Close
            }
            else if (action.ActionType == TagActionType.Add)
            {
                var newTag = await ShowTagEditorAsync(null);
                if (newTag != null)
                {
                    _tagService.AddTag(newTag);
                    await _tagService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == TagActionType.Edit && action.Tag != null)
            {
                var editedTag = await ShowTagEditorAsync(action.Tag);
                if (editedTag != null)
                {
                    _tagService.UpdateTag(editedTag);
                    await _tagService.SaveAsync();
                }
                // Loop to show dialog again
            }
            else if (action.ActionType == TagActionType.Delete && action.Tag != null)
            {
                var confirmed = await ShowDeleteConfirmationAsync(action.Tag);
                if (confirmed)
                {
                    _tagService.RemoveTag(action.Tag.Id);
                    await _tagService.SaveAsync();
                }
                // Loop to show dialog again
            }
        }
    }

    private (StackPanel Panel, TagAction Action) BuildMainPanel()
    {
        var action = new TagAction { ActionType = TagActionType.None };
        
        var mainPanel = new StackPanel { Spacing = 12, MinWidth = 500 };

        // Header with count and add button
        var headerPanel = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var countText = new TextBlock
        {
            Text = $"Tags: {_tagService.TagCount} / {TagManagementService.MaxTagCount}",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        Grid.SetColumn(countText, 0);
        headerPanel.Children.Add(countText);

        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 14 },
                    new TextBlock { Text = "Add Tag", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = !_tagService.IsAtMaxCapacity
        };
        Grid.SetColumn(addButton, 1);
        headerPanel.Children.Add(addButton);

        addButton.Click += (s, e) =>
        {
            action.ActionType = TagActionType.Add;
            _mainDialog?.Hide();
        };

        mainPanel.Children.Add(headerPanel);

        // Tags list container
        var tagsPanel = new StackPanel { Spacing = 8 };
        
        if (_tagService.TagCount == 0)
        {
            tagsPanel.Children.Add(new TextBlock
            {
                Text = "No tags created yet. Click 'Add Tag' to create your first tag.",
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 20, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else
        {
            foreach (var tag in _tagService.Tags)
            {
                var tagItem = CreateTagItemUI(tag, action);
                tagsPanel.Children.Add(tagItem);
            }
        }

        var scrollViewer = new ScrollViewer
        {
            Content = tagsPanel,
            MaxHeight = 300,
            MinHeight = 100
        };
        mainPanel.Children.Add(scrollViewer);

        // Info text
        mainPanel.Children.Add(new TextBlock
        {
            Text = "?? Tags can be used to organize and filter your links and categories.",
            FontSize = 12,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        });

        return (mainPanel, action);
    }

    private Border CreateTagItemUI(TagItem tag, TagAction action)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Color
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name/Description
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

        // Color indicator
        var colorBorder = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(ParseColor(tag.Color)),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(colorBorder, 0);
        grid.Children.Add(colorBorder);

        // Name and description
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = tag.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14
        });
        
        if (!string.IsNullOrWhiteSpace(tag.Description))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = tag.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300
            });
        }
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // Action buttons
        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var editButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 14 },
            Padding = new Thickness(8)
        };
        ToolTipService.SetToolTip(editButton, "Edit tag");
        
        editButton.Click += (s, e) =>
        {
            action.ActionType = TagActionType.Edit;
            action.Tag = tag;
            _mainDialog?.Hide();
        };
        actionsPanel.Children.Add(editButton);

        var deleteButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 },
            Padding = new Thickness(8)
        };
        ToolTipService.SetToolTip(deleteButton, "Delete tag");
        
        deleteButton.Click += (s, e) =>
        {
            action.ActionType = TagActionType.Delete;
            action.Tag = tag;
            _mainDialog?.Hide();
        };
        actionsPanel.Children.Add(deleteButton);

        Grid.SetColumn(actionsPanel, 2);
        grid.Children.Add(actionsPanel);

        border.Child = grid;
        return border;
    }

    private async Task<bool> ShowDeleteConfirmationAsync(TagItem tag)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Delete Tag",
            Content = $"Are you sure you want to delete the tag '{tag.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _xamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<TagItem?> ShowTagEditorAsync(TagItem? existingTag)
    {
        bool isEdit = existingTag != null;
        
        var nameTextBox = new TextBox
        {
            Text = existingTag?.Name ?? string.Empty,
            PlaceholderText = "Enter tag name (required)",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var descriptionTextBox = new TextBox
        {
            Text = existingTag?.Description ?? string.Empty,
            PlaceholderText = "Enter description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Color picker
        var colorGrid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Margin = new Thickness(0, 0, 0, 8)
        };

        int selectedColorIndex = 0;
        var currentColor = existingTag?.Color ?? ColorPalette[0];
        
        for (int i = 0; i < ColorPalette.Count; i++)
        {
            var color = ColorPalette[i];
            var colorButton = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ParseColor(color)),
                Margin = new Thickness(2),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(2)
            };
            colorGrid.Items.Add(colorButton);

            if (color.Equals(currentColor, StringComparison.OrdinalIgnoreCase))
            {
                selectedColorIndex = i;
            }
        }
        colorGrid.SelectedIndex = selectedColorIndex;

        var panel = new StackPanel { MinWidth = 400 };
        panel.Children.Add(DialogHelpers.CreateLabel("Tag Name: *", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(nameTextBox);
        panel.Children.Add(DialogHelpers.CreateLabel("Description:", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(descriptionTextBox);
        panel.Children.Add(DialogHelpers.CreateLabel("Color:", new Thickness(0, 0, 0, 4)));
        panel.Children.Add(new ScrollViewer
        {
            Content = colorGrid,
            MaxHeight = 120
        });

        var dialog = new ContentDialog
        {
            Title = isEdit ? "Edit Tag" : "Add New Tag",
            Content = panel,
            PrimaryButtonText = isEdit ? "Save" : "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(existingTag?.Name)
        };

        nameTextBox.TextChanged += (s, e) =>
        {
            var name = nameTextBox.Text.Trim();
            var isValid = !string.IsNullOrWhiteSpace(name);
            
            // Check for duplicate names (excluding current tag if editing)
            if (isValid && _tagService.TagNameExists(name, existingTag?.Id))
            {
                isValid = false;
            }
            
            dialog.IsPrimaryButtonEnabled = isValid;
        };

        // Trigger initial validation
        nameTextBox.Text = nameTextBox.Text;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var selectedColor = colorGrid.SelectedIndex >= 0 && colorGrid.SelectedIndex < ColorPalette.Count
                ? ColorPalette[colorGrid.SelectedIndex]
                : ColorPalette[0];

            return new TagItem
            {
                Id = existingTag?.Id ?? Guid.NewGuid().ToString(),
                Name = nameTextBox.Text.Trim(),
                Description = descriptionTextBox.Text.Trim(),
                Color = selectedColor,
                CreatedDate = existingTag?.CreatedDate ?? DateTime.Now,
                ModifiedDate = DateTime.Now
            };
        }

        return null;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return Color.FromArgb(
                    255,
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
        }
        catch { }
        
        return Microsoft.UI.Colors.Blue;
    }

    private enum TagActionType
    {
        None,
        Add,
        Edit,
        Delete
    }

    private class TagAction
    {
        public TagActionType ActionType { get; set; }
        public TagItem? Tag { get; set; }
    }
}
