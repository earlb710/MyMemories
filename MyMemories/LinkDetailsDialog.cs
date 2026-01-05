using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Text;
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

        // Link URL/Path with Copy button
        detailsPanel.Children.Add(new TextBlock
        {
            Text = link.IsDirectory ? "Directory Path:" : "Path/URL:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        
        // Create a panel for URL and copy button
        var urlPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };
        
        var urlTextBlock = new TextBlock
        {
            Text = link.Url,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var copyButton = new Button
        {
            Content = "📋",
            FontSize = 16,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Top
        };
        
        copyButton.Click += (s, e) =>
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(link.Url);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
            catch
            {
                // Silently fail if clipboard access is denied
            }
        };
        
        urlPanel.Children.Add(urlTextBlock);
        urlPanel.Children.Add(copyButton);
        detailsPanel.Children.Add(urlPanel);

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

        // Folder Type (for directories)
        if (link.IsDirectory)
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = "Folder Type:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            
            string folderTypeText = link.FolderType switch
            {
                FolderLinkType.LinkOnly => "🔗 Link Only",
                FolderLinkType.CatalogueFiles => "📂 Catalogue Files",
                FolderLinkType.FilteredCatalogue => "🗂️ Filtered Catalogue",
                _ => "Link Only"
            };
            
            detailsPanel.Children.Add(new TextBlock
            {
                Text = folderTypeText,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // File Filters (if filtered catalogue)
            if (link.FolderType == FolderLinkType.FilteredCatalogue && !string.IsNullOrWhiteSpace(link.FileFilters))
            {
                detailsPanel.Children.Add(new TextBlock
                {
                    Text = "File Filters:",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                detailsPanel.Children.Add(new TextBlock
                {
                    Text = link.FileFilters,
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
        }

        // Link timestamps (created and modified)
        detailsPanel.Children.Add(new TextBlock
        {
            Text = "Link Timestamps:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = $"📅 Created: {link.CreatedDate:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = $"📝 Modified: {link.ModifiedDate:yyyy-MM-dd HH:mm:ss}",
            Margin = new Thickness(0, 0, 0, 8)
        });

        // File/Directory timestamps if it's a file system path
        if (!string.IsNullOrEmpty(link.Url))
        {
            try
            {
                if (link.IsDirectory && Directory.Exists(link.Url))
                {
                    var dirInfo = new DirectoryInfo(link.Url);
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = "Directory Timestamps:",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📅 Created: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📝 Modified: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"👁️ Accessed: {dirInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                }
                else if (File.Exists(link.Url))
                {
                    var fileInfo = new FileInfo(link.Url);
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = "File Timestamps:",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📦 Size: {FormatFileSize((ulong)fileInfo.Length)}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📅 Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📝 Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"👁️ Accessed: {fileInfo.LastAccessTime:yyyy-MM-dd HH:mm:ss}",
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                }
            }
            catch
            {
                // File/directory not accessible - skip file timestamps
            }
        }

        // Create and show dialog
        var dialog = new ContentDialog
        {
            Title = "Link Details",
            Content = new ScrollViewer
            {
                Content = detailsPanel,
                MaxHeight = 600
            },
            CloseButtonText = "Close",
            SecondaryButtonText = "Edit",
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Secondary;
    }

    /// <summary>
    /// Formats file size in a human-readable format.
    /// </summary>
    private static string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
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
        int selectedIndex = -1;
        int index = 0;
        
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
                selectedIndex = index;
            }
            index++;
        }

        // Set selected index after all items are added
        if (selectedIndex >= 0)
        {
            categoryComboBox.SelectedIndex = selectedIndex;
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

        // Folder Type ComboBox - NEW
        var folderTypeComboBox = new ComboBox
        {
            PlaceholderText = "Select folder type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed // Initially hidden
        };
        
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Link Only", Tag = FolderLinkType.LinkOnly });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Catalogue Files", Tag = FolderLinkType.CatalogueFiles });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Filtered Catalogue", Tag = FolderLinkType.FilteredCatalogue });
        folderTypeComboBox.SelectedIndex = 0; // Default to "Link Only"

        // File Filters TextBox - NEW
        var fileFiltersTextBox = new TextBox
        {
            PlaceholderText = "Enter file filters (e.g., *.txt;*.pdf or *.jpg,*.png)",
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed // Initially hidden
        };

        var folderTypeLabel = new TextBlock 
        { 
            Text = "Folder Type:", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = Visibility.Collapsed
        };

        var fileFiltersLabel = new TextBlock 
        { 
            Text = "File Filters: (separate with ; or ,)", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = Visibility.Collapsed,
            FontStyle = FontStyle.Italic
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
        
        // Add folder type controls
        stackPanel.Children.Add(folderTypeLabel);
        stackPanel.Children.Add(folderTypeComboBox);
        stackPanel.Children.Add(fileFiltersLabel);
        stackPanel.Children.Add(fileFiltersTextBox);
        
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
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = selectedIndex >= 0 // Enable if category is pre-selected
        };

        // Method to check if URL is a directory and show/hide folder options
        void CheckDirectoryAndUpdateUI()
        {
            bool isDirectory = false;
            try
            {
                var url = urlTextBox.Text.Trim();
                isDirectory = !string.IsNullOrWhiteSpace(url) && Directory.Exists(url);
            }
            catch { }

            folderTypeLabel.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;
            folderTypeComboBox.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;

            // Show file filters only if Filtered Catalogue is selected
            if (isDirectory && folderTypeComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is FolderLinkType folderType && 
                folderType == FolderLinkType.FilteredCatalogue)
            {
                fileFiltersLabel.Visibility = Visibility.Visible;
                fileFiltersTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                fileFiltersLabel.Visibility = Visibility.Collapsed;
                fileFiltersTextBox.Visibility = Visibility.Collapsed;
            }
        }

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
        urlTextBox.TextChanged += (s, args) => CheckDirectoryAndUpdateUI();
        folderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();

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
                    CheckDirectoryAndUpdateUI();
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
                isDirectory = Directory.Exists(url);
            }
            catch { }

            // Get folder type and filters
            var folderType = FolderLinkType.LinkOnly;
            string fileFilters = string.Empty;

            if (isDirectory && folderTypeComboBox.SelectedItem is ComboBoxItem selectedFolderTypeItem &&
                selectedFolderTypeItem.Tag is FolderLinkType selectedFolderType)
            {
                folderType = selectedFolderType;
                
                if (folderType == FolderLinkType.FilteredCatalogue)
                {
                    fileFilters = fileFiltersTextBox.Text.Trim();
                }
            }

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
                    CategoryNode = targetCategory,
                    FolderType = folderType,
                    FileFilters = fileFilters
                };
            }
        }

        return null;
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

        // Folder Type ComboBox - NEW
        var folderTypeComboBox = new ComboBox
        {
            PlaceholderText = "Select folder type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = link.IsDirectory ? Visibility.Visible : Visibility.Collapsed
        };
        
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Link Only", Tag = FolderLinkType.LinkOnly });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Catalogue Files", Tag = FolderLinkType.CatalogueFiles });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Filtered Catalogue", Tag = FolderLinkType.FilteredCatalogue });
        
        // Set current folder type
        folderTypeComboBox.SelectedIndex = (int)link.FolderType;

        // File Filters TextBox - NEW
        var fileFiltersTextBox = new TextBox
        {
            Text = link.FileFilters,
            PlaceholderText = "Enter file filters (e.g., *.txt;*.pdf or *.jpg,*.png)",
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = (link.IsDirectory && link.FolderType == FolderLinkType.FilteredCatalogue) 
                ? Visibility.Visible 
                : Visibility.Collapsed
        };

        var folderTypeLabel = new TextBlock 
        { 
            Text = "Folder Type:", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = link.IsDirectory ? Visibility.Visible : Visibility.Collapsed
        };

        var fileFiltersLabel = new TextBlock 
        { 
            Text = "File Filters: (separate with ; or ,)", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = (link.IsDirectory && link.FolderType == FolderLinkType.FilteredCatalogue) 
                ? Visibility.Visible 
                : Visibility.Collapsed,
            FontStyle = FontStyle.Italic
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
        
        // Add folder type controls
        stackPanel.Children.Add(folderTypeLabel);
        stackPanel.Children.Add(folderTypeComboBox);
        stackPanel.Children.Add(fileFiltersLabel);
        stackPanel.Children.Add(fileFiltersTextBox);
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Description:", 
            Margin = new Thickness(0, 8, 0, 4) 
        });
        stackPanel.Children.Add(descriptionTextBox);

        // Method to check if URL is a directory and show/hide folder options
        void CheckDirectoryAndUpdateUI()
        {
            bool isDirectory = false;
            try
            {
                var url = urlTextBox.Text.Trim();
                isDirectory = !string.IsNullOrWhiteSpace(url) && Directory.Exists(url);
            }
            catch { }

            folderTypeLabel.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;
            folderTypeComboBox.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;

            // Show file filters only if Filtered Catalogue is selected
            if (isDirectory && folderTypeComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is FolderLinkType folderType && 
                folderType == FolderLinkType.FilteredCatalogue)
            {
                fileFiltersLabel.Visibility = Visibility.Visible;
                fileFiltersTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                fileFiltersLabel.Visibility = Visibility.Collapsed;
                fileFiltersTextBox.Visibility = Visibility.Collapsed;
            }
        }

        // Wire up events
        urlTextBox.TextChanged += (s, args) => CheckDirectoryAndUpdateUI();
        folderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = "Edit Link",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
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
                isDirectory = Directory.Exists(newUrl);
            }
            catch { }

            // Get folder type and filters
            var folderType = FolderLinkType.LinkOnly;
            string fileFilters = string.Empty;

            if (isDirectory && folderTypeComboBox.SelectedItem is ComboBoxItem selectedFolderTypeItem &&
                selectedFolderTypeItem.Tag is FolderLinkType selectedFolderType)
            {
                folderType = selectedFolderType;
                
                if (folderType == FolderLinkType.FilteredCatalogue)
                {
                    fileFilters = fileFiltersTextBox.Text.Trim();
                }
            }

            return new LinkEditResult
            {
                Title = newTitle,
                Url = newUrl,
                Description = newDescription,
                IsDirectory = isDirectory,
                FolderType = folderType,
                FileFilters = fileFilters
            };
        }

        return null;
    }

    /// <summary>
    /// Shows the move link dialog to select a new category.
    /// </summary>
    public async Task<MoveLinkResult?> ShowMoveLinkAsync(IEnumerable<CategoryNode> allCategories, TreeViewNode currentCategoryNode, string linkTitle)
    {
        // Create category selector ComboBox
        var categoryComboBox = new ComboBox
        {
            PlaceholderText = "Select target category (required)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Populate categories, excluding the current category
        foreach (var category in allCategories)
        {
            // Don't allow moving to the same category
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
            // No other categories available
            var noCategoriesDialog = new ContentDialog
            {
                Title = "No Categories Available",
                Content = "There are no other categories to move this link to. Please create another category first.",
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };
            await noCategoriesDialog.ShowAsync();
            return null;
        }

        // Create info text
        var infoText = new TextBlock
        {
            Text = $"Move '{linkTitle}' to:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };

        // Create stack panel for dialog content
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(infoText);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Target Category: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(categoryComboBox);

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = "Move Link",
            Content = stackPanel,
            PrimaryButtonText = "Move",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = false // Initially disabled
        };

        // Enable button when category is selected
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
                return new MoveLinkResult
                {
                    TargetCategoryNode = targetCategory
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
        
        // Description - SECOND (with label)
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

    /// <summary>
    /// Shows the zip folder dialog.
    /// </summary>
    public async Task<ZipFolderResult?> ShowZipFolderDialogAsync(string folderTitle, string defaultTargetDirectory)
    {
        // Create input fields
        var zipFileNameTextBox = new TextBox
        {
            Text = folderTitle,
            PlaceholderText = "Enter zip file name (without .zip extension)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var targetDirectoryTextBox = new TextBox
        {
            Text = defaultTargetDirectory,
            PlaceholderText = "Enter target directory path",
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var browseButton = new Button
        {
            Content = "Browse...",
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var linkToCategoryCheckBox = new CheckBox
        {
            Content = "Link zip file to parent category",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var infoText = new TextBlock
        {
            Text = "This will create a zip archive of the folder and optionally add it as a link in the parent category.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Create stack panel for dialog content
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(infoText);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Zip File Name: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(zipFileNameTextBox);
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Target Directory: *", 
            Margin = new Thickness(0, 8, 0, 4),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stackPanel.Children.Add(targetDirectoryTextBox);
        stackPanel.Children.Add(browseButton);
        stackPanel.Children.Add(linkToCategoryCheckBox);

        // Create and configure the dialog
        var dialog = new ContentDialog
        {
            Title = "Create Zip Archive",
            Content = stackPanel,
            PrimaryButtonText = "Create Zip",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                      !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text)
        };

        // Handle browse button click
        browseButton.Click += async (s, args) =>
        {
            try
            {
                var folderPicker = new FolderPicker();
                var hWnd = WindowNative.GetWindowHandle(_parentWindow);
                InitializeWithWindow.Initialize(folderPicker, hWnd);

                folderPicker.FileTypeFilter.Add("*");
                
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    targetDirectoryTextBox.Text = folder.Path;
                    dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                                     !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text);
                }
            }
            catch (Exception)
            {
                // Error handled silently
            }
        };

        // Validate form when text changes
        zipFileNameTextBox.TextChanged += (s, args) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(zipFileNameTextBox.Text) && 
                                             !string.IsNullOrWhiteSpace(targetDirectoryTextBox.Text);
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string zipFileName = zipFileNameTextBox.Text.Trim();
            string targetDirectory = targetDirectoryTextBox.Text.Trim();
            bool linkToCategory = linkToCategoryCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(zipFileName) || string.IsNullOrWhiteSpace(targetDirectory))
            {
                return null; // Validation failed
            }

            // Validate that target directory exists
            if (!Directory.Exists(targetDirectory))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Invalid Directory",
                    Content = "The target directory does not exist. Please select a valid directory.",
                    CloseButtonText = "OK",
                    XamlRoot = _xamlRoot
                };
                await errorDialog.ShowAsync();
                return null;
            }

            return new ZipFolderResult
            {
                ZipFileName = zipFileName,
                TargetDirectory = targetDirectory,
                LinkToCategory = linkToCategory
            };
        }

        return null;
    }
}
