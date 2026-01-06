using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public LinkDialogBuilder(Window parentWindow, XamlRoot xamlRoot)
    {
        _parentWindow = parentWindow;
        _xamlRoot = xamlRoot;
        _folderPickerService = new FolderPickerService(parentWindow);
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

        var (folderControls, buttonPanel) = BuildFolderControls();

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Category: *", 
            new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(categoryComboBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Title: *", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(titleTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("File Path, Directory, or URL:", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(urlTextBox);
        stackPanel.Children.Add(buttonPanel);
        
        stackPanel.Children.Add(folderControls.TypeLabel);
        stackPanel.Children.Add(folderControls.TypeComboBox);
        stackPanel.Children.Add(folderControls.FiltersLabel);
        stackPanel.Children.Add(folderControls.FiltersTextBox);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(descriptionTextBox);

        var controls = new LinkDialogControls
        {
            CategoryComboBox = categoryComboBox,
            TitleTextBox = titleTextBox,
            UrlTextBox = urlTextBox,
            DescriptionTextBox = descriptionTextBox,
            FolderTypeComboBox = folderControls.TypeComboBox,
            FileFiltersTextBox = folderControls.FiltersTextBox,
            FolderTypeLabel = folderControls.TypeLabel,
            FileFiltersLabel = folderControls.FiltersLabel,
            BrowseFileButton = buttonPanel.Children[0] as Button,
            BrowseFolderButton = buttonPanel.Children[1] as Button
        };

        return (stackPanel, controls);
    }

    private (StackPanel, LinkDialogControls) BuildEditLinkUI(LinkItem link)
    {
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

        var (folderControls, _) = BuildFolderControls(link.IsDirectory, link.FolderType, link.FileFilters);

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Title:", 
            new Thickness(0, 0, 0, 4)));
        stackPanel.Children.Add(titleTextBox);
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Path/URL:", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(urlTextBox);
        
        stackPanel.Children.Add(folderControls.TypeLabel);
        stackPanel.Children.Add(folderControls.TypeComboBox);
        stackPanel.Children.Add(folderControls.FiltersLabel);
        stackPanel.Children.Add(folderControls.FiltersTextBox);
        
        stackPanel.Children.Add(DialogHelpers.CreateLabel("Description:", 
            new Thickness(0, 8, 0, 4)));
        stackPanel.Children.Add(descriptionTextBox);

        var controls = new LinkDialogControls
        {
            TitleTextBox = titleTextBox,
            UrlTextBox = urlTextBox,
            DescriptionTextBox = descriptionTextBox,
            FolderTypeComboBox = folderControls.TypeComboBox,
            FileFiltersTextBox = folderControls.FiltersTextBox,
            FolderTypeLabel = folderControls.TypeLabel,
            FileFiltersLabel = folderControls.FiltersLabel
        };

        return (stackPanel, controls);
    }

    private (FolderControlsGroup, StackPanel) BuildFolderControls(
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

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };
        buttonPanel.Children.Add(new Button { Content = "Browse File..." });
        buttonPanel.Children.Add(new Button { Content = "Browse Folder..." });

        return (new FolderControlsGroup
        {
            TypeComboBox = folderTypeComboBox,
            FiltersTextBox = fileFiltersTextBox,
            TypeLabel = folderTypeLabel,
            FiltersLabel = fileFiltersLabel
        }, buttonPanel);
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
        }

        controls.CategoryComboBox!.SelectionChanged += (s, args) => ValidateForm();
        controls.TitleTextBox.TextChanged += (s, args) => ValidateForm();
        controls.UrlTextBox.TextChanged += (s, args) => CheckDirectoryAndUpdateUI();
        controls.FolderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();

        controls.BrowseFileButton!.Click += async (s, args) => 
            await BrowseForFileAsync(controls.UrlTextBox, controls.TitleTextBox);
        
        controls.BrowseFolderButton!.Click += (s, args) => 
            BrowseForFolder(controls.UrlTextBox, controls.TitleTextBox, CheckDirectoryAndUpdateUI);
    }

    private void SetupEditLinkEventHandlers(LinkDialogControls controls)
    {
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
        }

        controls.UrlTextBox.TextChanged += (s, args) => CheckDirectoryAndUpdateUI();
        controls.FolderTypeComboBox.SelectionChanged += (s, args) => CheckDirectoryAndUpdateUI();
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

    private AddLinkResult? CreateAddLinkResult(LinkDialogControls controls)
    {
        string title = controls.TitleTextBox.Text.Trim();
        string url = controls.UrlTextBox.Text.Trim();
        string description = controls.DescriptionTextBox.Text.Trim();

        if (controls.CategoryComboBox?.SelectedIndex < 0 || 
            string.IsNullOrWhiteSpace(title) || 
            string.IsNullOrWhiteSpace(url))
        {
            return null;
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

        var categoryItem = controls.CategoryComboBox.SelectedItem as ComboBoxItem;
        var targetCategory = categoryItem?.Tag as TreeViewNode;

        if (targetCategory == null)
            return null;

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

    private LinkEditResult? CreateEditLinkResult(LinkDialogControls controls)
    {
        string newTitle = controls.TitleTextBox.Text.Trim();
        string newUrl = controls.UrlTextBox.Text.Trim();
        string newDescription = controls.DescriptionTextBox.Text.Trim();

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
            IsDirectory = isDirectory,
            FolderType = folderType,
            FileFilters = fileFilters
        };
    }

    private class LinkDialogControls
    {
        public ComboBox? CategoryComboBox { get; set; }
        public TextBox TitleTextBox { get; set; } = null!;
        public TextBox UrlTextBox { get; set; } = null!;
        public TextBox DescriptionTextBox { get; set; } = null!;
        public ComboBox FolderTypeComboBox { get; set; } = null!;
        public TextBox FileFiltersTextBox { get; set; } = null!;
        public TextBlock FolderTypeLabel { get; set; } = null!;
        public TextBlock FileFiltersLabel { get; set; } = null!;
        public Button? BrowseFileButton { get; set; }
        public Button? BrowseFolderButton { get; set; }
    }

    private class FolderControlsGroup
    {
        public ComboBox TypeComboBox { get; set; } = null!;
        public TextBox FiltersTextBox { get; set; } = null!;
        public TextBlock TypeLabel { get; set; } = null!;
        public TextBlock FiltersLabel { get; set; } = null!;
    }
}