using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Text;
using WinRT.Interop;

namespace MyMemories.Dialogs;

/// <summary>
/// Builder for link-related dialogs (add/edit).
/// </summary>
public class LinkDialogBuilder
{
    private readonly Window _parentWindow;
    private readonly XamlRoot _xamlRoot;
    private readonly FolderPickerService _folderPickerService;
    private readonly WebSummaryService _webSummaryService;
    private List<TreeViewNode>? _bookmarkLookupCategories;
    private CancellationTokenSource? _summarizeCts;

    public LinkDialogBuilder(Window parentWindow, XamlRoot xamlRoot)
    {
        _parentWindow = parentWindow;
        _xamlRoot = xamlRoot;
        _folderPickerService = new FolderPickerService(parentWindow);
        _webSummaryService = new WebSummaryService();
    }
    
    /// <summary>
    /// Sets the bookmark lookup categories for bookmark browsing.
    /// </summary>
    public void SetBookmarkLookupCategories(List<TreeViewNode> categories)
    {
        _bookmarkLookupCategories = categories;
    }

    /// <summary>
    /// Shows the add link dialog.
    /// </summary>
    public async Task<AddLinkResult?> ShowAddAsync(
        IEnumerable<CategoryNode> categories, 
        CategoryNode? selectedCategory)
    {
        var (stackPanel, controls) = BuildAddLinkUI(categories, selectedCategory, out int selectedIndex);
        
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
            IsPrimaryButtonEnabled = selectedIndex >= 0
        };

        SetupAddLinkEventHandlers(controls, dialog);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return CreateAddLinkResult(controls);
        }

        return null;
    }

    /// <summary>
    /// Shows the edit link dialog.
    /// </summary>
    public async Task<LinkEditResult?> ShowEditAsync(LinkItem link)
    {
        var (stackPanel, controls) = BuildEditLinkUI(link);

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

        SetupEditLinkEventHandlers(controls);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return CreateEditLinkResult(controls);
        }

        return null;
    }

    private (StackPanel, LinkDialogControls) BuildAddLinkUI(
        IEnumerable<CategoryNode> categories,
        CategoryNode? selectedCategory,
        out int selectedIndex)
    {
        var categoryComboBox = new ComboBox
        {
            PlaceholderText = "Select a category (required)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        selectedIndex = PopulateCategoryComboBox(categoryComboBox, categories, selectedCategory);

        // Link Type ComboBox
        var linkTypeComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };
        linkTypeComboBox.Items.Add(new ComboBoxItem { Content = "\U0001F310 URL", Tag = "URL" }); // ??
        linkTypeComboBox.Items.Add(new ComboBoxItem { Content = "\U0001F4C4 File", Tag = "File" }); // ??
        linkTypeComboBox.Items.Add(new ComboBoxItem { Content = "\U0001F4C1 Folder", Tag = "Folder" }); // ??
        linkTypeComboBox.SelectedIndex = 0; // Default to URL

        var titleTextBox = new TextBox
        {
            PlaceholderText = "Enter link title (required)",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var urlTextBox = new TextBox
        {
            PlaceholderText = "Enter URL",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var descriptionTextBox = new TextBox
        {
            PlaceholderText = "Enter description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false,
            MinWidth = 500,
            MinHeight = 150,
            MaxHeight = 300,
            Margin = new Thickness(0, 0, 0, 8)
        };
        // Enable vertical scrolling for long descriptions
        ScrollViewer.SetVerticalScrollBarVisibility(descriptionTextBox, ScrollBarVisibility.Auto);

        var keywordsTextBox = new TextBox
        {
            PlaceholderText = "Enter keywords (comma or semicolon separated, optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Bookmark category info text (initially hidden)
        var bookmarkInfoText = new TextBlock
        {
            Text = "\U0001F516 This is a URL Bookmarks category - only web links (http:// or https://) are allowed.", // ??
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed
        };

        // Browse button (initially hidden, shown only for File/Folder)
        var browseButton = new Button 
        { 
            Content = "Browse...",
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed
        };

        // Summarize button and progress ring
        var summarizeProgress = new ProgressRing
        {
            IsActive = false,
            Width = 16,
            Height = 16,
            Visibility = Visibility.Collapsed
        };

        var summarizeButton = new Button
        {
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE8A1", FontSize = 14 }, // Lightbulb icon
                    new TextBlock { Text = "Summarize URL", VerticalAlignment = VerticalAlignment.Center },
                    summarizeProgress
                }
            }
        };

        var (folderControls, _) = BuildFolderControls();

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category: *", new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(categoryComboBox);
        stackPanel.Children.Add(bookmarkInfoText);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Link Type: *", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(linkTypeComboBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Location: *", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(urlTextBox);
        stackPanel.Children.Add(browseButton);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Title: *", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(titleTextBox);
        
        stackPanel.Children.Add(folderControls.TypeLabel);
        stackPanel.Children.Add(folderControls.TypeComboBox);
        stackPanel.Children.Add(folderControls.FiltersLabel);
        stackPanel.Children.Add(folderControls.FiltersTextBox);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(descriptionTextBox);
        stackPanel.Children.Add(summarizeButton);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Keywords (comma or semicolon separated):", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(keywordsTextBox);

        var controls = new LinkDialogControls
        {
            CategoryComboBox = categoryComboBox,
            LinkTypeComboBox = linkTypeComboBox,
            TitleTextBox = titleTextBox,
            UrlTextBox = urlTextBox,
            DescriptionTextBox = descriptionTextBox,
            KeywordsTextBox = keywordsTextBox,
            FolderTypeComboBox = folderControls.TypeComboBox,
            FileFiltersTextBox = folderControls.FiltersTextBox,
            FolderTypeLabel = folderControls.TypeLabel,
            FileFiltersLabel = folderControls.FiltersLabel,
            BrowseButton = browseButton,
            BookmarkInfoText = bookmarkInfoText,
            SummarizeButton = summarizeButton,
            SummarizeProgress = summarizeProgress
        };

        return (stackPanel, controls);
    }

    private (StackPanel, LinkDialogControls) BuildEditLinkUI(LinkItem link)
    {
        // Log the description being loaded for debugging
        System.Diagnostics.Debug.WriteLine($"[BuildEditLinkUI] Loading description for '{link.Title}': Length={link.Description?.Length ?? 0}");
        if (!string.IsNullOrEmpty(link.Description))
        {
            System.Diagnostics.Debug.WriteLine($"[BuildEditLinkUI] Description preview: {link.Description.Substring(0, Math.Min(100, link.Description.Length))}...");
        }

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
            PlaceholderText = "Enter description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false,
            MinWidth = 500,
            MinHeight = 150,
            MaxHeight = 300,
            Margin = new Thickness(0, 0, 0, 8)
        };
        // Enable vertical scrolling for long descriptions
        ScrollViewer.SetVerticalScrollBarVisibility(descriptionTextBox, ScrollBarVisibility.Auto);
        
        // Set text after control creation to ensure proper initialization
        descriptionTextBox.Text = link.Description ?? string.Empty;

        var keywordsTextBox = new TextBox
        {
            Text = link.Keywords,
            PlaceholderText = "Enter keywords (comma or semicolon separated)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Summarize button and progress ring for edit dialog
        var summarizeProgress = new ProgressRing
        {
            IsActive = false,
            Width = 16,
            Height = 16,
            Visibility = Visibility.Collapsed
        };

        var summarizeButton = new Button
        {
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = IsWebUrl(link.Url) ? Visibility.Visible : Visibility.Collapsed,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE8A1", FontSize = 14 },
                    new TextBlock { Text = "Summarize URL", VerticalAlignment = VerticalAlignment.Center },
                    summarizeProgress
                }
            }
        };

        var (folderControls, _) = BuildFolderControls(link.IsDirectory, link.FolderType, link.FileFilters);

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Title:", new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(titleTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Path/URL:", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(urlTextBox);
        
        stackPanel.Children.Add(folderControls.TypeLabel);
        stackPanel.Children.Add(folderControls.TypeComboBox);
        stackPanel.Children.Add(folderControls.FiltersLabel);
        stackPanel.Children.Add(folderControls.FiltersTextBox);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(descriptionTextBox);
        stackPanel.Children.Add(summarizeButton);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Keywords (comma or semicolon separated):", new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(keywordsTextBox);

        var controls = new LinkDialogControls
        {
            TitleTextBox = titleTextBox,
            UrlTextBox = urlTextBox,
            DescriptionTextBox = descriptionTextBox,
            KeywordsTextBox = keywordsTextBox,
            FolderTypeComboBox = folderControls.TypeComboBox,
            FileFiltersTextBox = folderControls.FiltersTextBox,
            FolderTypeLabel = folderControls.TypeLabel,
            FileFiltersLabel = folderControls.FiltersLabel,
            SummarizeButton = summarizeButton,
            SummarizeProgress = summarizeProgress
        };

        return (stackPanel, controls);
    }

    private (FolderControlsGroup, StackPanel?) BuildFolderControls(
        bool initiallyVisible = false,
        FolderLinkType folderType = FolderLinkType.LinkOnly,
        string? fileFilters = null)
    {
        var folderTypeComboBox = new ComboBox
        {
            PlaceholderText = "Select folder type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = initiallyVisible ? Visibility.Visible : Visibility.Collapsed
        };
        
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Link Only", Tag = FolderLinkType.LinkOnly });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Catalogue Files", Tag = FolderLinkType.CatalogueFiles });
        folderTypeComboBox.Items.Add(new ComboBoxItem { Content = "Filtered Catalogue", Tag = FolderLinkType.FilteredCatalogue });
        folderTypeComboBox.SelectedIndex = (int)folderType;

        var fileFiltersTextBox = new TextBox
        {
            Text = fileFilters ?? string.Empty,
            PlaceholderText = "Enter file filters (e.g., *.txt;*.pdf or *.jpg,*.png)",
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = (initiallyVisible && folderType == FolderLinkType.FilteredCatalogue) 
                ? Visibility.Visible 
                : Visibility.Collapsed
        };

        var folderTypeLabel = new TextBlock 
        { 
            Text = "Folder Type:", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = initiallyVisible ? Visibility.Visible : Visibility.Collapsed
        };

        var fileFiltersLabel = new TextBlock 
        { 
            Text = "File Filters: (separate with ; or ,)", 
            Margin = new Thickness(0, 8, 0, 4),
            Visibility = (initiallyVisible && folderType == FolderLinkType.FilteredCatalogue) 
                ? Visibility.Visible 
                : Visibility.Collapsed,
            FontStyle = FontStyle.Italic
        };

        return (new FolderControlsGroup
        {
            TypeComboBox = folderTypeComboBox,
            FiltersTextBox = fileFiltersTextBox,
            TypeLabel = folderTypeLabel,
            FiltersLabel = fileFiltersLabel
        }, null);
    }

    private int PopulateCategoryComboBox(
        ComboBox comboBox,
        IEnumerable<CategoryNode> categories,
        CategoryNode? selectedCategory)
    {
        int selectedIndex = -1;
        int index = 0;
        
        foreach (var category in categories)
        {
            comboBox.Items.Add(new ComboBoxItem 
            { 
                Content = category.Name, 
                Tag = category.Node
            });
            
            if (selectedCategory?.Node == category.Node)
            {
                selectedIndex = index;
            }
            index++;
        }

        if (selectedIndex >= 0)
        {
            comboBox.SelectedIndex = selectedIndex;
        }

        return selectedIndex;
    }

    private void SetupAddLinkEventHandlers(LinkDialogControls controls, ContentDialog dialog)
    {
        void ValidateForm()
        {
            bool hasCategory = controls.CategoryComboBox?.SelectedIndex >= 0;
            bool hasTitle = !string.IsNullOrWhiteSpace(controls.TitleTextBox.Text);
            dialog.IsPrimaryButtonEnabled = hasCategory && hasTitle;
        }

        void UpdateSummarizeButtonVisibility()
        {
            // Show summarize button only for URL link type and when URL is a web URL
            string linkType = "URL";
            if (controls.LinkTypeComboBox?.SelectedItem is ComboBoxItem linkTypeItem)
            {
                linkType = linkTypeItem.Tag?.ToString() ?? "URL";
            }

            bool showSummarize = linkType == "URL" && IsWebUrl(controls.UrlTextBox.Text);
            if (controls.SummarizeButton != null)
            {
                controls.SummarizeButton.Visibility = showSummarize ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void UpdateUIForCategoryAndLinkType()
        {
            // Check if selected category is a bookmark category
            bool isBookmarkCategory = false;
            if (controls.CategoryComboBox?.SelectedItem is ComboBoxItem categoryItem &&
                categoryItem.Tag is TreeViewNode categoryNode &&
                categoryNode.Content is CategoryItem category)
            {
                isBookmarkCategory = category.IsBookmarkCategory;
            }

            // Get selected link type
            string linkType = "URL";
            if (controls.LinkTypeComboBox?.SelectedItem is ComboBoxItem linkTypeItem)
            {
                linkType = linkTypeItem.Tag?.ToString() ?? "URL";
            }

            // Lock link type to URL for bookmark categories
            if (isBookmarkCategory)
            {
                controls.LinkTypeComboBox.SelectedIndex = 0; // URL
                controls.LinkTypeComboBox.IsEnabled = false;
                controls.UrlTextBox.PlaceholderText = "Enter URL (http:// or https://)";
                controls.BookmarkInfoText.Visibility = Visibility.Visible;
                controls.BrowseButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                controls.LinkTypeComboBox.IsEnabled = true;
                controls.BookmarkInfoText.Visibility = Visibility.Collapsed;
                
                // Update UI based on link type
                switch (linkType)
                {
                    case "URL":
                        controls.UrlTextBox.PlaceholderText = "Enter URL (e.g., https://example.com)";
                        controls.BrowseButton.Visibility = Visibility.Visible;
                        controls.BrowseButton.Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children =
                            {
                                new FontIcon { Glyph = "\uE71B", FontSize = 14 },
                                new TextBlock { Text = "Browse Bookmarks", VerticalAlignment = VerticalAlignment.Center }
                            }
                        };
                        break;
                    case "File":
                        controls.UrlTextBox.PlaceholderText = "Enter file path or click Browse";
                        controls.BrowseButton.Visibility = Visibility.Visible;
                        controls.BrowseButton.Content = "Browse File...";
                        break;
                    case "Folder":
                        controls.UrlTextBox.PlaceholderText = "Enter folder path or click Browse";
                        controls.BrowseButton.Visibility = Visibility.Visible;
                        controls.BrowseButton.Content = "Browse Folder...";
                        break;
                }
            }
            
            UpdateSummarizeButtonVisibility();
        }

        void CheckDirectoryAndUpdateUI()
        {
            // Check if selected category is a bookmark category
            bool isBookmarkCategory = false;
            if (controls.CategoryComboBox?.SelectedItem is ComboBoxItem categoryItem &&
                categoryItem.Tag is TreeViewNode categoryNode &&
                categoryNode.Content is CategoryItem category)
            {
                isBookmarkCategory = category.IsBookmarkCategory;
            }

            // Don't show folder controls for bookmark categories
            if (isBookmarkCategory)
            {
                controls.FolderTypeLabel.Visibility = Visibility.Collapsed;
                controls.FolderTypeComboBox.Visibility = Visibility.Collapsed;
                controls.FileFiltersLabel.Visibility = Visibility.Collapsed;
                controls.FileFiltersTextBox.Visibility = Visibility.Collapsed;
                return;
            }

            bool isDirectory = false;
            try
            {
                var url = controls.UrlTextBox.Text.Trim();
                isDirectory = !string.IsNullOrWhiteSpace(url) && Directory.Exists(url);
            }
            catch { }

            controls.FolderTypeLabel.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;
            controls.FolderTypeComboBox.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;

            if (isDirectory && controls.FolderTypeComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is FolderLinkType folderType && 
                folderType == FolderLinkType.FilteredCatalogue)
            {
                controls.FileFiltersLabel.Visibility = Visibility.Visible;
                controls.FileFiltersTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                controls.FileFiltersLabel.Visibility = Visibility.Collapsed;
                controls.FileFiltersTextBox.Visibility = Visibility.Collapsed;
            }
            
            UpdateSummarizeButtonVisibility();
        }

        controls.CategoryComboBox!.SelectionChanged += (s, args) =>
        {
            ValidateForm();
            UpdateUIForCategoryAndLinkType();
            CheckDirectoryAndUpdateUI();
        };
        
        controls.LinkTypeComboBox!.SelectionChanged += (s, args) =>
        {
            UpdateUIForCategoryAndLinkType();
        };
        
        controls.TitleTextBox.TextChanged += (s, args) => ValidateForm();
        controls.UrlTextBox.TextChanged += (s, args) =>
        {
            CheckDirectoryAndUpdateUI();
            UpdateSummarizeButtonVisibility();
        };
        controls.FolderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();

        controls.BrowseButton!.Click += async (s, args) =>
        {
            var linkType = (controls.LinkTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "URL";
            
            if (linkType == "URL")
            {
                await BrowseBookmarksAsync(controls.UrlTextBox, controls.TitleTextBox, controls.DescriptionTextBox);
            }
            else if (linkType == "File")
            {
                await BrowseForFileAsync(controls.UrlTextBox, controls.TitleTextBox);
            }
            else if (linkType == "Folder")
            {
                BrowseForFolder(controls.UrlTextBox, controls.TitleTextBox, CheckDirectoryAndUpdateUI);
            }
        };

        // Handle summarize button click
        if (controls.SummarizeButton != null)
        {
            controls.SummarizeButton.Click += async (s, args) =>
            {
                await SummarizeUrlAsync(controls);
                ValidateForm(); // Re-validate in case title was populated
            };
        }

        UpdateUIForCategoryAndLinkType();
        CheckDirectoryAndUpdateUI();
    }

    private void SetupEditLinkEventHandlers(LinkDialogControls controls)
    {
        void UpdateSummarizeButtonVisibility()
        {
            bool showSummarize = IsWebUrl(controls.UrlTextBox.Text);
            if (controls.SummarizeButton != null)
            {
                controls.SummarizeButton.Visibility = showSummarize ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void CheckDirectoryAndUpdateUI()
        {
            bool isDirectory = false;
            try
            {
                var url = controls.UrlTextBox.Text.Trim();
                isDirectory = !string.IsNullOrWhiteSpace(url) && Directory.Exists(url);
            }
            catch { }

            controls.FolderTypeLabel.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;
            controls.FolderTypeComboBox.Visibility = isDirectory ? Visibility.Visible : Visibility.Collapsed;

            if (isDirectory && controls.FolderTypeComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is FolderLinkType folderType && 
                folderType == FolderLinkType.FilteredCatalogue)
            {
                controls.FileFiltersLabel.Visibility = Visibility.Visible;
                controls.FileFiltersTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                controls.FileFiltersLabel.Visibility = Visibility.Collapsed;
                controls.FileFiltersTextBox.Visibility = Visibility.Collapsed;
            }
            
            UpdateSummarizeButtonVisibility();
        }

        controls.UrlTextBox.TextChanged += (s, args) =>
        {
            CheckDirectoryAndUpdateUI();
            UpdateSummarizeButtonVisibility();
        };
        controls.FolderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();

        // Handle summarize button click
        if (controls.SummarizeButton != null)
        {
            controls.SummarizeButton.Click += async (s, args) =>
            {
                await SummarizeUrlAsync(controls);
            };
        }
    }

    private async Task BrowseForFileAsync(TextBox urlTextBox, TextBox titleTextBox)
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
        catch { }
    }

    private void BrowseForFolder(
        TextBox urlTextBox, 
        TextBox titleTextBox, 
        Action onFolderSelected)
    {
        var currentPath = urlTextBox.Text.Trim();
        var startingDirectory = !string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath) 
            ? currentPath 
            : null;

        var selectedPath = _folderPickerService.BrowseForFolder(startingDirectory, "Select Folder");
        
        if (!string.IsNullOrEmpty(selectedPath))
        {
            urlTextBox.Text = selectedPath;
            if (string.IsNullOrWhiteSpace(titleTextBox.Text))
            {
                titleTextBox.Text = Path.GetFileName(selectedPath);
            }
            onFolderSelected();
        }
    }
    
    /// <summary>
    /// Shows bookmark selection dialog and populates URL and title fields.
    /// Uses a Flyout instead of ContentDialog to avoid nesting dialogs.
    /// </summary>
    private async Task BrowseBookmarksAsync(TextBox urlTextBox, TextBox titleTextBox, TextBox descriptionTextBox)
    {
        // Ensure we're on the UI thread
        await Task.Yield();
        
        if (_bookmarkLookupCategories == null || _bookmarkLookupCategories.Count == 0)
        {
            // Show a simple info bar instead of a dialog
            var infoBar = new InfoBar
            {
                Title = "No Bookmark Categories",
                Message = "To use this feature:\n1. Create or edit a bookmark category\n2. Check '?? URL Bookmarks Only'\n3. Check '?? Use for bookmark lookup'",
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            // We can't easily insert this into the current dialog, so just return
            // User will need to set up bookmark categories first
            return;
        }

        // Build bookmark tree
        var treeView = new TreeView
        {
            SelectionMode = TreeViewSelectionMode.Single,
            MinHeight = 300,
            MaxHeight = 400,
            Width = 500
        };

        // Keep reference to original categories for search reset
        var originalCategories = new List<TreeViewNode>();
        foreach (var categoryNode in _bookmarkLookupCategories)
        {
            var clonedNode = CloneTreeNode(categoryNode);
            clonedNode.IsExpanded = true; // Expand root nodes by default
            treeView.RootNodes.Add(clonedNode);
            originalCategories.Add(clonedNode);
        }

        var searchBox = new TextBox
        {
            PlaceholderText = "Search bookmarks...",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var selectButton = new Button
        {
            Content = "Select",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
            IsEnabled = false,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 8, 0)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(selectButton);

        var stackPanel = new StackPanel
        {
            Width = 500
        };
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Browse Bookmarks",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        stackPanel.Children.Add(searchBox);
        stackPanel.Children.Add(new ScrollViewer
        {
            Content = treeView,
            MaxHeight = 400,
            Margin = new Thickness(0, 0, 0, 8)
        });
        stackPanel.Children.Add(buttonPanel);

        // Create flyout
        var flyout = new Flyout
        {
            Content = stackPanel,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        };

        // Track selected node
        TreeViewNode? selectedNode = null;

        // Function to select and populate a link
        Action<LinkItem> SelectLink = (LinkItem selectedLink) =>
        {
            urlTextBox.Text = selectedLink.Url;
            if (string.IsNullOrWhiteSpace(titleTextBox.Text))
            {
                titleTextBox.Text = selectedLink.Title;
            }
            if (string.IsNullOrWhiteSpace(descriptionTextBox.Text) && !string.IsNullOrWhiteSpace(selectedLink.Description))
            {
                descriptionTextBox.Text = selectedLink.Description;
            }
            flyout.Hide();
        };

        // Enable select button when a link is selected
        treeView.SelectionChanged += (s, e) =>
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewNode node)
            {
                selectedNode = node;
                selectButton.IsEnabled = node.Content is LinkItem;
            }
            else
            {
                selectedNode = null;
                selectButton.IsEnabled = false;
            }
        };

        // Handle double-click/double-tap on tree items
        treeView.ItemInvoked += (s, e) =>
        {
            if (e.InvokedItem is TreeViewNode node && node.Content is LinkItem link)
            {
                SelectLink(link);
            }
        };

        // Implement search filtering
        searchBox.TextChanged += (s, e) =>
        {
            var searchText = searchBox.Text.Trim().ToLowerInvariant();
            
            // Clear and rebuild tree based on search
            treeView.RootNodes.Clear();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all - restore original
                foreach (var node in originalCategories)
                {
                    treeView.RootNodes.Add(node);
                }
            }
            else
            {
                // Filter and show only matching nodes
                foreach (var originalNode in originalCategories)
                {
                    var filteredNode = BuildFilteredTree(originalNode, searchText);
                    if (filteredNode != null)
                    {
                        filteredNode.IsExpanded = true; // Expand matching categories
                        treeView.RootNodes.Add(filteredNode);
                        ExpandAllChildren(filteredNode); // Expand all children to show matches
                    }
                }
            }
        };

        // Handle select button click
        selectButton.Click += (s, e) =>
        {
            if (selectedNode != null && selectedNode.Content is LinkItem selectedLink)
            {
                SelectLink(selectedLink);
            }
        };

        // Handle cancel button click
        cancelButton.Click += (s, e) =>
        {
            flyout.Hide();
        };

        // Show flyout attached to the browse button instead of URL textbox for better positioning
        flyout.ShowAt(urlTextBox);
    }

    /// <summary>
    /// Expands all children recursively.
    /// </summary>
    private void ExpandAllChildren(TreeViewNode node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            ExpandAllChildren(child);
        }
    }

    /// <summary>
    /// Clones a tree node for display in bookmark browser.
    /// </summary>
    private TreeViewNode CloneTreeNode(TreeViewNode source)
    {
        var clone = new TreeViewNode
        {
            Content = source.Content,
            IsExpanded = false
        };

        foreach (var child in source.Children)
        {
            clone.Children.Add(CloneTreeNode(child));
        }

        return clone;
    }

    /// <summary>
    /// Builds a filtered tree containing only matching nodes.
    /// Returns null if no matches found in this branch.
    /// </summary>
    private TreeViewNode? BuildFilteredTree(TreeViewNode source, string searchText)
    {
        bool currentNodeMatches = false;

        // Check if current node matches
        if (source.Content is LinkItem link)
        {
            currentNodeMatches = link.Title.ToLowerInvariant().Contains(searchText) ||
                                (!string.IsNullOrWhiteSpace(link.Url) && link.Url.ToLowerInvariant().Contains(searchText)) ||
                                (!string.IsNullOrWhiteSpace(link.Description) && link.Description.ToLowerInvariant().Contains(searchText)) ||
                                (!string.IsNullOrWhiteSpace(link.Keywords) && MatchesKeywordsSearch(link.Keywords, searchText));
        }
        else if (source.Content is CategoryItem category)
        {
            currentNodeMatches = category.Name.ToLowerInvariant().Contains(searchText) ||
                                (!string.IsNullOrWhiteSpace(category.Keywords) && MatchesKeywordsSearch(category.Keywords, searchText));
        }

        // Recursively filter children
        var matchingChildren = new List<TreeViewNode>();
        foreach (var child in source.Children)
        {
            var filteredChild = BuildFilteredTree(child, searchText);
            if (filteredChild != null)
            {
                matchingChildren.Add(filteredChild);
            }
        }

        // If current node matches OR any children match, include this node
        if (currentNodeMatches || matchingChildren.Count > 0)
        {
            var filteredNode = new TreeViewNode
            {
                Content = source.Content,
                IsExpanded = false
            };

            foreach (var child in matchingChildren)
            {
                filteredNode.Children.Add(child);
            }

            return filteredNode;
        }

        return null;
    }

    /// <summary>
    /// Checks if the search term matches any of the keywords (comma or semicolon separated).
    /// </summary>
    private bool MatchesKeywordsSearch(string keywords, string searchLower)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return false;

        var keywordList = keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var keyword in keywordList)
        {
            var trimmedKeyword = keyword.Trim().ToLowerInvariant();
            if (trimmedKeyword.Contains(searchLower) || searchLower.Contains(trimmedKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private AddLinkResult? CreateAddLinkResult(LinkDialogControls controls)
    {
        string title = controls.TitleTextBox.Text.Trim();
        string url = controls.UrlTextBox.Text.Trim();
        string description = controls.DescriptionTextBox.Text.Trim();
        string keywords = controls.KeywordsTextBox?.Text.Trim() ?? string.Empty;

        if (controls.CategoryComboBox?.SelectedIndex < 0 || 
            string.IsNullOrWhiteSpace(title) || 
            string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var categoryItem = controls.CategoryComboBox.SelectedItem as ComboBoxItem;
        var targetCategory = categoryItem?.Tag as TreeViewNode;

        if (targetCategory == null)
            return null;

        // Check if target category is a URL Bookmarks category
        var targetCategoryItem = targetCategory.Content as CategoryItem;
        if (targetCategoryItem?.IsBookmarkCategory == true)
        {
            // Validate that the URL is a web link (http:// or https://)
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Show error via ContentDialog
                var errorDialog = new ContentDialog
                {
                    Title = "Invalid URL",
                    Content = "URL Bookmarks categories only allow web links.\n\n" +
                             "Please enter a URL starting with http:// or https://",
                    CloseButtonText = "OK",
                    XamlRoot = _xamlRoot
                };
                _ = errorDialog.ShowAsync(); // Fire and forget
                return null;
            }
        }

        bool isDirectory = false;
        try { isDirectory = Directory.Exists(url); } catch { }

        var folderType = FolderLinkType.LinkOnly;
        string fileFilters = string.Empty;

        if (isDirectory && controls.FolderTypeComboBox.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is FolderLinkType selectedFolderType)
        {
            folderType = selectedFolderType;
            if (folderType == FolderLinkType.FilteredCatalogue)
            {
                fileFilters = controls.FileFiltersTextBox.Text.Trim();
            }
        }

        return new AddLinkResult
        {
            Title = title,
            Url = url,
            Description = description,
            Keywords = keywords,
            IsDirectory = isDirectory,
            CategoryNode = targetCategory,
            FolderType = folderType,
            FileFilters = fileFilters
        };
    }

    private LinkEditResult? CreateEditLinkResult(LinkDialogControls controls)
    {
        string newTitle = controls.TitleTextBox.Text.Trim();
        string newUrl = controls.UrlTextBox.Text.Trim();
        string newDescription = controls.DescriptionTextBox.Text.Trim();
        string newKeywords = controls.KeywordsTextBox?.Text.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newTitle) || string.IsNullOrWhiteSpace(newUrl))
        {
            return null;
        }

        bool isDirectory = false;
        try { isDirectory = Directory.Exists(newUrl); } catch { }

        var folderType = FolderLinkType.LinkOnly;
        string fileFilters = string.Empty;

        if (isDirectory && controls.FolderTypeComboBox.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is FolderLinkType selectedFolderType)
        {
            folderType = selectedFolderType;
            if (folderType == FolderLinkType.FilteredCatalogue)
            {
                fileFilters = controls.FileFiltersTextBox.Text.Trim();
            }
        }

        return new LinkEditResult
        {
            Title = newTitle,
            Url = newUrl,
            Description = newDescription,
            Keywords = newKeywords,
            IsDirectory = isDirectory,
            FolderType = folderType,
            FileFilters = fileFilters
        };
    }

    private class LinkDialogControls
    {
        public ComboBox? CategoryComboBox { get; set; }
        public ComboBox? LinkTypeComboBox { get; set; }
        public TextBox TitleTextBox { get; set; } = null!;
        public TextBox UrlTextBox { get; set; } = null!;
        public TextBox DescriptionTextBox { get; set; } = null!;
        public TextBox KeywordsTextBox { get; set; } = null!;
        public ComboBox FolderTypeComboBox { get; set; } = null!;
        public TextBox FileFiltersTextBox { get; set; } = null!;
        public TextBlock FolderTypeLabel { get; set; } = null!;
        public TextBlock FileFiltersLabel { get; set; } = null!;
        public Button? BrowseButton { get; set; }
        public TextBlock BookmarkInfoText { get; set; } = null!;
        public Button? SummarizeButton { get; set; }
        public ProgressRing? SummarizeProgress { get; set; }
    }

    private class FolderControlsGroup
    {
        public ComboBox TypeComboBox { get; set; } = null!;
        public TextBox FiltersTextBox { get; set; } = null!;
        public TextBlock TypeLabel { get; set; } = null!;
        public TextBlock FiltersLabel { get; set; } = null!;
    }

    /// <summary>
    /// Checks if a URL is a web URL (http:// or https://).
    /// </summary>
    private static bool IsWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Summarizes the URL and populates the title and description fields.
    /// </summary>
    private async Task SummarizeUrlAsync(LinkDialogControls controls)
    {
        var url = controls.UrlTextBox.Text.Trim();
        if (!IsWebUrl(url))
            return;

        // Cancel and dispose any existing summarization
        _summarizeCts?.Cancel();
        _summarizeCts?.Dispose();
        _summarizeCts = new CancellationTokenSource();

        try
        {
            // Show progress
            if (controls.SummarizeProgress != null)
            {
                controls.SummarizeProgress.IsActive = true;
                controls.SummarizeProgress.Visibility = Visibility.Visible;
            }
            if (controls.SummarizeButton != null)
            {
                controls.SummarizeButton.IsEnabled = false;
            }

            var summary = await _webSummaryService.SummarizeUrlAsync(url, _summarizeCts.Token);

            if (summary.Success)
            {
                // Populate title if empty
                if (string.IsNullOrWhiteSpace(controls.TitleTextBox.Text) && !string.IsNullOrWhiteSpace(summary.Title))
                {
                    controls.TitleTextBox.Text = summary.Title;
                }

                // Build description from summary
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

                // Add metadata if available
                if (!string.IsNullOrWhiteSpace(summary.Author) || !string.IsNullOrWhiteSpace(summary.PublishedDate))
                {
                    if (descriptionBuilder.Length > 0)
                        descriptionBuilder.AppendLine();
                    
                    if (!string.IsNullOrWhiteSpace(summary.Author))
                        descriptionBuilder.AppendLine($"Author: {summary.Author}");
                    if (!string.IsNullOrWhiteSpace(summary.PublishedDate))
                        descriptionBuilder.AppendLine($"Published: {summary.PublishedDate}");
                }

                if (descriptionBuilder.Length > 0)
                {
                    controls.DescriptionTextBox.Text = descriptionBuilder.ToString().Trim();
                }

                // Populate keywords if available and field is empty
                if (string.IsNullOrWhiteSpace(controls.KeywordsTextBox?.Text) && summary.Keywords.Count > 0)
                {
                    controls.KeywordsTextBox!.Text = string.Join(", ", summary.Keywords);
                }
            }
            else if (!summary.WasCancelled)
            {
                // Show error in description if it was empty
                if (string.IsNullOrWhiteSpace(controls.DescriptionTextBox.Text))
                {
                    controls.DescriptionTextBox.Text = $"Could not summarize URL: {summary.ErrorMessage}";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled - do nothing
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(controls.DescriptionTextBox.Text))
            {
                controls.DescriptionTextBox.Text = $"Error summarizing URL: {ex.Message}";
            }
        }
        finally
        {
            // Hide progress
            if (controls.SummarizeProgress != null)
            {
                controls.SummarizeProgress.IsActive = false;
                controls.SummarizeProgress.Visibility = Visibility.Collapsed;
            }
            if (controls.SummarizeButton != null)
            {
                controls.SummarizeButton.IsEnabled = true;
            }
        }
    }
}
