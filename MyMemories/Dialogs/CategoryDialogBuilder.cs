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
/// Options for displaying the category dialog.
/// </summary>
public record CategoryDialogOptions
{
    public string? CurrentName { get; init; }
    public string? CurrentDescription { get; init; }
    public string? CurrentIcon { get; init; }
    public string? CurrentKeywords { get; init; }
    public bool IsRootCategory { get; init; } = true;
    public PasswordProtectionType CurrentPasswordProtection { get; init; } = PasswordProtectionType.None;
    public string? CurrentPasswordHash { get; init; }
    public bool CurrentIsBookmarkCategory { get; init; }
    public bool CurrentIsBookmarkLookup { get; init; }
    public bool? CurrentIsAuditLoggingEnabled { get; init; }
    public bool HasNonUrlChildren { get; init; }
}

/// <summary>
/// Builder for category-related dialogs.
/// </summary>
public class CategoryDialogBuilder
{
    private readonly XamlRoot _xamlRoot;
    private readonly ConfigurationService? _configService;
    
    // UI Layout Constants
    private const int TextBoxDescriptionHeight = 80;
    private const int TextBoxKeywordsHeight = 60;
    private const int IconPickerMaxHeight = 180;
    private const int IconButtonSize = 50;
    private const int IconFontSize = 28;
    private const int DialogMaxHeight = 600;
    
    private static readonly List<string> CategoryIcons = new()
    {
        "📁", "📂", "📚", "📖", "📝", "📄", "📋", "📌",
        "🗂️", "🗃️", "🗄️", "📦", "🎯", "⭐", "💼", "🏠",
        "🎨", "🎭", "🎪", "🎬", "🎮", "🎵", "🎸", "📷",
        "🖼️", "🌍", "🌐", "🔧", "🔨", "⚙️", "🔗", "📊",
        "📈", "📉", "💻", "⌨️", "🖥️", "📱", "☁️", "💾",
        "🔒", "🔓", "🔑", "🏆", "🎓", "📚", "✏️", "📐",
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
    public async Task<CategoryEditResult?> ShowCategoryDialogAsync(string title, CategoryDialogOptions? options = null)
    {
        options ??= new CategoryDialogOptions();
        
        bool isNewCategory = options.CurrentName == null;
        bool auditLoggingEnabled = options.CurrentIsAuditLoggingEnabled ?? isNewCategory;
        
        var (stackPanel, controls) = BuildCategoryDialogUI(options, auditLoggingEnabled);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = DialogMaxHeight
            },
            PrimaryButtonText = isNewCategory ? "Create" : "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(options.CurrentName)
        };

        controls.NameTextBox.TextChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(controls.NameTextBox.Text);
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return await CreateCategoryResultAsync(controls, options.CurrentPasswordHash);
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

        foreach (var category in allCategories.Where(c => c.Node != currentCategoryNode))
        {
            categoryComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = category.Name, 
                Tag = category.Node
            });
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
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Target Category: *", new Thickness(0, 8, 0, 4)));
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

        if (result == ContentDialogResult.Primary && 
            categoryComboBox.SelectedItem is ComboBoxItem { Tag: TreeViewNode targetCategory })
        {
            return new MoveLinkResult { TargetCategoryNode = targetCategory };
        }

        return null;
    }

    private (StackPanel, CategoryDialogControls) BuildCategoryDialogUI(CategoryDialogOptions options, bool auditLoggingEnabled)
    {
        var controls = new CategoryDialogControls
        {
            NameTextBox = CreateNameTextBox(options.CurrentName),
            DescriptionTextBox = CreateDescriptionTextBox(options.CurrentDescription),
            KeywordsTextBox = CreateKeywordsTextBox(options.CurrentKeywords),
            IconGridView = BuildIconPicker(options.CurrentIcon)
        };

        var bookmarkControls = BuildBookmarkControls(options);
        controls.IsBookmarkCategoryCheckBox = bookmarkControls.CategoryCheckBox;
        controls.IsBookmarkLookupCheckBox = bookmarkControls.LookupCheckBox;

        if (options.IsRootCategory && _configService?.IsLoggingEnabled() == true)
        {
            controls.IsAuditLoggingCheckBox = CreateAuditLoggingCheckBox(auditLoggingEnabled);
        }

        if (options.IsRootCategory)
        {
            var passwordControls = BuildPasswordControls(options.CurrentPasswordProtection, options.CurrentPasswordHash);
            controls.PasswordProtectionComboBox = passwordControls.ComboBox;
            controls.OwnPasswordBox = passwordControls.PasswordBox;
            controls.ConfirmPasswordBox = passwordControls.ConfirmPasswordBox;
        }

        var stackPanel = AssembleDialogPanel(controls, options, bookmarkControls.InheritedNote);
        return (stackPanel, controls);
    }

    private static TextBox CreateNameTextBox(string? currentName) => new()
    {
        Text = currentName ?? string.Empty,
        PlaceholderText = "Enter category name (required)",
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static TextBox CreateDescriptionTextBox(string? currentDescription) => new()
    {
        Text = currentDescription ?? string.Empty,
        PlaceholderText = "Enter category description (optional)",
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Height = TextBoxDescriptionHeight,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static TextBox CreateKeywordsTextBox(string? currentKeywords) => new()
    {
        Text = currentKeywords ?? string.Empty,
        PlaceholderText = "Enter keywords (comma or semicolon separated, optional)",
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Height = TextBoxKeywordsHeight,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static CheckBox CreateAuditLoggingCheckBox(bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Content = "📝 Enable Audit Logging",
            IsChecked = isChecked,
            Margin = new Thickness(0, 8, 0, 8)
        };
        ToolTipService.SetToolTip(checkBox, "Log all changes to this category to a separate log file");
        return checkBox;
    }

    private static (CheckBox? CategoryCheckBox, CheckBox? LookupCheckBox, TextBlock? InheritedNote) BuildBookmarkControls(CategoryDialogOptions options)
    {
        if (options.HasNonUrlChildren && !options.CurrentIsBookmarkCategory)
        {
            return (null, null, null);
        }

        var categoryCheckBox = new CheckBox
        {
            Content = "🔖 URL Bookmarks Only (restrict to web links)",
            IsChecked = options.CurrentIsBookmarkCategory,
            IsEnabled = options.IsRootCategory || !options.CurrentIsBookmarkCategory,
            Margin = new Thickness(0, 8, 0, 8)
        };

        var lookupCheckBox = new CheckBox
        {
            Content = "🔍 Use for bookmark lookup",
            IsChecked = options.CurrentIsBookmarkLookup,
            IsEnabled = options.CurrentIsBookmarkCategory,
            Visibility = options.CurrentIsBookmarkCategory ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(20, 0, 0, 8)
        };

        categoryCheckBox.Checked += (s, e) =>
        {
            lookupCheckBox.IsEnabled = true;
            lookupCheckBox.Visibility = Visibility.Visible;
        };

        categoryCheckBox.Unchecked += (s, e) =>
        {
            lookupCheckBox.IsEnabled = false;
            lookupCheckBox.Visibility = Visibility.Collapsed;
            lookupCheckBox.IsChecked = false;
        };

        TextBlock? inheritedNote = null;
        if (!options.IsRootCategory && options.CurrentIsBookmarkCategory)
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

        return (categoryCheckBox, lookupCheckBox, inheritedNote);
    }

    private static (ComboBox ComboBox, PasswordBox PasswordBox, PasswordBox ConfirmPasswordBox, TextBlock PasswordLabel, TextBlock ConfirmLabel) BuildPasswordControls(
        PasswordProtectionType currentProtection, 
        string? currentPasswordHash)
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 12),
            MinWidth = 200
        };

        comboBox.Items.Add(new ComboBoxItem { Content = "No Password", Tag = PasswordProtectionType.None });
        comboBox.Items.Add(new ComboBoxItem { Content = "Global Password", Tag = PasswordProtectionType.GlobalPassword });
        comboBox.Items.Add(new ComboBoxItem { Content = "Own Password", Tag = PasswordProtectionType.OwnPassword });
        comboBox.SelectedIndex = (int)currentProtection;

        var isOwnPassword = currentProtection == PasswordProtectionType.OwnPassword;
        var placeholderText = currentPasswordHash != null ? "Leave empty to keep current password" : "Enter password";
        var confirmPlaceholder = currentPasswordHash != null ? "Leave empty to keep current password" : "Confirm password";

        var passwordLabel = new TextBlock
        {
            Text = "Password:",
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed
        };

        var passwordBox = new PasswordBox
        {
            PlaceholderText = placeholderText,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed
        };

        var confirmLabel = new TextBlock
        {
            Text = "Confirm Password:",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed
        };

        var confirmBox = new PasswordBox
        {
            PlaceholderText = confirmPlaceholder,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = isOwnPassword ? Visibility.Visible : Visibility.Collapsed
        };

        comboBox.SelectionChanged += (s, args) =>
        {
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            var protectionType = selectedItem?.Tag as PasswordProtectionType? ?? PasswordProtectionType.None;
            var showPassword = protectionType == PasswordProtectionType.OwnPassword;
            var visibility = showPassword ? Visibility.Visible : Visibility.Collapsed;

            passwordLabel.Visibility = visibility;
            passwordBox.Visibility = visibility;
            confirmLabel.Visibility = visibility;
            confirmBox.Visibility = visibility;
        };

        return (comboBox, passwordBox, confirmBox, passwordLabel, confirmLabel);
    }

    private StackPanel AssembleDialogPanel(CategoryDialogControls controls, CategoryDialogOptions options, TextBlock? inheritedNote)
    {
        var stackPanel = new StackPanel();

        // Basic fields
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category Name: *", new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(controls.NameTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(controls.DescriptionTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Keywords:", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(controls.KeywordsTextBox);

        // Audit Logging checkbox (root categories only)
        if (controls.IsAuditLoggingCheckBox != null)
        {
            stackPanel.Children.Add(controls.IsAuditLoggingCheckBox);
        }

        // Bookmark checkboxes
        if (controls.IsBookmarkCategoryCheckBox != null)
        {
            stackPanel.Children.Add(controls.IsBookmarkCategoryCheckBox);
            if (inheritedNote != null)
            {
                stackPanel.Children.Add(inheritedNote);
            }
            if (controls.IsBookmarkLookupCheckBox != null)
            {
                stackPanel.Children.Add(controls.IsBookmarkLookupCheckBox);
            }
        }

        // Password controls (root categories only)
        if (options.IsRootCategory && controls.PasswordProtectionComboBox != null)
        {
            AddPasswordSection(stackPanel, controls);
        }

        // Icon picker
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category Icon:", new Thickness(0, 16, 0, 4)));
        stackPanel.Children.Add(new ScrollViewer
        {
            Content = controls.IconGridView,
            MaxHeight = IconPickerMaxHeight,
            Margin = new Thickness(0, 0, 0, 8)
        });

        return stackPanel;
    }

    private static void AddPasswordSection(StackPanel stackPanel, CategoryDialogControls controls)
    {
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Password Protection:", new Thickness(0, 16, 0, 4)));
        stackPanel.Children.Add(controls.PasswordProtectionComboBox!);

        stackPanel.Children.Add(new TextBlock
        {
            Text = "⚠️ Note: Password protection only restricts access to viewing links in this category. It does not protect the actual files or folders that are linked.",
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (controls.OwnPasswordBox != null)
        {
            // Add password label and box (visibility controlled by combo selection)
            var passwordLabel = new TextBlock
            {
                Text = "Password:",
                Margin = new Thickness(0, 8, 0, 4),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Visibility = controls.OwnPasswordBox.Visibility
            };
            stackPanel.Children.Add(passwordLabel);
            stackPanel.Children.Add(controls.OwnPasswordBox);
        }

        if (controls.ConfirmPasswordBox != null)
        {
            var confirmLabel = new TextBlock
            {
                Text = "Confirm Password:",
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Visibility = controls.ConfirmPasswordBox.Visibility
            };
            stackPanel.Children.Add(confirmLabel);
            stackPanel.Children.Add(controls.ConfirmPasswordBox);
        }
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
            iconGridView.Items.Add(new Border
            {
                Width = IconButtonSize,
                Height = IconButtonSize,
                Margin = new Thickness(4),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = IconFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            if (!string.IsNullOrEmpty(currentIcon) && icon == currentIcon)
            {
                selectedIndex = i;
            }
        }

        iconGridView.SelectedIndex = selectedIndex;
        return iconGridView;
    }

    private async Task<CategoryEditResult?> CreateCategoryResultAsync(CategoryDialogControls controls, string? currentPasswordHash)
    {
        string categoryName = controls.NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        string selectedIcon = controls.IconGridView.SelectedIndex >= 0 && controls.IconGridView.SelectedIndex < CategoryIcons.Count
            ? CategoryIcons[controls.IconGridView.SelectedIndex]
            : CategoryIcons[0];

        var passwordResult = await ValidateAndGetPasswordAsync(controls, currentPasswordHash);
        if (passwordResult == null)
        {
            return null;
        }

        return new CategoryEditResult
        {
            Name = categoryName,
            Description = controls.DescriptionTextBox.Text.Trim(),
            Keywords = controls.KeywordsTextBox?.Text.Trim() ?? string.Empty,
            Icon = selectedIcon,
            PasswordProtection = passwordResult.Value.Protection,
            OwnPassword = passwordResult.Value.Password,
            IsBookmarkCategory = controls.IsBookmarkCategoryCheckBox?.IsChecked ?? false,
            IsBookmarkLookup = controls.IsBookmarkLookupCheckBox?.IsChecked ?? false,
            IsAuditLoggingEnabled = controls.IsAuditLoggingCheckBox?.IsChecked ?? false
        };
    }

    private async Task<(PasswordProtectionType Protection, string? Password)?> ValidateAndGetPasswordAsync(
        CategoryDialogControls controls, 
        string? currentPasswordHash)
    {
        if (controls.PasswordProtectionComboBox == null)
        {
            return (PasswordProtectionType.None, null);
        }

        var selectedItem = controls.PasswordProtectionComboBox.SelectedItem as ComboBoxItem;
        var passwordProtection = selectedItem?.Tag as PasswordProtectionType? ?? PasswordProtectionType.None;

        if (passwordProtection == PasswordProtectionType.GlobalPassword)
        {
            if (_configService == null || !_configService.HasGlobalPassword())
            {
                await DialogHelpers.ShowErrorAsync(_xamlRoot,
                    "Global Password Not Set",
                    "A global password has not been configured yet. Please set a global password in the Security Setup (Configuration > Security Setup) before using this option.");
                return null;
            }
            return (passwordProtection, null);
        }

        if (passwordProtection == PasswordProtectionType.OwnPassword &&
            controls.OwnPasswordBox != null &&
            controls.ConfirmPasswordBox != null)
        {
            var password = controls.OwnPasswordBox.Password;
            var confirmPassword = controls.ConfirmPasswordBox.Password;

            if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirmPassword))
            {
                if (password != confirmPassword)
                {
                    await DialogHelpers.ShowErrorAsync(_xamlRoot, "Password Mismatch", "The passwords do not match. Please try again.");
                    return null;
                }
                return (passwordProtection, string.IsNullOrEmpty(password) ? null : password);
            }
            
            if (currentPasswordHash == null)
            {
                await DialogHelpers.ShowErrorAsync(_xamlRoot, "Password Required", "Please enter a password or select a different protection type.");
                return null;
            }
        }

        return (passwordProtection, null);
    }

    private sealed class CategoryDialogControls
    {
        public required TextBox NameTextBox { get; init; }
        public required TextBox DescriptionTextBox { get; init; }
        public required TextBox KeywordsTextBox { get; init; }
        public required GridView IconGridView { get; init; }
        public CheckBox? IsBookmarkCategoryCheckBox { get; set; }
        public CheckBox? IsBookmarkLookupCheckBox { get; set; }
        public CheckBox? IsAuditLoggingCheckBox { get; set; }
        public ComboBox? PasswordProtectionComboBox { get; set; }
        public PasswordBox? OwnPasswordBox { get; set; }
        public PasswordBox? ConfirmPasswordBox { get; set; }
    }
}