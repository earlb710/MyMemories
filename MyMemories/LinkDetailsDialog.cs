using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MyMemories;

/// <summary>
/// Dialog for displaying and editing link details.
/// </summary>
public class LinkDetailsDialog
{
    private readonly Window _parentWindow;
    private readonly XamlRoot _xamlRoot;

    // Generic category icons
    private static readonly List<string> CategoryIcons = new()
    {
        "📁", "📂", "📚", "📖", "📝", "📄", "📋", "📌",
        "🗂️", "🗃️", "🗄️", "📦", "🎯", "⭐", "💼", "🏠",
        "🎨", "🎭", "🎪", "🎬", "🎮", "🎵", "🎸", "📷",
        "🖼️", "🌍", "🌐", "🔧", "🔨", "⚙️", "🔗", "📊",
        "📈", "📉", "💻", "⌨️", "🖥️", "📱", "☁️", "💾",
        "🔒", "🔓", "🔑", "🏆", "🎓", "📚", "✏️", "📐"
    };

    public LinkDetailsDialog(Window parentWindow, XamlRoot xamlRoot)
    {
        _parentWindow = parentWindow;
        _xamlRoot = xamlRoot;
    }

    /// <summary>
    /// Shows the link details dialog.
    /// </summary>
    public async Task<bool> ShowAsync(LinkItem link)
    {
        // Create details panel
        var detailsPanel = new StackPanel { Spacing = 12 };

        // Link title
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Title:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = link.Title,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Link URL/Path
        detailsPanel.Children.Add(new TextBlock
        {
            Text = link.IsDirectory ? "Directory Path:" : "Path/URL:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = link.Url,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Link description
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = "Description:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            detailsPanel.Children.Add(new TextBlock
            {
                Text = link.Description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        // Link type
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Type:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = link.IsDirectory ? "Directory" : "File/URL",
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Create and show dialog
        var dialog = new ContentDialog
        {
            Title = "Link Details",
            Content = detailsPanel,
            CloseButtonText = "Close",
            SecondaryButtonText = "Edit",
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Secondary;
    }

    /// <summary>
    /// Shows the edit link dialog.
    /// </summary>
    public async Task<LinkEditResult?> ShowEditAsync(LinkItem link)
    {
        // Create input fields for editing
        var titleTextBox = new TextBox
        {
            Text = link.Title,
            PlaceholderText = "Enter link title",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var urlTextBox = new TextBox
        {
            Text = link.Url,
            PlaceholderText = "Enter file path, directory, or URL",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var descriptionTextBox = new TextBox
        {
            Text = link.Description,
            PlaceholderText = "Enter description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Create stack panel for dialog content
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Title:", 
            Margin = new Thickness(0, 0, 0, 4) 
        });
        stackPanel.Children.Add(titleTextBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Path/URL:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(urlTextBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Description:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(descriptionTextBox);

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = "Edit Link",
            Content = stackPanel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string newTitle = titleTextBox.Text.Trim();
            string newUrl = urlTextBox.Text.Trim();
            string newDescription = descriptionTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newTitle) || string.IsNullOrWhiteSpace(newUrl))
            {
                return null; // Validation failed
            }

            // Update IsDirectory flag
            bool isDirectory = false;
            try
            {
                isDirectory = System.IO.Directory.Exists(newUrl);
            }
            catch { }

            return new LinkEditResult
            {
                Title = newTitle,
                Url = newUrl,
                Description = newDescription,
                IsDirectory = isDirectory
            };
        }

        return null;
    }

    /// <summary>
    /// Shows the add link dialog.
    /// </summary>
    public async Task<AddLinkResult?> ShowAddAsync(IEnumerable<CategoryNode> categories, CategoryNode? selectedCategory)
    {
        // Create category selector ComboBox - FIRST ITEM
        var categoryComboBox = new ComboBox
        {
            PlaceholderText = "Select a category (required)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Populate categories
        foreach (var category in categories)
        {
            categoryComboBox.Items.Add(new ComboBoxItem 
            { 
                Content = category.Name, 
                Tag = category.Node
            });
            
            // Pre-select if this is the selected category
            if (selectedCategory?.Node == category.Node)
            {
                categoryComboBox.SelectedIndex = categoryComboBox.Items.Count - 1;
            }
        }

        // Create input fields for the dialog
        var titleTextBox = new TextBox
        {
            PlaceholderText = "Enter link title (required)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var urlTextBox = new TextBox
        {
            PlaceholderText = "Enter file path, directory, or URL",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var descriptionTextBox = new TextBox
        {
            PlaceholderText = "Enter description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var browseFileButton = new Button
        {
            Content = "Browse File...",
        };

        var browseFolderButton = new Button
        {
            Content = "Browse Folder...",
        };

        buttonPanel.Children.Add(browseFileButton);
        buttonPanel.Children.Add(browseFolderButton);

        // Create stack panel for dialog content - Category FIRST
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Category: *", 
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(categoryComboBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Title: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(titleTextBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "File Path, Directory, or URL:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(urlTextBox);
        stackPanel.Children.Add(buttonPanel);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Description:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(descriptionTextBox);

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = "Add Link",
            Content = stackPanel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = false // Initially disabled
        };

        // Method to validate form and enable/disable Add button
        void ValidateForm()
        {
            bool hasCategory = categoryComboBox.SelectedIndex >= 0;
            bool hasTitle = !string.IsNullOrWhiteSpace(titleTextBox.Text);
            dialog.IsPrimaryButtonEnabled = hasCategory && hasTitle;
        }

        // Wire up validation events
        categoryComboBox.SelectionChanged += (s, args) => ValidateForm();
        titleTextBox.TextChanged += (s, args) => ValidateForm();

        // Handle browse file button click
        browseFileButton.Click += async (s, args) =>
        {
            try
            {
                var openPicker = new FileOpenPicker();
                var hWnd = WindowNative.GetWindowHandle(_parentWindow);
                InitializeWithWindow.Initialize(openPicker, hWnd);

                openPicker.FileTypeFilter.Add("*");
                
                StorageFile? file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    urlTextBox.Text = file.Path;
                    if (string.IsNullOrWhiteSpace(titleTextBox.Text))
                    {
                        titleTextBox.Text = file.Name;
                    }
                }
            }
            catch (Exception)
            {
                // Error handled by caller
            }
        };

        // Handle browse folder button click
        browseFolderButton.Click += async (s, args) =>
        {
            try
            {
                var folderPicker = new FolderPicker();
                var hWnd = WindowNative.GetWindowHandle(_parentWindow);
                InitializeWithWindow.Initialize(folderPicker, hWnd);

                folderPicker.FileTypeFilter.Add("*");
                
                StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    urlTextBox.Text = folder.Path;
                    if (string.IsNullOrWhiteSpace(titleTextBox.Text))
                    {
                        titleTextBox.Text = folder.Name;
                    }
                }
            }
            catch (Exception)
            {
                // Error handled by caller
            }
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string title = titleTextBox.Text.Trim();
            string url = urlTextBox.Text.Trim();
            string description = descriptionTextBox.Text.Trim();

            if (categoryComboBox.SelectedIndex < 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            // Determine if it's a directory
            bool isDirectory = false;
            try
            {
                isDirectory = System.IO.Directory.Exists(url);
            }
            catch { }

            var selectedItem = categoryComboBox.SelectedItem as ComboBoxItem;
            var targetCategory = selectedItem?.Tag as TreeViewNode;

            if (targetCategory != null)
            {
                return new AddLinkResult
                {
                    Title = title,
                    Url = url,
                    Description = description,
                    IsDirectory = isDirectory,
                    CategoryNode = targetCategory
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Shows the add/edit category dialog with icon picker.
    /// </summary>
    public async Task<CategoryEditResult?> ShowCategoryDialogAsync(string title, string? currentName = null, string? currentDescription = null, string? currentIcon = null)
    {
        // Create input fields FIRST
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

        // Icon picker with GridView - LAST
        var iconGridView = new GridView
        {
            MaxHeight = 300,
            SelectionMode = ListViewSelectionMode.Single,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Populate icon grid and find the index for default/current icon
        int selectedIndex = 0; // Default to folder icon (📁) at index 0
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

            // Pre-select current icon if editing
            if (!string.IsNullOrEmpty(currentIcon) && icon == currentIcon)
            {
                selectedIndex = i;
            }
        }

        // Set initial selection (defaults to 0 which is folder icon 📁)
        iconGridView.SelectedIndex = selectedIndex;

        // Create stack panel for dialog content - Name, Description, THEN Icon
        var stackPanel = new StackPanel();
        
        // Category Name - FIRST
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Category Name: *", 
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(categoryNameTextBox);
        
        // Description - SECOND
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Description:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(categoryDescriptionTextBox);
        
        // Icon Picker - LAST
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Category Icon:", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        
        var iconScrollViewer = new ScrollViewer
        {
            MaxHeight = 300,
            Margin = new Thickness(0, 0, 0, 8)
        };
        iconScrollViewer.Content = iconGridView;
        stackPanel.Children.Add(iconScrollViewer);

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = title,
            Content = stackPanel,
            PrimaryButtonText = currentName == null ? "Create" : "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(currentName)
        };

        // Validate form
        categoryNameTextBox.TextChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(categoryNameTextBox.Text);
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string categoryName = categoryNameTextBox.Text.Trim();
            string categoryDescription = categoryDescriptionTextBox.Text.Trim();
            string selectedIcon = CategoryIcons[0]; // Default to folder icon 📁

            if (iconGridView.SelectedIndex >= 0 && iconGridView.SelectedIndex < CategoryIcons.Count)
            {
                selectedIcon = CategoryIcons[iconGridView.SelectedIndex];
            }

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            return new CategoryEditResult
            {
                Name = categoryName,
                Description = categoryDescription,
                Icon = selectedIcon
            };
        }

        return null;
    }
}
