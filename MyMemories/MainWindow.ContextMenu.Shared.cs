using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Shared context menu helper methods.
/// </summary>
public sealed partial class MainWindow
{
    // ========================================
    // SHARED HELPER METHODS
    // ========================================

    private async Task ShowSortDialogAsync(TreeViewNode node, SortOption currentSort, bool isCategory)
    {
        var sortComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        sortComboBox.Items.Add(new ComboBoxItem { Content = "Name (A-Z)", Tag = SortOption.NameAscending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Name (Z-A)", Tag = SortOption.NameDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Date (Newest First)", Tag = SortOption.DateDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Date (Oldest First)", Tag = SortOption.DateAscending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Size (Largest First)", Tag = SortOption.SizeDescending });
        sortComboBox.Items.Add(new ComboBoxItem { Content = "Size (Smallest First)", Tag = SortOption.SizeAscending });

        sortComboBox.SelectedIndex = (int)currentSort;

        var dialog = new ContentDialog
        {
            Title = isCategory ? "Sort Category" : "Sort Catalog",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Select sort order:" },
                    sortComboBox
                }
            },
            PrimaryButtonText = "Sort",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && sortComboBox.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is SortOption newSort)
        {
            if (isCategory && node.Content is CategoryItem category)
            {
                category.SortOrder = newSort;
                category.ModifiedDate = DateTime.Now;
                SortingService.SortCategoryChildren(node, newSort);
            }
            else if (!isCategory && node.Content is LinkItem link)
            {
                link.CatalogSortOrder = newSort;
                link.ModifiedDate = DateTime.Now;
                SortingService.SortCatalogEntries(node, newSort);
            }

            var rootNode = GetRootCategoryNode(node);
            await _categoryService!.SaveCategoryAsync(rootNode);

            StatusText.Text = $"Sorted by {selectedItem.Content}";
        }
    }
}
