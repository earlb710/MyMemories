using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MyMemories;

/// <summary>
/// Page with a tree view on the left and detail display on the right.
/// </summary>
public sealed partial class TreeViewPage : Page
{
    private ObservableCollection<TreeItemViewModel> _rootItems;
    private TreeItemViewModel? _selectedItem;

    public TreeViewPage()
    {
        this.InitializeComponent();
        InitializeTreeView();
    }

    private void InitializeTreeView()
    {
        _rootItems = new ObservableCollection<TreeItemViewModel>
        {
            new TreeItemViewModel
            {
                Name = "Photos",
                Icon = "\uE8B9", // Pictures icon
                IsExpanded = true,
                Children = new ObservableCollection<TreeItemViewModel>
                {
                    new TreeItemViewModel { Name = "Family", Icon = "\uE716", Count = "(12)" },
                    new TreeItemViewModel { Name = "Vacation 2025", Icon = "\uE716", Count = "(45)" },
                    new TreeItemViewModel { Name = "Events", Icon = "\uE716", Count = "(8)" }
                }
            },
            new TreeItemViewModel
            {
                Name = "Documents",
                Icon = "\uE8A5", // Document icon
                IsExpanded = true,
                Children = new ObservableCollection<TreeItemViewModel>
                {
                    new TreeItemViewModel { Name = "Work", Icon = "\uE8F4", Count = "(23)" },
                    new TreeItemViewModel { Name = "Personal", Icon = "\uE8F4", Count = "(15)" },
                    new TreeItemViewModel { Name = "Archive", Icon = "\uE8F4", Count = "(67)" }
                }
            },
            new TreeItemViewModel
            {
                Name = "Web Links",
                Icon = "\uE71B", // Globe icon
                Children = new ObservableCollection<TreeItemViewModel>
                {
                    new TreeItemViewModel { Name = "Bookmarks", Icon = "\uE734", Count = "(31)" },
                    new TreeItemViewModel { Name = "References", Icon = "\uE734", Count = "(18)" }
                }
            },
            new TreeItemViewModel
            {
                Name = "Notes",
                Icon = "\uE70B", // Note icon
                Children = new ObservableCollection<TreeItemViewModel>
                {
                    new TreeItemViewModel { Name = "Ideas", Icon = "\uE81E", Count = "(9)" },
                    new TreeItemViewModel { Name = "Journal", Icon = "\uE81E", Count = "(42)" }
                }
            }
        };

        ItemsTreeView.ItemsSource = _rootItems;
    }

    private void ItemsTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeItemViewModel item)
        {
            _selectedItem = item;
            ShowItemDetails(item);
        }
    }

    private void ShowItemDetails(TreeItemViewModel item)
    {
        // Update header
        DetailTitle.Text = item.Name;
        DetailSubtitle.Text = $"Category • {item.Count}";

        // Show detail content
        EmptyState.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;

        // Update properties
        TypeText.Text = "Category";
        CreatedText.Text = DateTime.Now.AddMonths(-3).ToString("MMMM dd, yyyy");
        ModifiedText.Text = DateTime.Now.ToString("MMMM dd, yyyy");
        SizeText.Text = $"{item.Count}";
        LocationText.Text = $"My Memories/{GetParentPath(item)}";

        // Sample description
        DescriptionBox.Text = $"This is a collection of items related to {item.Name}.";

        // Sample tags
        TagsPanel.ItemsSource = new[] { "Important", item.Name, "2025" };

        // Sample related items
        RelatedItemsList.ItemsSource = new[]
        {
            new RelatedItem { Name = "Related item 1", Icon = "\uE8A5" },
            new RelatedItem { Name = "Related item 2", Icon = "\uE8A5" },
            new RelatedItem { Name = "Related item 3", Icon = "\uE8A5" }
        };

        // Hide image preview for now (show for actual image items)
        ImagePreview.Visibility = Visibility.Collapsed;
    }

    private string GetParentPath(TreeItemViewModel item)
    {
        // Simple implementation - in a real app, track parent relationships
        return item.Name;
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // TODO: Implement search functionality
        var query = args.QueryText;
        // Filter tree items based on query
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Show dialog to add new category
        var newCategory = new TreeItemViewModel
        {
            Name = "New Category",
            Icon = "\uE8F4",
            Count = "(0)"
        };
        _rootItems.Add(newCategory);
    }

    private async void ImportFiles_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement file import functionality
        var dialog = new ContentDialog
        {
            Title = "Import Files",
            Content = "File import functionality will be implemented here.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement edit functionality
        if (_selectedItem != null)
        {
            // Enable editing mode
        }
    }

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Item",
            Content = $"Are you sure you want to delete '{_selectedItem.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // TODO: Delete the item
            _rootItems.Remove(_selectedItem);
            EmptyState.Visibility = Visibility.Visible;
            DetailContent.Visibility = Visibility.Collapsed;
        }
    }

    private async void ShareItem_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement share functionality
        var dialog = new ContentDialog
        {
            Title = "Share",
            Content = "Share functionality will be implemented here.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void AddTag_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Show dialog to add new tag
        var dialog = new ContentDialog
        {
            Title = "Add Tag",
            Content = new TextBox { PlaceholderText = "Enter tag name..." },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

/// <summary>
/// View model for tree items.
/// </summary>
public class TreeItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE8A5";
    public string Count { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
    public Visibility ShowCount => string.IsNullOrEmpty(Count) ? Visibility.Collapsed : Visibility.Visible;
    public ObservableCollection<TreeItemViewModel>? Children { get; set; }
}

/// <summary>
/// Model for related items.
/// </summary>
public class RelatedItem
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE8A5";
}