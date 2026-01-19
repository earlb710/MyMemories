using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories.Dialogs;

/// <summary>
/// Dialog for creating and editing saved searches with AND/OR/NOT conditions.
/// </summary>
public class SavedSearchDialog
{
    private readonly XamlRoot _xamlRoot;
    private readonly List<string> _availableCategories;
    private TextBox? _nameTextBox;
    private TextBox? _descriptionTextBox;
    private ComboBox? _iconComboBox;
    private StackPanel? _conditionGroupsPanel;
    private StackPanel? _categoriesPanel;
    private CheckBox? _allCategoriesCheckBox;
    private readonly List<ConditionGroupControl> _conditionGroups = new();
    private readonly List<CheckBox> _categoryCheckBoxes = new();
    
    // Available icons for saved searches
    private static readonly string[] IconOptions = new[]
    {
        "\U0001F50E", // ?? Right-Pointing Magnifying Glass
        "\U0001F50D", // ?? Left-Pointing Magnifying Glass
        "\U00002B50", // ? Star
        "\U0001F4C1", // ?? Folder
        "\U0001F4C4", // ?? Document
        "\U0001F3AF", // ?? Target
        "\U0001F4A1", // ?? Light Bulb
        "\U0001F516", // ?? Bookmark
        "\U0001F4CC", // ?? Pin
        "\U00002764", // ?? Heart
        "\U0001F525", // ?? Fire
        "\U00002705", // ? Check
        "\U000026A0", // ?? Warning
        "\U0001F4C8", // ?? Chart
        "\U0001F4CB"  // ?? Clipboard
    };
    
    public SavedSearchDialog(XamlRoot xamlRoot, IEnumerable<string> availableCategories)
    {
        _xamlRoot = xamlRoot;
        _availableCategories = availableCategories.ToList();
    }
    
    /// <summary>
    /// Shows dialog to create a new saved search.
    /// </summary>
    public async Task<SavedSearch?> ShowCreateDialogAsync()
    {
        return await ShowDialogAsync(null);
    }
    
    /// <summary>
    /// Shows dialog to edit an existing saved search.
    /// </summary>
    public async Task<SavedSearch?> ShowEditDialogAsync(SavedSearch existingSearch)
    {
        return await ShowDialogAsync(existingSearch);
    }
    
    private async Task<SavedSearch?> ShowDialogAsync(SavedSearch? existingSearch)
    {
        _conditionGroups.Clear();
        
        var content = CreateDialogContent(existingSearch);
        
        
        var dialog = new ContentDialog
        {
            Title = existingSearch == null ? "Create Saved Search" : "Edit Saved Search",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 600,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = existingSearch == null ? "Create" : "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };
        
        // Validation error message TextBlock
        TextBlock? validationError = null;
        
        dialog.PrimaryButtonClick += (s, e) =>
        {
            // Remove any previous validation error
            if (validationError != null && content.Children.Contains(validationError))
            {
                content.Children.Remove(validationError);
            }
            
            string? errorMessage = null;
            
            if (string.IsNullOrWhiteSpace(_nameTextBox?.Text))
            {
                errorMessage = "?? Please enter a name for the search.";
            }
            else if (_conditionGroups.All(g => g.GetConditions().Count == 0))
            {
                errorMessage = "?? Please add at least one search condition.";
            }
            
            if (errorMessage != null)
            {
                e.Cancel = true;
                
                // Show inline validation error instead of a new dialog
                validationError = new TextBlock
                {
                    Text = errorMessage,
                    Foreground = new SolidColorBrush(Colors.Red),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                content.Children.Insert(0, validationError);
            }
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            var search = existingSearch ?? new SavedSearch();
            search.Name = _nameTextBox?.Text ?? string.Empty;
            search.Description = _descriptionTextBox?.Text ?? string.Empty;
            search.Icon = _iconComboBox?.SelectedItem?.ToString() ?? "\U0001F50E";
            search.ConditionGroups = _conditionGroups
                .Select(g => g.GetConditionGroup())
                .Where(g => g.Conditions.Count > 0)
                .ToList();
            
            // Get selected categories
            if (_allCategoriesCheckBox?.IsChecked == true)
            {
                search.IncludedCategories = new List<string>();
            }
            else
            {
                search.IncludedCategories = _categoryCheckBoxes
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Content?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            
            search.ModifiedDate = DateTime.Now;
            
            return search;
        }
        
        return null;
    }
    
    private StackPanel CreateDialogContent(SavedSearch? existingSearch)
    {
        var mainPanel = new StackPanel { Spacing = 16, MinWidth = 800 };
        
        // Basic info section
        var basicInfoPanel = CreateBasicInfoSection(existingSearch);
        mainPanel.Children.Add(basicInfoPanel);
        
        // Categories section
        var categoriesPanel = CreateCategoriesSection(existingSearch);
        mainPanel.Children.Add(categoriesPanel);
        
        // Conditions section
        var conditionsHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 4)
        };
        
        conditionsHeader.Children.Add(new TextBlock
        {
            Text = "Search Conditions",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        
        var addGroupButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 12 },
                    new TextBlock { Text = "Add Group", FontSize = 12 }
                }
            },
            Padding = new Thickness(8, 4, 8, 4)
        };
        addGroupButton.Click += (s, e) => AddConditionGroup(null);
        conditionsHeader.Children.Add(addGroupButton);
        
        mainPanel.Children.Add(conditionsHeader);
        
        // Help text
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Use AND/OR between conditions. Groups can be combined with AND/OR. Search is case-insensitive.",
            FontSize = 11,
            Foreground = new SolidColorBrush(Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        // Condition groups container
        _conditionGroupsPanel = new StackPanel { Spacing = 12 };
        mainPanel.Children.Add(_conditionGroupsPanel);
        
        // Add existing condition groups or a default one
        if (existingSearch?.ConditionGroups.Count > 0)
        {
            foreach (var group in existingSearch.ConditionGroups)
            {
                AddConditionGroup(group);
            }
        }
        else
        {
            AddConditionGroup(null);
        }
        
        return mainPanel;
    }
    
    private Border CreateBasicInfoSection(SavedSearch? existingSearch)
    {
        var panel = new StackPanel { Spacing = 8 };
        
        // Icon and Name in a row (Icon first)
        var nameRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(100) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 8
        };
        
        var iconStack = new StackPanel { Spacing = 4 };
        iconStack.Children.Add(new TextBlock { Text = "Icon", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _iconComboBox = new ComboBox
        {
            ItemsSource = IconOptions,
            SelectedItem = existingSearch?.Icon ?? IconOptions[0],
            MinWidth = 90,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        // If existing icon not in list, select first one
        if (_iconComboBox.SelectedItem == null)
        {
            _iconComboBox.SelectedIndex = 0;
        }
        iconStack.Children.Add(_iconComboBox);
        Grid.SetColumn(iconStack, 0);
        nameRow.Children.Add(iconStack);
        
        var nameStack = new StackPanel { Spacing = 4 };
        nameStack.Children.Add(new TextBlock { Text = "Search Name *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _nameTextBox = new TextBox
        {
            PlaceholderText = "e.g., Important Documents",
            Text = existingSearch?.Name ?? string.Empty
        };
        nameStack.Children.Add(_nameTextBox);
        Grid.SetColumn(nameStack, 1);
        nameRow.Children.Add(nameStack);
        
        panel.Children.Add(nameRow);
        
        // Description (multiline)
        panel.Children.Add(new TextBlock 
        { 
            Text = "Description", 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });
        _descriptionTextBox = new TextBox
        {
            PlaceholderText = "Optional description of what this search finds",
            Text = existingSearch?.Description ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60,
            MaxHeight = 100
        };
        panel.Children.Add(_descriptionTextBox);
        
        return new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = panel
        };
    }
    
    private Border CreateCategoriesSection(SavedSearch? existingSearch)
    {
        var panel = new StackPanel { Spacing = 8 };
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Include Categories", 
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
        });
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "Select which categories to search. Leave 'All Categories' checked to search everywhere.",
            FontSize = 11,
            Foreground = new SolidColorBrush(Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });
        
        // "All Categories" checkbox
        bool searchAllCategories = existingSearch == null || existingSearch.IncludedCategories.Count == 0;
        _allCategoriesCheckBox = new CheckBox
        {
            Content = "All Categories",
            IsChecked = searchAllCategories,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _allCategoriesCheckBox.Checked += (s, e) => UpdateCategoryCheckBoxesEnabled(false);
        _allCategoriesCheckBox.Unchecked += (s, e) => UpdateCategoryCheckBoxesEnabled(true);
        panel.Children.Add(_allCategoriesCheckBox);
        
        // Individual category checkboxes in a scrollable panel
        _categoriesPanel = new StackPanel { Spacing = 2, Margin = new Thickness(20, 4, 0, 0) };
        
        foreach (var categoryName in _availableCategories)
        {
            var checkBox = new CheckBox
            {
                Content = categoryName,
                IsChecked = existingSearch?.IncludedCategories.Contains(categoryName) ?? false,
                IsEnabled = !searchAllCategories
            };
            _categoryCheckBoxes.Add(checkBox);
            _categoriesPanel.Children.Add(checkBox);
        }
        
        var categoriesScroll = new ScrollViewer
        {
            Content = _categoriesPanel,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        panel.Children.Add(categoriesScroll);
        
        return new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = panel
        };
    }
    
    private void UpdateCategoryCheckBoxesEnabled(bool enabled)
    {
        foreach (var checkBox in _categoryCheckBoxes)
        {
            checkBox.IsEnabled = enabled;
            if (!enabled)
            {
                checkBox.IsChecked = false;
            }
        }
    }
    
    private void AddConditionGroup(SearchConditionGroup? existingGroup)
    {
        var groupControl = new ConditionGroupControl(
            _conditionGroups.Count > 0,
            existingGroup,
            OnRemoveGroup);
        
        _conditionGroups.Add(groupControl);
        _conditionGroupsPanel?.Children.Add(groupControl.Container);
    }
    
    private void OnRemoveGroup(ConditionGroupControl group)
    {
        if (_conditionGroups.Count <= 1)
            return;
        
        _conditionGroups.Remove(group);
        _conditionGroupsPanel?.Children.Remove(group.Container);
        
        // Update first group to not show operator
        if (_conditionGroups.Count > 0)
        {
            _conditionGroups[0].UpdateOperatorVisibility(false);
        }
    }
    
    private async Task ShowValidationErrorAsync(string message)
    {
        var errorDialog = new ContentDialog
        {
            Title = "Validation Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = _xamlRoot
        };
        await errorDialog.ShowAsync();
    }
}

/// <summary>
/// Control for a single condition group.
/// </summary>
internal class ConditionGroupControl
{
    public Border Container { get; }
    
    private readonly StackPanel _conditionsPanel;
    private readonly ComboBox _groupOperatorCombo;
    private readonly CheckBox _negateCheckBox;
    private readonly List<ConditionRowControl> _conditions = new();
    private readonly Action<ConditionGroupControl> _onRemove;
    
    public ConditionGroupControl(bool showOperator, SearchConditionGroup? existingGroup, Action<ConditionGroupControl> onRemove)
    {
        _onRemove = onRemove;
        
        var mainPanel = new StackPanel { Spacing = 8 };
        
        // Header with operator and actions
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };
        
        // Group operator (AND/OR)
        _groupOperatorCombo = new ComboBox
        {
            ItemsSource = new[] { "AND", "OR" },
            SelectedIndex = existingGroup?.GroupOperator == LogicalOperator.Or ? 1 : 0,
            Visibility = showOperator ? Visibility.Visible : Visibility.Collapsed,
            MinWidth = 80
        };
        Grid.SetColumn(_groupOperatorCombo, 0);
        header.Children.Add(_groupOperatorCombo);
        
        // Group label
        var label = new TextBlock
        {
            Text = "Condition Group",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        header.Children.Add(label);
        
        // Negate checkbox
        _negateCheckBox = new CheckBox
        {
            Content = "NOT",
            IsChecked = existingGroup?.IsNegated ?? false
        };
        ToolTipService.SetToolTip(_negateCheckBox, "Negate this entire group (match items that do NOT match these conditions)");
        Grid.SetColumn(_negateCheckBox, 2);
        header.Children.Add(_negateCheckBox);
        
        // Remove group button
        var removeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 },
            Padding = new Thickness(6)
        };
        ToolTipService.SetToolTip(removeButton, "Remove this condition group");
        removeButton.Click += (s, e) => _onRemove(this);
        Grid.SetColumn(removeButton, 3);
        header.Children.Add(removeButton);
        
        mainPanel.Children.Add(header);
        
        // Conditions container
        _conditionsPanel = new StackPanel { Spacing = 4 };
        mainPanel.Children.Add(_conditionsPanel);
        
        // Add condition button
        var addConditionButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new FontIcon { Glyph = "\uE710", FontSize = 10 },
                    new TextBlock { Text = "Add Condition", FontSize = 11 }
                }
            },
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 4, 0, 0)
        };
        addConditionButton.Click += (s, e) => AddCondition(null);
        mainPanel.Children.Add(addConditionButton);
        
        // Add existing conditions or default
        if (existingGroup?.Conditions.Count > 0)
        {
            foreach (var condition in existingGroup.Conditions)
            {
                AddCondition(condition);
            }
        }
        else
        {
            AddCondition(null);
        }
        
        Container = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 0, 120, 215)),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = mainPanel
        };
    }
    
    public void UpdateOperatorVisibility(bool visible)
    {
        _groupOperatorCombo.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void AddCondition(SearchCondition? existing)
    {
        var conditionRow = new ConditionRowControl(
            _conditions.Count > 0,
            existing,
            c => RemoveCondition(c));
        
        _conditions.Add(conditionRow);
        _conditionsPanel.Children.Add(conditionRow.Container);
    }
    
    private void RemoveCondition(ConditionRowControl condition)
    {
        if (_conditions.Count <= 1)
            return;
        
        _conditions.Remove(condition);
        _conditionsPanel.Children.Remove(condition.Container);
        
        if (_conditions.Count > 0)
        {
            _conditions[0].UpdateOperatorVisibility(false);
        }
    }
    
    public SearchConditionGroup GetConditionGroup()
    {
        return new SearchConditionGroup
        {
            GroupOperator = _groupOperatorCombo.SelectedIndex == 1 ? LogicalOperator.Or : LogicalOperator.And,
            IsNegated = _negateCheckBox.IsChecked ?? false,
            Conditions = _conditions.Select(c => c.GetCondition()).ToList()
        };
    }
    
    public List<SearchCondition> GetConditions()
    {
        return _conditions.Select(c => c.GetCondition()).ToList();
    }
}

/// <summary>
/// Control for a single search condition row.
/// </summary>
internal class ConditionRowControl
{
    public Grid Container { get; }
    
    private readonly ComboBox _operatorCombo;
    private readonly ComboBox _fieldCombo;
    private readonly ComboBox _comparisonCombo;
    private readonly TextBox _valueTextBox;
    private readonly TextBox _secondaryValueTextBox;
    private readonly CheckBox _negateCheckBox;
    
    // Field display names with type prefixes
    private static readonly (string Display, SearchField Field)[] FieldOptions = new[]
    {
        ("Content: Any", SearchField.Any),
        ("Name: Title/Name", SearchField.Name),
        ("Content: Description", SearchField.Description),
        ("Content: Keywords", SearchField.Keywords),
        ("Content: URL", SearchField.Url),
        ("Tag: Tag Name", SearchField.Tag),
        ("Rating: Rating Name", SearchField.Rating),
        ("Rating: Score", SearchField.RatingScore),
        ("Date: Created", SearchField.DateCreated),
        ("Date: Modified", SearchField.DateModified),
        ("Type: Item Type", SearchField.ItemType),
        ("Type: File Extension", SearchField.FileExtension),
        ("Name: Category Path", SearchField.CategoryPath)
    };
    
    public ConditionRowControl(bool showOperator, SearchCondition? existing, Action<ConditionRowControl> onRemove)
    {
        Container = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(80) },  // Operator (wider for AND/OR)
                new ColumnDefinition { Width = new GridLength(150) }, // Field (wider for type prefix)
                new ColumnDefinition { Width = new GridLength(110) }, // Comparison
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Value
                new ColumnDefinition { Width = GridLength.Auto },      // NOT
                new ColumnDefinition { Width = GridLength.Auto }       // Remove
            },
            ColumnSpacing = 4
        };
        
        // Condition operator (AND/OR)
        _operatorCombo = new ComboBox
        {
            ItemsSource = new[] { "AND", "OR" },
            SelectedIndex = existing?.ConditionOperator == LogicalOperator.Or ? 1 : 0,
            Visibility = showOperator ? Visibility.Visible : Visibility.Collapsed,
            MinWidth = 75,
            Width = 75,
            FontSize = 11
        };
        Grid.SetColumn(_operatorCombo, 0);
        Container.Children.Add(_operatorCombo);
        
        // Field selector with type prefixes
        _fieldCombo = new ComboBox
        {
            ItemsSource = FieldOptions.Select(f => f.Display).ToArray(),
            SelectedIndex = GetFieldIndex(existing?.Field ?? SearchField.Any),
            MinWidth = 145,
            FontSize = 11
        };
        Grid.SetColumn(_fieldCombo, 1);
        Container.Children.Add(_fieldCombo);
        
        // Comparison operator
        _comparisonCombo = new ComboBox
        {
            ItemsSource = new[]
            {
                "Contains", "Not Contains", "Equals", "Not Equals",
                "Starts With", "Ends With", "Regex",
                "Greater Than", "Less Than", "Between",
                "Has Value", "Is Empty"
            },
            SelectedIndex = GetComparisonIndex(existing?.Operator ?? SearchOperator.Contains),
            MinWidth = 95,
            FontSize = 11
        };
        _comparisonCombo.SelectionChanged += (s, e) => UpdateValueVisibility();
        Grid.SetColumn(_comparisonCombo, 2);
        Container.Children.Add(_comparisonCombo);
        
        // Value inputs in a stack
        var valueStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        
        _valueTextBox = new TextBox
        {
            PlaceholderText = "Value",
            Text = existing?.Value ?? string.Empty,
            MinWidth = 100,
            FontSize = 11
        };
        valueStack.Children.Add(_valueTextBox);
        
        _secondaryValueTextBox = new TextBox
        {
            PlaceholderText = "To",
            Text = existing?.SecondaryValue ?? string.Empty,
            MinWidth = 60,
            FontSize = 11,
            Visibility = Visibility.Collapsed
        };
        valueStack.Children.Add(_secondaryValueTextBox);
        
        Grid.SetColumn(valueStack, 3);
        Container.Children.Add(valueStack);
        
        // Negate checkbox
        _negateCheckBox = new CheckBox
        {
            Content = "NOT",
            IsChecked = existing?.IsNegated ?? false,
            FontSize = 10
        };
        Grid.SetColumn(_negateCheckBox, 4);
        Container.Children.Add(_negateCheckBox);
        
        // Remove button
        var removeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
            Padding = new Thickness(4)
        };
        removeButton.Click += (s, e) => onRemove(this);
        Grid.SetColumn(removeButton, 5);
        Container.Children.Add(removeButton);
        
        UpdateValueVisibility();
    }
    
    public void UpdateOperatorVisibility(bool visible)
    {
        _operatorCombo.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void UpdateValueVisibility()
    {
        var selectedOp = _comparisonCombo.SelectedIndex;
        
        // Has Value (10) and Is Empty (11) don't need a value
        var needsValue = selectedOp < 10;
        _valueTextBox.Visibility = needsValue ? Visibility.Visible : Visibility.Collapsed;
        
        // Between (9) needs secondary value
        _secondaryValueTextBox.Visibility = selectedOp == 9 ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private static int GetComparisonIndex(SearchOperator op)
    {
        return op switch
        {
            SearchOperator.Contains => 0,
            SearchOperator.NotContains => 1,
            SearchOperator.Equals => 2,
            SearchOperator.NotEquals => 3,
            SearchOperator.StartsWith => 4,
            SearchOperator.EndsWith => 5,
            SearchOperator.MatchesRegex => 6,
            SearchOperator.GreaterThan => 7,
            SearchOperator.LessThan => 8,
            SearchOperator.Between => 9,
            SearchOperator.HasValue => 10,
            SearchOperator.IsEmpty => 11,
            _ => 0
        };
    }
    
    private SearchOperator GetSearchOperator()
    {
        return _comparisonCombo.SelectedIndex switch
        {
            0 => SearchOperator.Contains,
            1 => SearchOperator.NotContains,
            2 => SearchOperator.Equals,
            3 => SearchOperator.NotEquals,
            4 => SearchOperator.StartsWith,
            5 => SearchOperator.EndsWith,
            6 => SearchOperator.MatchesRegex,
            7 => SearchOperator.GreaterThan,
            8 => SearchOperator.LessThan,
            9 => SearchOperator.Between,
            10 => SearchOperator.HasValue,
            11 => SearchOperator.IsEmpty,
            _ => SearchOperator.Contains
        };
    }
    
    public SearchCondition GetCondition()
    {
        var selectedIndex = _fieldCombo.SelectedIndex;
        var field = selectedIndex >= 0 && selectedIndex < FieldOptions.Length 
            ? FieldOptions[selectedIndex].Field 
            : SearchField.Any;
        
        return new SearchCondition
        {
            Field = field,
            Operator = GetSearchOperator(),
            Value = _valueTextBox.Text ?? string.Empty,
            SecondaryValue = _secondaryValueTextBox.Text,
            ConditionOperator = _operatorCombo.SelectedIndex == 1 ? LogicalOperator.Or : LogicalOperator.And,
            IsNegated = _negateCheckBox.IsChecked ?? false
        };
    }
    
    private static int GetFieldIndex(SearchField field)
    {
        for (int i = 0; i < FieldOptions.Length; i++)
        {
            if (FieldOptions[i].Field == field)
                return i;
        }
        return 0;
    }
}
