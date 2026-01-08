using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Text;

namespace MyMemories.Dialogs;

/// <summary>
/// Builder for category-related dialogs.
/// </summary>
public class CategoryDialogBuilder
{
    private readonly XamlRoot _xamlRoot;
    private readonly ConfigurationService? _configService;
    
    private static readonly List<string> CategoryIcons = new()
    {
        "📁", "📂", "📚", "📖", "📝", "📄", "📋", "📌",
        "🗂️", "🗃️", "🗄️", "📦", "🎯", "⭐", "💼", "🏠",
        "🎨", "🎭", "🎪", "🎬", "🎮", "🎵", "🎸", "📷",
        "🖼️", "🌍", "🌐", "🔧", "🔨", "⚙️", "🔗", "📊",
        "📈", "📉", "💻", "⌨️", "🖥️", "📱", "☁️", "💾",
        "🔒", "🔓", "🔑", "🏆", "🎓", "📚", "✏️", "📐",
        // Bookmark icons
        "🔖", "📑", "🏷️", "📎", "💌", "📧", "📨", "📬"
    };

    public CategoryDialogBuilder(XamlRoot xamlRoot, ConfigurationService? configService = null)
    {
        _xamlRoot = xamlRoot;
        _configService = configService;
    }

    /// <summary>
    /// Shows the add/edit category dialog with icon picker and password options.
    /// </summary>
    public async Task<CategoryEditResult?> ShowCategoryDialogAsync(
        string title, 
        string? currentName = null, 
        string? currentDescription = null, 
        string? currentIcon = null,
        bool isRootCategory = true,
        PasswordProtectionType currentPasswordProtection = PasswordProtectionType.None,
        string? currentPasswordHash = null,
        bool currentIsBookmarkCategory = false,
        bool currentIsBookmarkLookup = false)
    {
        var (stackPanel, controls) = BuildCategoryDialogUI(
            currentName, 
            currentDescription, 
            currentIcon, 
            isRootCategory,
            currentPasswordProtection,
            currentPasswordHash,
            currentIsBookmarkCategory,
            currentIsBookmarkLookup);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = currentName == null ? "Create" : "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(currentName)
        };

        controls.NameTextBox.TextChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(controls.NameTextBox.Text);
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return await CreateCategoryResultAsync(controls, currentPasswordHash);
        }

        return null;
    }

    /// <summary>
    /// Shows the move link dialog to select a new category.
    /// </summary>
    public async Task<MoveLinkResult?> ShowMoveLinkAsync(
        IEnumerable<CategoryNode> allCategories, 
        TreeViewNode currentCategoryNode, 
        string linkTitle)
    {
        var categoryComboBox = new ComboBox
        {
            PlaceholderText = "Select target category (required)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Populate categories, excluding the current category
        foreach (var category in allCategories)
        {
            if (category.Node != currentCategoryNode)
            {
                categoryComboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = category.Name, 
                    Tag = category.Node
                });
            }
        }

        if (categoryComboBox.Items.Count == 0)
        {
            await DialogHelpers.ShowErrorAsync(_xamlRoot, 
                "No Categories Available",
                "There are no other categories to move this link to. Please create another category first.");
            return null;
        }

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Move '{linkTitle}' to:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Target Category: *", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(categoryComboBox);

        var dialog = new ContentDialog
        {
            Title = "Move Link",
            Content = stackPanel,
            PrimaryButtonText = "Move",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = false
        };

        categoryComboBox.SelectionChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = categoryComboBox.SelectedIndex >= 0;
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && categoryComboBox.SelectedIndex >= 0)
        {
            var selectedItem = categoryComboBox.SelectedItem as ComboBoxItem;
            var targetCategory = selectedItem?.Tag as TreeViewNode;

            if (targetCategory != null)
            {
                return new MoveLinkResult { TargetCategoryNode = targetCategory };
            }
        }

        return null;
    }

    private (StackPanel, CategoryDialogControls) BuildCategoryDialogUI(
        string? currentName, 
        string? currentDescription, 
        string? currentIcon,
        bool isRootCategory,
        PasswordProtectionType currentPasswordProtection,
        string? currentPasswordHash,
        bool currentIsBookmarkCategory,
        bool currentIsBookmarkLookup)
    {
        var categoryNameTextBox = new TextBox
        {
            Text = currentName ?? string.Empty,
            PlaceholderText = "Enter category name (required)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var categoryDescriptionTextBox = new TextBox
        {
            Text = currentDescription ?? string.Empty,
            PlaceholderText = "Enter category description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // URL Bookmarks checkbox - show for all categories, but disable for inherited
        CheckBox? isBookmarkCategoryCheckBox = null;
        CheckBox? isBookmarkLookupCheckBox = null;
        TextBlock? inheritedNote = null;
        
        // Always create checkbox (for root and subcategories)
        isBookmarkCategoryCheckBox = new CheckBox
        {
            Content = "\U0001F516 URL Bookmarks Only (restrict to web links)", // 🔖
            IsChecked = currentIsBookmarkCategory,
            IsEnabled = isRootCategory || !currentIsBookmarkCategory, // Enabled for root or non-inherited subcategories
            Margin = new Thickness(0, 8, 0, 8)
        };
        
        // Bookmark Lookup checkbox (shown only when bookmark category is checked)
        isBookmarkLookupCheckBox = new CheckBox
        {
            Content = "\U0001F50D Use for bookmark lookup", // 🔍
            IsChecked = currentIsBookmarkLookup,
            IsEnabled = currentIsBookmarkCategory,
            Visibility = currentIsBookmarkCategory ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(20, 0, 0, 8) // Indented to show relationship
        };
        
        // Wire up the bookmark category checkbox to show/hide lookup checkbox
        isBookmarkCategoryCheckBox.Checked += (s, e) =>
        {
            isBookmarkLookupCheckBox.IsEnabled = true;
            isBookmarkLookupCheckBox.Visibility = Visibility.Visible;
        };
        
        isBookmarkCategoryCheckBox.Unchecked += (s, e) =>
        {
            isBookmarkLookupCheckBox.IsEnabled = false;
            isBookmarkLookupCheckBox.Visibility = Visibility.Collapsed;
            isBookmarkLookupCheckBox.IsChecked = false;
        };
        
        // Add helper text if it's inherited from bookmark parent
        if (!isRootCategory && currentIsBookmarkCategory)
        {
            inheritedNote = new TextBlock
            {
                Text = "   (Inherited from parent category)",
                FontSize = 11,
                FontStyle = FontStyle.Italic,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, -4, 0, 8)
            };
        }

        var iconGridView = BuildIconPicker(currentIcon);

        // Password controls (only for root categories)
        ComboBox? passwordProtectionComboBox = null;
        PasswordBox? ownPasswordBox = null;
        PasswordBox? confirmPasswordBox = null;
        TextBlock? passwordLabel = null;
        TextBlock? confirmPasswordLabel = null;

        if (isRootCategory)
        {
            // Debug output
            System.Diagnostics.Debug.WriteLine($"BuildCategoryDialogUI: isRootCategory={isRootCategory}, creating password controls");
            
            passwordProtectionComboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12),
                MinWidth = 200
            };

            passwordProtectionComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = "No Password", 
                Tag = PasswordProtectionType.None 
            });
            passwordProtectionComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = "Global Password", 
                Tag = PasswordProtectionType.GlobalPassword 
            });
            passwordProtectionComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = "Own Password", 
                Tag = PasswordProtectionType.OwnPassword 
            });

            passwordProtectionComboBox.SelectedIndex = (int)currentPasswordProtection;

            passwordLabel = new TextBlock
            {
                Text = "Password:",
                Margin = new Thickness(0, 8, 0, 4),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Visibility = currentPasswordProtection == PasswordProtectionType.OwnPassword 
                    ? Visibility.Visible 
                    : Visibility.Collapsed
            };

            ownPasswordBox = new PasswordBox
            {
                PlaceholderText = currentPasswordHash != null 
                    ? "Leave empty to keep current password" 
                    : "Enter password",
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = currentPasswordProtection == PasswordProtectionType.OwnPassword 
                    ? Visibility.Visible 
                    : Visibility.Collapsed
            };

            confirmPasswordLabel = new TextBlock
            {
                Text = "Confirm Password:",
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Visibility = currentPasswordProtection == PasswordProtectionType.OwnPassword 
                    ? Visibility.Visible 
                    : Visibility.Collapsed
            };

            confirmPasswordBox = new PasswordBox
            {
                PlaceholderText = currentPasswordHash != null 
                    ? "Leave empty to keep current password" 
                    : "Confirm password",
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = currentPasswordProtection == PasswordProtectionType.OwnPassword 
                    ? Visibility.Visible 
                    : Visibility.Collapsed
            };

            // Handle password protection type changes
            passwordProtectionComboBox.SelectionChanged += (s, args) =>
            {
                var selectedItem = passwordProtectionComboBox.SelectedItem as ComboBoxItem;
                var protectionType = selectedItem?.Tag as PasswordProtectionType? ?? PasswordProtectionType.None;

                var isOwnPassword = protectionType == PasswordProtectionType.OwnPassword;
                if (passwordLabel != null) passwordLabel.Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed;
                if (ownPasswordBox != null) ownPasswordBox.Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed;
                if (confirmPasswordLabel != null) confirmPasswordLabel.Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed;
                if (confirmPasswordBox != null) confirmPasswordBox.Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"BuildCategoryDialogUI: isRootCategory={isRootCategory}, NOT creating password controls");
        }

        var stackPanel = new StackPanel();
        
        // Category Name
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category Name: *", 
            new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(categoryNameTextBox);
        
        // Description
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(categoryDescriptionTextBox);

        // URL Bookmarks checkbox (always show for all categories)
        if (isBookmarkCategoryCheckBox != null)
        {
            stackPanel.Children.Add(isBookmarkCategoryCheckBox);
            
            // Add helper text if it's inherited
            if (inheritedNote != null)
            {
                stackPanel.Children.Add(inheritedNote);
            }
            
            // Add bookmark lookup checkbox (shown conditionally)
            if (isBookmarkLookupCheckBox != null)
            {
                stackPanel.Children.Add(isBookmarkLookupCheckBox);
            }
        }

        // Password Protection (only for root categories) - BEFORE ICON PICKER
        if (isRootCategory && passwordProtectionComboBox != null)
        {
            System.Diagnostics.Debug.WriteLine("Adding password controls to stackPanel");
            
            var passwordSectionLabel = DialogHelpers.CreateLabel("Password Protection:", 
                new Thickness(0, 16, 0, 4));
            stackPanel.Children.Add(passwordSectionLabel);
            stackPanel.Children.Add(passwordProtectionComboBox);

            if (passwordLabel != null) stackPanel.Children.Add(passwordLabel);
            if (ownPasswordBox != null) stackPanel.Children.Add(ownPasswordBox);
            if (confirmPasswordLabel != null) stackPanel.Children.Add(confirmPasswordLabel);
            if (confirmPasswordBox != null) stackPanel.Children.Add(confirmPasswordBox);
            
            System.Diagnostics.Debug.WriteLine($"Password controls added. StackPanel child count: {stackPanel.Children.Count}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"NOT adding password controls. isRootCategory={isRootCategory}, comboBox null={passwordProtectionComboBox == null}");
        }
        
        // Icon Picker - AT THE END
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category Icon:", 
            new Thickness(0, 16, 0, 4)));
        
        var iconScrollViewer = new ScrollViewer
        {
            MaxHeight = 180,
            Margin = new Thickness(0, 0, 0, 8)
        };
        iconScrollViewer.Content = iconGridView;
        stackPanel.Children.Add(iconScrollViewer);
        
        System.Diagnostics.Debug.WriteLine($"Final stackPanel child count: {stackPanel.Children.Count}");

        var controls = new CategoryDialogControls
        {
            NameTextBox = categoryNameTextBox,
            DescriptionTextBox = categoryDescriptionTextBox,
            IconGridView = iconGridView,
            IsBookmarkCategoryCheckBox = isBookmarkCategoryCheckBox,
            IsBookmarkLookupCheckBox = isBookmarkLookupCheckBox,
            PasswordProtectionComboBox = passwordProtectionComboBox,
            OwnPasswordBox = ownPasswordBox,
            ConfirmPasswordBox = confirmPasswordBox
        };

        return (stackPanel, controls);
    }

    private GridView BuildIconPicker(string? currentIcon)
    {
        var iconGridView = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Margin = new Thickness(0, 0, 0, 8)
        };

        int selectedIndex = 0;
        for (int i = 0; i < CategoryIcons.Count; i++)
        {
            var icon = CategoryIcons[i];
            var iconButton = new Border
            {
                Width = 50,
                Height = 50,
                Margin = new Thickness(4),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            iconGridView.Items.Add(iconButton);

            if (!string.IsNullOrEmpty(currentIcon) && icon == currentIcon)
            {
                selectedIndex = i;
            }
        }

        iconGridView.SelectedIndex = selectedIndex;
        return iconGridView;
    }

    private async Task<CategoryEditResult?> CreateCategoryResultAsync(
        CategoryDialogControls controls,
        string? currentPasswordHash)
    {
        string categoryName = controls.NameTextBox.Text.Trim();
        string categoryDescription = controls.DescriptionTextBox.Text.Trim();
        string selectedIcon = CategoryIcons[0];

        if (controls.IconGridView.SelectedIndex >= 0 && controls.IconGridView.SelectedIndex < CategoryIcons.Count)
        {
            selectedIcon = CategoryIcons[controls.IconGridView.SelectedIndex];
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var passwordProtection = PasswordProtectionType.None;
        string? ownPassword = null;

        if (controls.PasswordProtectionComboBox != null)
        {
            var selectedItem = controls.PasswordProtectionComboBox.SelectedItem as ComboBoxItem;
            passwordProtection = selectedItem?.Tag as PasswordProtectionType? ?? PasswordProtectionType.None;

            // Check if Global Password is selected but not set
            if (passwordProtection == PasswordProtectionType.GlobalPassword)
            {
                if (_configService == null || !_configService.HasGlobalPassword())
                {
                    await DialogHelpers.ShowErrorAsync(_xamlRoot,
                        "Global Password Not Set",
                        "A global password has not been configured yet. Please set a global password in the Security Setup (Configuration > Security Setup) before using this option.");
                    return null;
                }
            }

            if (passwordProtection == PasswordProtectionType.OwnPassword && 
                controls.OwnPasswordBox != null && 
                controls.ConfirmPasswordBox != null)
            {
                // Validate password
                if (!string.IsNullOrEmpty(controls.OwnPasswordBox.Password) || 
                    !string.IsNullOrEmpty(controls.ConfirmPasswordBox.Password))
                {
                    if (controls.OwnPasswordBox.Password != controls.ConfirmPasswordBox.Password)
                    {
                        await DialogHelpers.ShowErrorAsync(_xamlRoot,
                            "Password Mismatch",
                            "The passwords do not match. Please try again.");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(controls.OwnPasswordBox.Password))
                    {
                        ownPassword = controls.OwnPasswordBox.Password;
                    }
                }
                else if (currentPasswordHash == null)
                {
                    // New category with own password but no password entered
                    await DialogHelpers.ShowErrorAsync(_xamlRoot,
                        "Password Required",
                        "Please enter a password or select a different protection type.");
                    return null;
                }
            }
        }

        return new CategoryEditResult
        {
            Name = categoryName,
            Description = categoryDescription,
            Icon = selectedIcon,
            PasswordProtection = passwordProtection,
            OwnPassword = ownPassword,
            IsBookmarkCategory = controls.IsBookmarkCategoryCheckBox?.IsChecked ?? false,
            IsBookmarkLookup = controls.IsBookmarkLookupCheckBox?.IsChecked ?? false
        };
    }

    private class CategoryDialogControls
    {
        public TextBox NameTextBox { get; set; } = null!;
        public TextBox DescriptionTextBox { get; set; } = null!;
        public GridView IconGridView { get; set; } = null!;
        public CheckBox? IsBookmarkCategoryCheckBox { get; set; }
        public CheckBox? IsBookmarkLookupCheckBox { get; set; }
        public ComboBox? PasswordProtectionComboBox { get; set; }
        public PasswordBox? OwnPasswordBox { get; set; }
        public PasswordBox? ConfirmPasswordBox { get; set; }
    }
}