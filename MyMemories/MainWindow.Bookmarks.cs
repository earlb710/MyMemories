using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Bookmark import functionality for MainWindow.
/// </summary>
public sealed partial class MainWindow
{
    private async void MenuFile_ImportBookmarks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var importer = new BookmarkImporterService();
            var browsers = importer.DetectInstalledBrowsers();

            if (!browsers.Any())
            {
                await ShowErrorDialogAsync("No Browsers Found", 
                    "No supported browsers were detected on this system.\n\n" +
                    "Supported browsers: Chrome, Edge, Brave, Vivaldi, Opera");
                return;
            }

            // Show browser selection dialog
            var selectedBrowser = await ShowBrowserSelectionDialogAsync(browsers);
            if (selectedBrowser == null)
                return;

            // Show import options dialog
            var importOptionsResult = await ShowImportOptionsDialogAsync();
            if (importOptionsResult == null)
                return;

            var (importOptions, createNewCategory, organizeByFolder) = importOptionsResult.Value;

            // Import bookmarks
            StatusText.Text = $"Importing bookmarks from {selectedBrowser.Name}...";
            
            var result = await importer.ImportBookmarksAsync(selectedBrowser.BookmarksPath, importOptions);

            if (!result.IsSuccess)
            {
                await ShowErrorDialogAsync("Import Failed", result.ErrorMessage ?? "Unknown error occurred");
                StatusText.Text = "Ready";
                return;
            }

            if (result.Bookmarks.Count == 0)
            {
                await ShowErrorDialogAsync("No Bookmarks Found", 
                    "No bookmarks were found matching the specified criteria.");
                StatusText.Text = "Ready";
                return;
            }

            // Select target category
            var targetCategory = await SelectTargetCategoryAsync(createNewCategory, selectedBrowser);
            if (targetCategory == null)
            {
                StatusText.Text = "Import cancelled";
                return;
            }

            // Add bookmarks to category
            await AddBookmarksToCategory(result.Bookmarks, targetCategory, organizeByFolder);

            // Update bookmark count if it's a new import category
            if (createNewCategory && targetCategory.Content is CategoryItem catItem)
            {
                catItem.ImportedBookmarkCount = result.Bookmarks.Count;
                await _categoryService!.SaveCategoryAsync(targetCategory);
            }

            StatusText.Text = $"Successfully imported {result.Bookmarks.Count} bookmarks";

            // Show success dialog
            await ShowSuccessDialogAsync("Bookmarks Imported Successfully", 
                $"Imported {result.Bookmarks.Count} bookmark(s) from {selectedBrowser.Name} into category '{GetCategoryName(targetCategory)}'.");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.MenuFile_ImportBookmarks_Click", "Error importing bookmarks", ex);
            await ShowErrorDialogAsync("Import Error", $"An error occurred while importing bookmarks:\n\n{ex.Message}");
            StatusText.Text = "Ready";
        }
    }

    private async Task<BrowserInfo?> ShowBrowserSelectionDialogAsync(System.Collections.Generic.List<BrowserInfo> browsers)
    {
        var stackPanel = new StackPanel { Spacing = 12 };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select the browser to import bookmarks from:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            SelectedIndex = 0
        };

        // Add browsers directly to Items collection with custom visual layout
        foreach (var browser in browsers)
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Tag = browser,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = GetBrowserGlyph(browser.BrowserType),
                        FontSize = 24,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock 
                            { 
                                Text = browser.Name,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                            },
                            new TextBlock 
                            { 
                                Text = browser.BookmarksPath,
                                FontSize = 11,
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            };
            
            listView.Items.Add(item);
        }

        stackPanel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = "Import Browser Bookmarks",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 400
            },
            PrimaryButtonText = "Next",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && listView.SelectedItem is StackPanel selectedPanel)
        {
            return selectedPanel.Tag as BrowserInfo;
        }

        return null;
    }

    private async Task<(ImportOptions? Options, bool CreateNewCategory, bool OrganizeByFolder)?> ShowImportOptionsDialogAsync()
    {
        var stackPanel = new StackPanel { Spacing = 16 };

        // Import options
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Import Options:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var includeFoldersCheckBox = new CheckBox
        {
            Content = "Import folder structure",
            IsChecked = true
        };
        stackPanel.Children.Add(includeFoldersCheckBox);

        var organizeFoldersCheckBox = new CheckBox
        {
            Content = "Create subcategories for bookmark folders",
            IsChecked = false,
            Margin = new Thickness(20, 0, 0, 0)
        };
        stackPanel.Children.Add(organizeFoldersCheckBox);

        // Enable/disable based on include folders
        includeFoldersCheckBox.Checked += (s, e) => organizeFoldersCheckBox.IsEnabled = true;
        includeFoldersCheckBox.Unchecked += (s, e) =>
        {
            organizeFoldersCheckBox.IsEnabled = false;
            organizeFoldersCheckBox.IsChecked = false;
        };

        var skipDuplicatesCheckBox = new CheckBox
        {
            Content = "Skip duplicate URLs",
            IsChecked = true
        };
        stackPanel.Children.Add(skipDuplicatesCheckBox);

        // Category options
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Target Category:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 8)
        });

        var existingCategoryRadio = new RadioButton
        {
            Content = "Add to existing category",
            IsChecked = true,
            GroupName = "CategoryOption"
        };
        stackPanel.Children.Add(existingCategoryRadio);

        var newCategoryRadio = new RadioButton
        {
            Content = "Create new category",
            GroupName = "CategoryOption"
        };
        stackPanel.Children.Add(newCategoryRadio);

        // URL filter (optional)
        stackPanel.Children.Add(new TextBlock
        {
            Text = "URL Filter (optional):",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        });

        var urlFilterTextBox = new TextBox
        {
            PlaceholderText = "e.g., github.com (leave empty to import all)",
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(urlFilterTextBox);

        stackPanel.Children.Add(new TextBlock
        {
            Text = "?? Tip: The browser must be closed before importing bookmarks.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = "Import Options",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 500
            },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var options = new ImportOptions
            {
                IncludeFolders = includeFoldersCheckBox.IsChecked == true,
                SkipDuplicates = skipDuplicatesCheckBox.IsChecked == true,
                UrlFilter = string.IsNullOrWhiteSpace(urlFilterTextBox.Text) ? null : urlFilterTextBox.Text
            };

            return (options, newCategoryRadio.IsChecked == true, organizeFoldersCheckBox.IsChecked == true);
        }

        return null;
    }

    private async Task<TreeViewNode?> SelectTargetCategoryAsync(bool createNew, BrowserInfo? browserInfo = null)
    {
        if (createNew)
        {
            // Create a new category for bookmarks with import metadata
            var categoryName = $"Imported Bookmarks - {DateTime.Now:yyyy-MM-dd}";
            
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = categoryName,
                    Description = browserInfo != null 
                        ? $"Imported from {browserInfo.Name} on {DateTime.Now:yyyy-MM-dd HH:mm}"
                        : $"Imported bookmarks on {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Icon = "\U0001F516", // ?? Bookmark emoji (proper Unicode escape)
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsBookmarkImport = browserInfo != null,
                    SourceBrowserType = browserInfo?.BrowserType,
                    SourceBrowserName = browserInfo?.Name,
                    SourceBookmarksPath = browserInfo?.BookmarksPath,
                    LastBookmarkImportDate = DateTime.Now,
                    ImportedBookmarkCount = 0, // Will be set after adding bookmarks
                    IsBookmarkCategory = true // Always true for imported bookmarks
                }
            };

            _treeViewService!.InsertCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(categoryNode);

            return categoryNode;
        }
        else
        {
            // Show category selection dialog
            var allCategories = GetAllCategoriesFlat();

            if (!allCategories.Any())
            {
                await ShowErrorDialogAsync("No Categories", 
                    "No categories exist. Please create a category first or select 'Create new category' option.");
                return null;
            }

            var comboBox = new ComboBox
            {
                ItemsSource = allCategories,
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var dialog = new ContentDialog
            {
                Title = "Select Target Category",
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Select the category to add bookmarks to:",
                            TextWrapping = TextWrapping.Wrap 
                        },
                        comboBox
                    }
                },
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && comboBox.SelectedItem is CategoryNode selectedCategory)
            {
                return selectedCategory.Node;
            }

            return null;
        }
    }

    private async Task AddBookmarksToCategory(System.Collections.Generic.List<BookmarkItem> bookmarks, 
        TreeViewNode targetCategory, bool organizeByFolder)
    {
        var categoryPath = _treeViewService!.GetCategoryPath(targetCategory);
        var addedCount = 0;

        if (organizeByFolder)
        {
            // Group bookmarks by folder path and create subcategories
            var folderGroups = bookmarks.GroupBy(b => b.FolderPath);

            foreach (var group in folderGroups)
            {
                var folderPath = group.Key;
                
                // Create subcategory for this folder (if it doesn't exist)
                var subCategory = await GetOrCreateSubcategoryAsync(targetCategory, folderPath);

                // Add bookmarks to subcategory
                foreach (var bookmark in group)
                {
                    if (await AddBookmarkLinkAsync(subCategory, bookmark, categoryPath))
                        addedCount++;
                }
            }
        }
        else
        {
            // Add all bookmarks to the target category
            foreach (var bookmark in bookmarks)
            {
                if (await AddBookmarkLinkAsync(targetCategory, bookmark, categoryPath))
                    addedCount++;
            }
        }

        // Save the category
        var rootNode = GetRootCategoryNode(targetCategory);
        await _categoryService!.SaveCategoryAsync(rootNode);

        LogUtilities.LogInfo("MainWindow.AddBookmarksToCategory", 
            $"Added {addedCount} bookmarks to category '{categoryPath}'");
    }

    private async Task<TreeViewNode> GetOrCreateSubcategoryAsync(TreeViewNode parentCategory, string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return parentCategory;

        // Check if subcategory already exists
        var existingSubcat = parentCategory.Children
            .FirstOrDefault(n => n.Content is CategoryItem cat && cat.Name == folderPath);

        if (existingSubcat != null)
            return existingSubcat;

        // Get parent's bookmark category flag to inherit
        var parentCategoryItem = parentCategory.Content as CategoryItem;
        bool parentIsBookmarkCategory = parentCategoryItem?.IsBookmarkCategory ?? false;

        // Create new subcategory with proper Unicode emoji
        var subCategoryNode = new TreeViewNode
        {
            Content = new CategoryItem
            {
                Name = folderPath,
                Description = $"Bookmarks from {folderPath}",
                Icon = "\U0001F4D1", // ?? Bookmark tabs emoji (proper Unicode escape)
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                IsBookmarkCategory = parentIsBookmarkCategory // Inherit from parent
            }
        };

        _treeViewService!.InsertSubCategoryNode(parentCategory, subCategoryNode);
        
        return subCategoryNode;
    }

    private async Task<bool> AddBookmarkLinkAsync(TreeViewNode categoryNode, BookmarkItem bookmark, string categoryPath)
    {
        try
        {
            // Check for duplicates
            var isDuplicate = categoryNode.Children
                .Any(n => n.Content is LinkItem link && link.Url == bookmark.Url);

            if (isDuplicate)
                return false;

            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = bookmark.Name,
                    Url = bookmark.Url,
                    Description = $"Imported from {bookmark.FolderPath}",
                    IsDirectory = false,
                    CategoryPath = categoryPath,
                    CreatedDate = bookmark.DateAdded,
                    ModifiedDate = DateTime.Now
                }
            };

            categoryNode.Children.Add(linkNode);
            categoryNode.IsExpanded = true;

            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.AddBookmarkLinkAsync", 
                $"Error adding bookmark: {bookmark.Name}", ex);
            return false;
        }
    }

    private async Task ShowSuccessDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private string GetCategoryName(TreeViewNode categoryNode)
    {
        if (categoryNode.Content is CategoryItem category)
            return category.Name;
        return "Unknown";
    }

    private string GetBrowserGlyph(BrowserType browserType)
    {
        return browserType switch
        {
            BrowserType.Chrome => "\uE774", // Globe icon
            BrowserType.Edge => "\uE737",   // Edge icon
            BrowserType.Brave => "\uE8A1",  // Shield icon  
            BrowserType.Vivaldi => "\uE773", // Music note
            BrowserType.Opera => "\uE8A5",   // World icon
            BrowserType.Firefox => "\uE7E8", // Fire icon
            _ => "\uE774" // Default globe
        };
    }

    /// <summary>
    /// Refreshes bookmarks from the original browser source.
    /// </summary>
    public async Task RefreshBookmarksAsync(CategoryItem category, TreeViewNode categoryNode)
    {
        if (!category.IsBookmarkImport || string.IsNullOrEmpty(category.SourceBookmarksPath))
        {
            await ShowErrorDialogAsync("Cannot Refresh", 
                "This category is not a bookmark import or source information is missing.");
            return;
        }

        try
        {
            // Detect if the original import was organized by folders
            // If there are subcategories with bookmark folders, preserve that structure
            bool wasOrganizedByFolders = categoryNode.Children
                .Any(n => n.Content is CategoryItem subCat && 
                         !string.IsNullOrEmpty(subCat.Description) && 
                         subCat.Description.StartsWith("Bookmarks from "));

            // Show confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Refresh Bookmarks",
                Content = $"This will re-import bookmarks from {category.SourceBrowserName}.\n\n" +
                         $"Current bookmarks: {category.ImportedBookmarkCount ?? 0}\n" +
                         $"Last import: {category.LastBookmarkImportDate:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Organization: {(wasOrganizedByFolders ? "By Folder" : "Flat")}\n\n" +
                         "Would you like to:\n" +
                         "• Replace all existing bookmarks\n" +
                         "• Add only new bookmarks (skip duplicates)",
                PrimaryButtonText = "Replace All",
                SecondaryButtonText = "Add New Only",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.None)
                return;

            bool replaceAll = result == ContentDialogResult.Primary;

            StatusText.Text = $"Refreshing bookmarks from {category.SourceBrowserName}...";

            // Import bookmarks
            var importer = new BookmarkImporterService();
            var importOptions = new ImportOptions
            {
                IncludeFolders = true,
                SkipDuplicates = !replaceAll // Only skip duplicates if adding new only
            };

            var importResult = await importer.ImportBookmarksAsync(category.SourceBookmarksPath, importOptions);

            if (!importResult.IsSuccess)
            {
                await ShowErrorDialogAsync("Refresh Failed", importResult.ErrorMessage ?? "Unknown error occurred");
                StatusText.Text = "Ready";
                return;
            }

            if (importResult.Bookmarks.Count == 0)
            {
                await ShowErrorDialogAsync("No Bookmarks Found", 
                    "No bookmarks were found in the browser bookmarks file.");
                StatusText.Text = "Ready";
                return;
            }

            // If replace all, clear existing content
            if (replaceAll)
            {
                if (wasOrganizedByFolders)
                {
                    // Remove all subcategories and links
                    var childrenToRemove = categoryNode.Children.ToList();
                    foreach (var child in childrenToRemove)
                    {
                        categoryNode.Children.Remove(child);
                    }
                }
                else
                {
                    // Remove only links (keep subcategories if any)
                    var linksToRemove = categoryNode.Children
                        .Where(n => n.Content is LinkItem)
                        .ToList();

                    foreach (var linkNode in linksToRemove)
                    {
                        categoryNode.Children.Remove(linkNode);
                    }
                }
            }

            // Add the bookmarks using the same organization as original import
            await AddBookmarksToCategory(importResult.Bookmarks, categoryNode, wasOrganizedByFolders);

            // Update metadata
            category.LastBookmarkImportDate = DateTime.Now;
            category.ImportedBookmarkCount = importResult.Bookmarks.Count;
            category.ModifiedDate = DateTime.Now;

            // Save the category
            var rootNode = GetRootCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Refresh the details view
            if (LinksTreeView.SelectedNode == categoryNode)
            {
                await _detailsViewService!.ShowCategoryDetailsAsync(category, categoryNode, 
                    async () => await RefreshBookmarksAsync(category, categoryNode));
                _detailsViewService.ShowCategoryHeader(_treeViewService!.GetCategoryPath(categoryNode), 
                    category.Description, category.Icon);
                HeaderViewerScroll.Visibility = Visibility.Visible;
            }

            StatusText.Text = $"Successfully refreshed {importResult.Bookmarks.Count} bookmarks";

            // Show success dialog
            var orgText = wasOrganizedByFolders ? " (organized by folder)" : "";
            await ShowSuccessDialogAsync("Bookmarks Refreshed", 
                $"Successfully {(replaceAll ? "replaced" : "added")} {importResult.Bookmarks.Count} bookmark(s) from {category.SourceBrowserName}{orgText}.");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.RefreshBookmarksAsync", "Error refreshing bookmarks", ex);
            await ShowErrorDialogAsync("Refresh Error", $"An error occurred while refreshing bookmarks:\n\n{ex.Message}");
            StatusText.Text = "Ready";
        }
    }
}
