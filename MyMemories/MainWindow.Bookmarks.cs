using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Bookmark import and export functionality for MainWindow.
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

            // Show enhanced import options dialog with folder selection
            var importOptionsResult = await ShowEnhancedImportOptionsDialogAsync(importer, selectedBrowser);
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

    /// <summary>
    /// Shows an enhanced import options dialog with folder tree selection and domain filters.
    /// </summary>
    private async Task<(ImportOptions? Options, bool CreateNewCategory, bool OrganizeByFolder)?> ShowEnhancedImportOptionsDialogAsync(
        BookmarkImporterService importer, BrowserInfo browser)
    {
        var mainPanel = new StackPanel { Spacing = 12, MinWidth = 550 };

        // Loading indicator while fetching folder structure
        var loadingPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 20, Height = 20 });
        loadingPanel.Children.Add(new TextBlock { Text = "Loading browser bookmarks...", VerticalAlignment = VerticalAlignment.Center });
        mainPanel.Children.Add(loadingPanel);

        var dialog = new ContentDialog
        {
            Title = $"Import from {browser.Name}",
            Content = new ScrollViewer { Content = mainPanel, MaxHeight = 600 },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = Content.XamlRoot
        };

        // Variables to store UI elements
        TreeView? folderTreeView = null;
        ListView? domainListView = null;
        CheckBox? includeFoldersCheckBox = null;
        CheckBox? organizeFoldersCheckBox = null;
        CheckBox? skipDuplicatesCheckBox = null;
        RadioButton? existingCategoryRadio = null;
        RadioButton? newCategoryRadio = null;
        CheckBox? filterByDomainCheckBox = null;

        // Load folder structure asynchronously
        _ = Task.Run(async () =>
        {
            var folderStructure = await importer.GetBrowserFolderStructureAsync(browser.BookmarksPath);
            var domains = await importer.GetUniqueDomains(browser.BookmarksPath);

            DispatcherQueue.TryEnqueue(() =>
            {
                mainPanel.Children.Clear();

                if (folderStructure == null)
                {
                    mainPanel.Children.Add(CreateIconTextBlock("\uE7BA", "Could not read browser bookmarks. Make sure the browser is closed.", Microsoft.UI.Colors.Orange));
                    return;
                }

                // === Folder Selection Section ===
                mainPanel.Children.Add(CreateSectionHeader("\uE8B7", "Select Folders to Import:"));

                folderTreeView = new TreeView
                {
                    SelectionMode = TreeViewSelectionMode.Multiple,
                    MaxHeight = 200
                };

                // Populate folder tree
                foreach (var rootFolder in folderStructure.RootFolders)
                {
                    var node = CreateFolderTreeNode(rootFolder);
                    node.IsExpanded = true;
                    folderTreeView.RootNodes.Add(node);
                }

                // Select all by default
                SelectAllFolderNodes(folderTreeView);

                var folderScroll = new ScrollViewer
                {
                    Content = folderTreeView,
                    MaxHeight = 200,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                mainPanel.Children.Add(folderScroll);

                // Select All / Deselect All buttons for folders
                var folderButtonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var selectAllFoldersBtn = new Button { Content = "Select All Folders", Padding = new Thickness(8, 4, 8, 4) };
                selectAllFoldersBtn.Click += (s, e) => SelectAllFolderNodes(folderTreeView);
                folderButtonPanel.Children.Add(selectAllFoldersBtn);

                var deselectAllFoldersBtn = new Button { Content = "Deselect All", Padding = new Thickness(8, 4, 8, 4) };
                deselectAllFoldersBtn.Click += (s, e) => folderTreeView.SelectedNodes.Clear();
                folderButtonPanel.Children.Add(deselectAllFoldersBtn);
                mainPanel.Children.Add(folderButtonPanel);

                // === Domain Filter Section ===
                filterByDomainCheckBox = new CheckBox
                {
                    Content = CreateIconTextPanel("\uE774", "Filter by Domain (optional)"),
                    IsChecked = false,
                    Margin = new Thickness(0, 12, 0, 4)
                };
                mainPanel.Children.Add(filterByDomainCheckBox);

                domainListView = new ListView
                {
                    SelectionMode = ListViewSelectionMode.Multiple,
                    MaxHeight = 150,
                    Visibility = Visibility.Collapsed
                };

                foreach (var domain in domains.Take(50)) // Limit to 50 domains
                {
                    domainListView.Items.Add(domain);
                }

                var domainScroll = new ScrollViewer
                {
                    Content = domainListView,
                    MaxHeight = 150,
                    Margin = new Thickness(20, 0, 0, 8),
                    Visibility = Visibility.Collapsed
                };
                mainPanel.Children.Add(domainScroll);

                filterByDomainCheckBox.Checked += (s, e) => domainScroll.Visibility = Visibility.Visible;
                filterByDomainCheckBox.Unchecked += (s, e) => domainScroll.Visibility = Visibility.Collapsed;

                // === Import Options Section ===
                mainPanel.Children.Add(CreateSectionHeader("\uE713", "Import Options:"));

                includeFoldersCheckBox = new CheckBox { Content = "Import folder structure", IsChecked = true };
                mainPanel.Children.Add(includeFoldersCheckBox);

                organizeFoldersCheckBox = new CheckBox
                {
                    Content = "Create subcategories for bookmark folders",
                    IsChecked = false,
                    Margin = new Thickness(20, 0, 0, 0)
                };
                mainPanel.Children.Add(organizeFoldersCheckBox);

                includeFoldersCheckBox.Checked += (s, e) => organizeFoldersCheckBox.IsEnabled = true;
                includeFoldersCheckBox.Unchecked += (s, e) =>
                {
                    organizeFoldersCheckBox.IsEnabled = false;
                    organizeFoldersCheckBox.IsChecked = false;
                };

                skipDuplicatesCheckBox = new CheckBox { Content = "Skip duplicate URLs", IsChecked = true };
                mainPanel.Children.Add(skipDuplicatesCheckBox);

                // === Target Category Section ===
                mainPanel.Children.Add(CreateSectionHeader("\uE8B7", "Target Category:"));

                existingCategoryRadio = new RadioButton
                {
                    Content = "Add to existing category",
                    IsChecked = true,
                    GroupName = "CategoryOption"
                };
                mainPanel.Children.Add(existingCategoryRadio);

                newCategoryRadio = new RadioButton
                {
                    Content = "Create new category",
                    GroupName = "CategoryOption"
                };
                mainPanel.Children.Add(newCategoryRadio);

                // Warning
                mainPanel.Children.Add(CreateIconTextBlock("\uE7BA", "The browser must be closed before importing bookmarks.", Microsoft.UI.Colors.Orange, fontSize: 12));

                dialog.IsPrimaryButtonEnabled = true;
            });
        });

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && folderTreeView != null)
        {
            // Build selected folders list
            var selectedFolders = folderTreeView.SelectedNodes
                .Where(n => n.Content is BrowserFolder)
                .Select(n => ((BrowserFolder)n.Content).FullPath)
                .ToList();

            // Build domain filters list
            var domainFilters = new List<string>();
            if (filterByDomainCheckBox?.IsChecked == true && domainListView != null)
            {
                domainFilters = domainListView.SelectedItems
                    .Cast<string>()
                    .ToList();
            }

            var options = new ImportOptions
            {
                IncludeFolders = includeFoldersCheckBox?.IsChecked == true,
                SkipDuplicates = skipDuplicatesCheckBox?.IsChecked == true,
                SelectedFolders = selectedFolders,
                DomainFilters = domainFilters
            };

            return (options, newCategoryRadio?.IsChecked == true, organizeFoldersCheckBox?.IsChecked == true);
        }

        return null;
    }

    /// <summary>
    /// Creates a TreeViewNode for a browser folder.
    /// </summary>
    private TreeViewNode CreateFolderTreeNode(BrowserFolder folder)
    {
        var node = new TreeViewNode
        {
            Content = folder,
            IsExpanded = false
        };

        foreach (var subFolder in folder.SubFolders)
        {
            node.Children.Add(CreateFolderTreeNode(subFolder));
        }

        return node;
    }

    /// <summary>
    /// Selects all folder nodes in a TreeView.
    /// </summary>
    private void SelectAllFolderNodes(TreeView treeView)
    {
        treeView.SelectedNodes.Clear();
        foreach (var rootNode in treeView.RootNodes)
        {
            SelectFolderNodeRecursive(treeView, rootNode);
        }
    }

    private void SelectFolderNodeRecursive(TreeView treeView, TreeViewNode node)
    {
        treeView.SelectedNodes.Add(node);
        foreach (var child in node.Children)
        {
            SelectFolderNodeRecursive(treeView, child);
        }
    }

    /// <summary>
    /// Shows a sync dialog to detect and import changes from browser bookmarks.
    /// </summary>
    public async Task SyncBookmarksAsync(CategoryItem category, TreeViewNode categoryNode)
    {
        if (!category.IsBookmarkImport || string.IsNullOrEmpty(category.SourceBookmarksPath))
        {
            await ShowErrorDialogAsync("Cannot Sync", 
                "This category is not a bookmark import or source information is missing.");
            return;
        }

        try
        {
            StatusText.Text = $"Checking for changes in {category.SourceBrowserName}...";

            var importer = new BookmarkImporterService();

            // Collect existing links from this category
            var existingLinks = new List<LinkItem>();
            CollectLinksFromNode(categoryNode, existingLinks);

            // Detect changes
            var syncResult = await importer.DetectChangesAsync(
                category.SourceBookmarksPath, 
                existingLinks);

            if (!syncResult.IsSuccess)
            {
                await ShowErrorDialogAsync("Sync Failed", syncResult.ErrorMessage ?? "Unknown error occurred");
                StatusText.Text = "Ready";
                return;
            }

            if (!syncResult.HasChanges)
            {
                await ShowSuccessDialogAsync("No Changes Detected", 
                    $"Your bookmarks are in sync with {category.SourceBrowserName}.\n\n" +
                    $"Browser bookmarks: {syncResult.TotalBrowserBookmarks}\n" +
                    $"MyMemories links: {syncResult.TotalExistingLinks}");
                StatusText.Text = "Ready";
                return;
            }

            // Show sync preview dialog
            var syncAction = await ShowSyncPreviewDialogAsync(category, syncResult);
            if (syncAction == null)
            {
                StatusText.Text = "Sync cancelled";
                return;
            }

            var (importNew, removeDeleted, updateModified) = syncAction.Value;

            int changesApplied = 0;

            // Import new bookmarks
            if (importNew && syncResult.NewBookmarks.Count > 0)
            {
                StatusText.Text = $"Importing {syncResult.NewBookmarks.Count} new bookmarks...";
                
                var categoryPath = _treeViewService!.GetCategoryPath(categoryNode);
                foreach (var bookmark in syncResult.NewBookmarks)
                {
                    if (await AddBookmarkLinkAsync(categoryNode, bookmark, categoryPath))
                        changesApplied++;
                }
            }

            // Remove deleted bookmarks
            if (removeDeleted && syncResult.DeletedBookmarks.Count > 0)
            {
                StatusText.Text = $"Removing {syncResult.DeletedBookmarks.Count} deleted bookmarks...";
                
                var deletedUrls = syncResult.DeletedBookmarks.Select(b => b.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
                RemoveLinksWithUrls(categoryNode, deletedUrls);
                changesApplied += syncResult.DeletedBookmarks.Count;
            }

            // Update modified bookmarks (titles)
            if (updateModified && syncResult.ModifiedBookmarks.Count > 0)
            {
                StatusText.Text = $"Updating {syncResult.ModifiedBookmarks.Count} modified bookmarks...";
                
                foreach (var (browserBookmark, existingLink) in syncResult.ModifiedBookmarks)
                {
                    existingLink.Title = browserBookmark.Name;
                    existingLink.ModifiedDate = DateTime.Now;
                    changesApplied++;
                }
            }

            // Save the category
            if (changesApplied > 0)
            {
                category.LastBookmarkImportDate = DateTime.Now;
                category.ModifiedDate = DateTime.Now;
                
                var rootNode = GetRootCategoryNode(categoryNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                // Refresh the details view
                if (LinksTreeView.SelectedNode == categoryNode)
                {
                    await _detailsViewService!.ShowCategoryDetailsAsync(category, categoryNode, 
                        async () => await RefreshBookmarksAsync(category, categoryNode),
                        async () => await SyncBookmarksAsync(category, categoryNode));
                }
            }

            StatusText.Text = $"Sync complete: {changesApplied} changes applied";

            await ShowSuccessDialogAsync("Sync Complete", 
                $"Successfully synchronized bookmarks with {category.SourceBrowserName}.\n\n" +
                $"Changes applied: {changesApplied}");
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.SyncBookmarksAsync", "Error syncing bookmarks", ex);
            await ShowErrorDialogAsync("Sync Error", $"An error occurred while syncing bookmarks:\n\n{ex.Message}");
            StatusText.Text = "Ready";
        }
    }

    /// <summary>
    /// Shows a dialog previewing sync changes and lets user select what to apply.
    /// </summary>
    private async Task<(bool ImportNew, bool RemoveDeleted, bool UpdateModified)?> ShowSyncPreviewDialogAsync(
        CategoryItem category, BookmarkSyncResult syncResult)
    {
        var mainPanel = new StackPanel { Spacing = 12, MinWidth = 450 };

        mainPanel.Children.Add(new TextBlock
        {
            Text = $"Changes detected in {category.SourceBrowserName}:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // Summary stats
        var statsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 8) };
        
        statsPanel.Children.Add(CreateSyncStatRowWithIcon("\uE774", $"Browser bookmarks: {syncResult.TotalBrowserBookmarks}")); // Globe
        statsPanel.Children.Add(CreateSyncStatRowWithIcon("\uE8F1", $"MyMemories links: {syncResult.TotalExistingLinks}")); // Library
        
        mainPanel.Children.Add(statsPanel);

        // Checkboxes for each type of change
        CheckBox? importNewCheckBox = null;
        CheckBox? removeDeletedCheckBox = null;
        CheckBox? updateModifiedCheckBox = null;

        if (syncResult.NewBookmarks.Count > 0)
        {
            importNewCheckBox = new CheckBox
            {
                Content = CreateIconTextPanel("\uE710", $"Import {syncResult.NewBookmarks.Count} new bookmark(s)"), // Add
                IsChecked = true
            };
            mainPanel.Children.Add(importNewCheckBox);

            // Show first few new bookmarks as preview
            if (syncResult.NewBookmarks.Count <= 5)
            {
                var previewPanel = new StackPanel { Margin = new Thickness(24, 0, 0, 0) };
                foreach (var bookmark in syncResult.NewBookmarks)
                {
                    previewPanel.Children.Add(new TextBlock
                    {
                        Text = $"• {bookmark.Name}",
                        FontSize = 12,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }
                mainPanel.Children.Add(previewPanel);
            }
        }

        if (syncResult.DeletedBookmarks.Count > 0)
        {
            removeDeletedCheckBox = new CheckBox
            {
                Content = CreateIconTextPanel("\uE738", $"Remove {syncResult.DeletedBookmarks.Count} bookmark(s) not in browser"), // Remove
                IsChecked = false // Default to not removing
            };
            mainPanel.Children.Add(removeDeletedCheckBox);
        }

        if (syncResult.ModifiedBookmarks.Count > 0)
        {
            updateModifiedCheckBox = new CheckBox
            {
                Content = CreateIconTextPanel("\uE70F", $"Update {syncResult.ModifiedBookmarks.Count} bookmark title(s)"), // Edit
                IsChecked = true
            };
            mainPanel.Children.Add(updateModifiedCheckBox);
        }

        var dialog = new ContentDialog
        {
            Title = "Sync Bookmarks",
            Content = new ScrollViewer { Content = mainPanel, MaxHeight = 500 },
            PrimaryButtonText = "Apply Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return (
                importNewCheckBox?.IsChecked == true,
                removeDeletedCheckBox?.IsChecked == true,
                updateModifiedCheckBox?.IsChecked == true
            );
        }

        return null;
    }

    /// <summary>
    /// Creates a row for sync statistics display with a FontIcon.
    /// </summary>
    private StackPanel CreateSyncStatRowWithIcon(string glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>
    /// Creates a row for sync statistics display.
    /// </summary>
    private StackPanel CreateSyncStatRow(string icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = icon, FontSize = 14 });
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>
    /// Collects all LinkItems from a node recursively.
    /// </summary>
    private void CollectLinksFromNode(TreeViewNode node, List<LinkItem> links)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                links.Add(link);
            }
            else if (child.Content is CategoryItem)
            {
                CollectLinksFromNode(child, links);
            }
        }
    }

    /// <summary>
    /// Removes links with URLs matching the given set.
    /// </summary>
    private void RemoveLinksWithUrls(TreeViewNode node, HashSet<string> urlsToRemove)
    {
        var nodesToRemove = node.Children
            .Where(n => n.Content is LinkItem link && urlsToRemove.Contains(link.Url))
            .ToList();

        foreach (var nodeToRemove in nodesToRemove)
        {
            node.Children.Remove(nodeToRemove);
        }

        // Recursively process subcategories
        foreach (var child in node.Children.Where(n => n.Content is CategoryItem))
        {
            RemoveLinksWithUrls(child, urlsToRemove);
        }
    }

    private async Task<BrowserInfo?> ShowBrowserSelectionDialogAsync(List<BrowserInfo> browsers)
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

    private async Task<TreeViewNode?> SelectTargetCategoryAsync(bool createNew, BrowserInfo? browserInfo = null)
    {
        if (createNew)
        {
            var baseCategoryName = "Imported Bookmarks";
            var categoryName = baseCategoryName;
            
            var sequenceNumber = 1;
            while (CategoryNameExists(categoryName))
            {
                sequenceNumber++;
                categoryName = $"{baseCategoryName} ({sequenceNumber})";
            }
            
            var categoryNode = new TreeViewNode
            {
                Content = new CategoryItem
                {
                    Name = categoryName,
                    Description = browserInfo != null 
                        ? $"Imported from {browserInfo.Name} on {DateTime.Now:yyyy-MM-dd HH:mm}"
                        : $"Imported bookmarks on {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Icon = "\U0001F516",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsBookmarkImport = browserInfo != null,
                    SourceBrowserType = browserInfo?.BrowserType,
                    SourceBrowserName = browserInfo?.Name,
                    SourceBookmarksPath = browserInfo?.BookmarksPath,
                    LastBookmarkImportDate = DateTime.Now,
                    ImportedBookmarkCount = 0,
                    IsBookmarkCategory = true
                }
            };

            _treeViewService!.InsertCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(categoryNode);

            return categoryNode;
        }
        else
        {
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

    private bool CategoryNameExists(string categoryName)
    {
        return LinksTreeView.RootNodes
            .Any(node => node.Content is CategoryItem cat && 
                        cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<TreeViewNode> GetOrCreateSubcategoryAsync(TreeViewNode parentCategory, string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return parentCategory;

        var existingSubcat = parentCategory.Children
            .FirstOrDefault(n => n.Content is CategoryItem cat && cat.Name == folderPath);

        if (existingSubcat != null)
            return existingSubcat;

        var parentCategoryItem = parentCategory.Content as CategoryItem;
        bool parentIsBookmarkCategory = parentCategoryItem?.IsBookmarkCategory ?? false;

        var subCategoryNode = new TreeViewNode
        {
            Content = new CategoryItem
            {
                Name = folderPath,
                Description = $"Bookmarks from {folderPath}",
                Icon = "\U0001F4D1",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                IsBookmarkCategory = parentIsBookmarkCategory
            }
        };

        _treeViewService!.InsertSubCategoryNode(parentCategory, subCategoryNode);
        
        return subCategoryNode;
    }

    private async Task<bool> AddBookmarkLinkAsync(TreeViewNode categoryNode, BookmarkItem bookmark, String categoryPath)
    {
        try
        {
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

    private async Task AddBookmarksToCategory(List<BookmarkItem> bookmarks, 
        TreeViewNode targetCategory, bool organizeByFolder)
    {
        var categoryPath = _treeViewService!.GetCategoryPath(targetCategory);
        var addedCount = 0;

        if (organizeByFolder)
        {
            var folderGroups = bookmarks.GroupBy(b => b.FolderPath);

            foreach (var group in folderGroups)
            {
                var folderPath = group.Key;
                var subCategory = await GetOrCreateSubcategoryAsync(targetCategory, folderPath);

                foreach (var bookmark in group)
                {
                    if (await AddBookmarkLinkAsync(subCategory, bookmark, categoryPath))
                        addedCount++;
                }
            }
        }
        else
        {
            foreach (var bookmark in bookmarks)
            {
                if (await AddBookmarkLinkAsync(targetCategory, bookmark, categoryPath))
                    addedCount++;
            }
        }

        var rootNode = GetRootCategoryNode(targetCategory);
        await _categoryService!.SaveCategoryAsync(rootNode);

        LogUtilities.LogInfo("MainWindow.AddBookmarksToCategory", 
            $"Added {addedCount} bookmarks to category '{categoryPath}'");
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
            BrowserType.Chrome => "\uE774",
            BrowserType.Edge => "\uE737",
            BrowserType.Brave => "\uE8A1",
            BrowserType.Vivaldi => "\uE773",
            BrowserType.Opera => "\uE8A5",
            BrowserType.Firefox => "\uE7E8",
            _ => "\uE774"
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
            bool wasOrganizedByFolders = categoryNode.Children
                .Any(n => n.Content is CategoryItem subCat && 
                         !string.IsNullOrEmpty(subCat.Description) && 
                         subCat.Description.StartsWith("Bookmarks from "));

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

            var importer = new BookmarkImporterService();
            var importOptions = new ImportOptions
            {
                IncludeFolders = true,
                SkipDuplicates = !replaceAll
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

            if (replaceAll)
            {
                if (wasOrganizedByFolders)
                {
                    var childrenToRemove = categoryNode.Children.ToList();
                    foreach (var child in childrenToRemove)
                    {
                        categoryNode.Children.Remove(child);
                    }
                }
                else
                {
                    var linksToRemove = categoryNode.Children
                        .Where(n => n.Content is LinkItem)
                        .ToList();

                    foreach (var linkNode in linksToRemove)
                    {
                        categoryNode.Children.Remove(linkNode);
                    }
                }
            }

            await AddBookmarksToCategory(importResult.Bookmarks, categoryNode, wasOrganizedByFolders);

            category.LastBookmarkImportDate = DateTime.Now;
            category.ImportedBookmarkCount = importResult.Bookmarks.Count;
            category.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(categoryNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            if (LinksTreeView.SelectedNode == categoryNode)
            {
                await _detailsViewService!.ShowCategoryDetailsAsync(category, categoryNode, 
                    async () => await RefreshBookmarksAsync(category, categoryNode),
                    null,
                    async () => await SyncBookmarksAsync(category, categoryNode));
                _detailsViewService.ShowCategoryHeader(_treeViewService!.GetCategoryPath(categoryNode), 
                    category.Description, category.Icon);
                HeaderViewerScroll.Visibility = Visibility.Visible;
            }

            StatusText.Text = $"Successfully refreshed {importResult.Bookmarks.Count} bookmarks";

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

    private async void MenuFile_ExportBookmarks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exporter = new BookmarkExporterService();
            var browsers = exporter.DetectInstalledBrowsers();

            if (!browsers.Any())
            {
                await ShowErrorDialogAsync("No Browsers Found",
                    "No supported browsers were detected on this system.\n\n" +
                    "Supported browsers: Chrome, Edge, Brave, Vivaldi, Opera");
                return;
            }

            var bookmarkCategories = GetBookmarkCategories();

            if (!bookmarkCategories.Any())
            {
                await ShowErrorDialogAsync("No Bookmark Categories",
                    "No bookmark categories were found.\n\n" +
                    "To export bookmarks, you need at least one category with URL links.\n" +
                    "Categories marked as 'URL Bookmarks Only' will be shown here.");
                return;
            }

            var exportResult = await ShowExportBookmarksDialogAsync(browsers, bookmarkCategories);
            if (exportResult == null)
                return;

            var (selectedBrowser, selectedNodes, folderName, enableSync) = exportResult.Value;

            // Collect existing links for sync comparison
            var existingLinks = new List<LinkItem>();
            foreach (var node in selectedNodes)
            {
                CollectLinksFromNode(node, existingLinks);
            }

            // Find the last export date from selected categories
            DateTime? lastExportDate = null;
            foreach (var node in selectedNodes)
            {
                if (node.Content is CategoryItem category && category.LastExportDate.HasValue)
                {
                    if (!lastExportDate.HasValue || category.LastExportDate.Value > lastExportDate.Value)
                    {
                        lastExportDate = category.LastExportDate;
                    }
                }
            }

            var bookmarks = exporter.CollectBookmarksFromNodes(selectedNodes);

            if (bookmarks.Count == 0 && !enableSync)
            {
                await ShowErrorDialogAsync("No Bookmarks to Export",
                    "No web URLs (http:// or https://) were found in the selected items.");
                return;
            }

            // If sync is enabled, detect changes from browser first
            BrowserSyncDetectionResult? syncDetection = null;
            if (enableSync)
            {
                StatusText.Text = $"Detecting changes from {selectedBrowser.Name}...";
                syncDetection = await exporter.DetectBrowserChangesAsync(
                    selectedBrowser.BookmarksPath, folderName, existingLinks, lastExportDate);

                if (!syncDetection.IsSuccess)
                {
                    await ShowErrorDialogAsync("Sync Detection Failed", syncDetection.ErrorMessage ?? "Unknown error");
                    StatusText.Text = "Ready";
                    return;
                }
            }

            // Build confirmation message
            var confirmContent = new StackPanel { Spacing = 8 };
            
            confirmContent.Children.Add(new TextBlock
            {
                Text = $"Target: {selectedBrowser.Name}",
                TextWrapping = TextWrapping.Wrap
            });

            confirmContent.Children.Add(new TextBlock
            {
                Text = $"Folder: '{folderName}' (in 'Other Bookmarks')",
                TextWrapping = TextWrapping.Wrap
            });

            if (bookmarks.Count > 0)
            {
                confirmContent.Children.Add(CreateSyncStatRowWithIcon("\uE898", $"Export: {bookmarks.Count} bookmark(s) to browser"));
            }

            if (enableSync && syncDetection != null && syncDetection.HasChanges)
            {
                confirmContent.Children.Add(new TextBlock
                {
                    Text = "\nChanges from browser to import:",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 4)
                });

                if (syncDetection.NewInBrowser.Count > 0)
                {
                    confirmContent.Children.Add(CreateSyncStatRowWithIcon("\uE710", $"New: {syncDetection.NewInBrowser.Count} bookmark(s)"));
                }
                if (syncDetection.ModifiedInBrowser.Count > 0)
                {
                    confirmContent.Children.Add(CreateSyncStatRowWithIcon("\uE70F", $"Modified: {syncDetection.ModifiedInBrowser.Count} bookmark(s)"));
                }
                if (syncDetection.DeletedFromBrowser.Count > 0)
                {
                    confirmContent.Children.Add(CreateSyncStatRowWithIcon("\uE738", $"Deleted: {syncDetection.DeletedFromBrowser.Count} bookmark(s)"));
                }
            }
            else if (enableSync)
            {
                confirmContent.Children.Add(new TextBlock
                {
                    Text = "\n? No changes to sync from browser",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            confirmContent.Children.Add(new TextBlock
            {
                Text = "?? If this folder already exists, it will be replaced.\nA backup will be created.",
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });

            var confirmDialog = new ContentDialog
            {
                Title = enableSync ? "Confirm Export/Sync" : "Confirm Export",
                Content = confirmContent,
                PrimaryButtonText = enableSync ? "Export/Sync" : "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var confirmResult = await confirmDialog.ShowAsync();
            if (confirmResult != ContentDialogResult.Primary)
                return;

            StatusText.Text = $"Exporting bookmarks to {selectedBrowser.Name}...";

            // Perform export
            var result = await exporter.ExportBookmarksAsync(
                selectedBrowser.BookmarksPath,
                bookmarks,
                folderName);

            if (!result.IsSuccess)
            {
                await ShowErrorDialogAsync("Export Failed", result.ErrorMessage ?? "Unknown error occurred");
                StatusText.Text = "Ready";
                return;
            }

            int syncedCount = 0;

            // Apply sync changes if enabled
            if (enableSync && syncDetection != null && syncDetection.HasChanges)
            {
                StatusText.Text = "Applying sync changes...";

                // Build a set of all existing URLs across all selected nodes for duplicate detection
                var allExistingUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var link in existingLinks)
                {
                    if (!string.IsNullOrEmpty(link.Url))
                    {
                        allExistingUrls.Add(NormalizeUrlForSync(link.Url));
                    }
                }

                // Import new bookmarks from browser
                foreach (var newBookmark in syncDetection.NewInBrowser)
                {
                    var normalizedNewUrl = NormalizeUrlForSync(newBookmark.Url);
                    
                    // Skip if already exists anywhere in the selected categories
                    if (allExistingUrls.Contains(normalizedNewUrl))
                    {
                        continue;
                    }

                    // Find the appropriate category to add to
                    var targetNode = FindCategoryForSync(selectedNodes, newBookmark.FolderPath);
                    if (targetNode != null)
                    {
                        var categoryPath = _treeViewService!.GetCategoryPath(targetNode);
                        
                        // Double-check for duplicates in the target node
                        var isDuplicate = false;
                        foreach (var child in targetNode.Children)
                        {
                            if (child.Content is LinkItem existingLink && 
                                string.Equals(NormalizeUrlForSync(existingLink.Url), normalizedNewUrl, StringComparison.OrdinalIgnoreCase))
                            {
                                isDuplicate = true;
                                break;
                            }
                        }

                        if (!isDuplicate)
                        {
                            var linkNode = new TreeViewNode
                            {
                                Content = new LinkItem
                                {
                                    Title = newBookmark.Name,
                                    Url = newBookmark.Url,
                                    Description = $"Synced from browser on {DateTime.Now:yyyy-MM-dd}",
                                    IsDirectory = false,
                                    CategoryPath = categoryPath,
                                    CreatedDate = newBookmark.DateAdded,
                                    ModifiedDate = DateTime.Now
                                }
                            };
                            targetNode.Children.Add(linkNode);
                            allExistingUrls.Add(normalizedNewUrl); // Track to avoid adding duplicates from same sync
                            syncedCount++;
                        }
                    }
                }

                // Update modified bookmarks
                foreach (var modifiedBookmark in syncDetection.ModifiedInBrowser)
                {
                    var normalizedModUrl = NormalizeUrlForSync(modifiedBookmark.Url);
                    var existingLink = existingLinks.FirstOrDefault(l => 
                        string.Equals(NormalizeUrlForSync(l.Url), normalizedModUrl, StringComparison.OrdinalIgnoreCase));
                    if (existingLink != null)
                    {
                        existingLink.Title = modifiedBookmark.Name;
                        existingLink.ModifiedDate = DateTime.Now;
                        syncedCount++;
                    }
                }

                // Note: We don't automatically delete bookmarks from MyMemories when deleted from browser
                // User should manually review deletions
            }

            // Update export metadata on categories
            var exportDate = DateTime.Now;
            foreach (var node in selectedNodes)
            {
                if (node.Content is CategoryItem category)
                {
                    category.LastExportDate = exportDate;
                    category.ExportFolderName = folderName;
                    category.ExportedToBrowserType = selectedBrowser.BrowserType;
                    category.ExportedToBookmarksPath = selectedBrowser.BookmarksPath;
                    category.ModifiedDate = exportDate;
                }
            }

            // Save categories
            var savedRoots = new HashSet<TreeViewNode>();
            foreach (var node in selectedNodes)
            {
                var rootNode = GetRootCategoryNode(node);
                if (rootNode != null && !savedRoots.Contains(rootNode))
                {
                    await _categoryService!.SaveCategoryAsync(rootNode);
                    savedRoots.Add(rootNode);
                }
            }

            StatusText.Text = $"Successfully exported {result.ExportedCount} bookmarks" + 
                             (syncedCount > 0 ? $", synced {syncedCount} changes" : "");

            // Build success message
            var successMessage = $"Exported {result.ExportedCount} bookmark(s) to {selectedBrowser.Name}.\n" +
                               $"Folder: '{folderName}' (in 'Other Bookmarks')";

            if (syncedCount > 0)
            {
                successMessage += $"\n\nSynced {syncedCount} change(s) from browser.";
            }

            if (!string.IsNullOrEmpty(result.BackupPath))
            {
                successMessage += $"\n\nBackup created:\n{result.BackupPath}";
            }

            await ShowSuccessDialogAsync(enableSync ? "Export/Sync Complete" : "Export Complete", successMessage);
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("MainWindow.MenuFile_ExportBookmarks_Click", "Error exporting bookmarks", ex);
            await ShowErrorDialogAsync("Export Error", $"An error occurred while exporting bookmarks:\n\n{ex.Message}");
            StatusText.Text = "Ready";
        }
    }

    /// <summary>
    /// Finds the appropriate category node to add a synced bookmark to.
    /// </summary>
    private TreeViewNode? FindCategoryForSync(List<TreeViewNode> selectedNodes, string folderPath)
    {
        // If folder path is empty, use the first selected category
        if (string.IsNullOrEmpty(folderPath))
        {
            return selectedNodes.FirstOrDefault(n => n.Content is CategoryItem);
        }

        // Try to find a matching subcategory
        foreach (var node in selectedNodes)
        {
            if (node.Content is CategoryItem category)
            {
                // Check if folder path matches category name
                if (string.Equals(category.Name, folderPath, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                // Search in children
                var found = FindCategoryByPath(node, folderPath);
                if (found != null)
                    return found;
            }
        }

        // Fall back to first category
        return selectedNodes.FirstOrDefault(n => n.Content is CategoryItem);
    }

    private TreeViewNode? FindCategoryByPath(TreeViewNode node, string folderPath)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem category)
            {
                if (folderPath.Contains(category.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                var found = FindCategoryByPath(child, folderPath);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Normalizes a URL for sync comparison.
    /// </summary>
    private static string NormalizeUrlForSync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            var uri = new Uri(url);
            var normalized = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            if (!string.IsNullOrEmpty(uri.Query))
                normalized += uri.Query;
            return normalized.TrimEnd('/').ToLowerInvariant();
        }
        catch
        {
            return url.TrimEnd('/').ToLowerInvariant();
        }
    }

    private async Task<(BrowserInfo Browser, List<TreeViewNode> SelectedNodes, string FolderName, bool EnableSync)?> ShowExportBookmarksDialogAsync(
        List<BrowserInfo> browsers,
        List<TreeViewNode> bookmarkCategories)
    {
        var mainPanel = new StackPanel { Spacing = 16, MinWidth = 500 };

        mainPanel.Children.Add(new TextBlock
        {
            Text = "Select Target Browser:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var browserComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        foreach (var browser in browsers)
        {
            browserComboBox.Items.Add(new ComboBoxItem
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = GetBrowserGlyph(browser.BrowserType),
                            FontSize = 16
                        },
                        new TextBlock { Text = browser.Name, VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Tag = browser
            });
        }
        browserComboBox.SelectedIndex = 0;

        mainPanel.Children.Add(browserComboBox);

        mainPanel.Children.Add(new TextBlock
        {
            Text = "Export Folder Name:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var folderNameTextBox = new TextBox
        {
            Text = "MyMemories",
            PlaceholderText = "Enter folder name for exported bookmarks",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        mainPanel.Children.Add(folderNameTextBox);

        // === Sync Option ===
        mainPanel.Children.Add(CreateSectionHeader("\uE895", "Sync Options:"));

        var syncCheckBox = new CheckBox
        {
            Content = CreateIconTextPanel("\uE72C", "Sync changes from browser"),
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 4)
        };
        mainPanel.Children.Add(syncCheckBox);

        var syncDescription = new TextBlock
        {
            Text = "When enabled, bookmarks added or modified in the browser's export folder will be imported back to MyMemories. Uses dates to detect changes and skips duplicates.",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 0, 8)
        };
        mainPanel.Children.Add(syncDescription);

        mainPanel.Children.Add(new TextBlock
        {
            Text = "Select Categories/Links to Export:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        mainPanel.Children.Add(new TextBlock
        {
            Text = "Only web URLs (http/https) will be exported. Files and folders are skipped.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var bookmarkTree = new TreeView
        {
            SelectionMode = TreeViewSelectionMode.Multiple,
            MinHeight = 200,
            MaxHeight = 300
        };

        foreach (var categoryNode in bookmarkCategories)
        {
            var clonedNode = CloneNodeForExport(categoryNode);
            clonedNode.IsExpanded = true;
            bookmarkTree.RootNodes.Add(clonedNode);
        }

        SelectRootNodesOnly(bookmarkTree);

        mainPanel.Children.Add(new ScrollViewer
        {
            Content = bookmarkTree,
            MaxHeight = 300,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var selectAllButton = new Button { Content = "Select All" };
        selectAllButton.Click += (s, e) => SelectAllNodes(bookmarkTree);
        buttonPanel.Children.Add(selectAllButton);

        var deselectAllButton = new Button { Content = "Deselect All" };
        deselectAllButton.Click += (s, e) => DeselectAllNodes(bookmarkTree);
        buttonPanel.Children.Add(deselectAllButton);

        mainPanel.Children.Add(buttonPanel);

        mainPanel.Children.Add(new TextBlock
        {
            Text = "?? The browser must be closed before exporting/syncing bookmarks.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var dialog = new ContentDialog
        {
            Title = "Export/Sync Bookmarks to Browser",
            Content = new ScrollViewer
            {
                Content = mainPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = "Export/Sync",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var selectedBrowser = (browserComboBox.SelectedItem as ComboBoxItem)?.Tag as BrowserInfo;
            var folderName = folderNameTextBox.Text.Trim();

            if (selectedBrowser == null || string.IsNullOrWhiteSpace(folderName))
                return null;

            var selectedNodes = bookmarkTree.SelectedNodes
                .Cast<TreeViewNode>()
                .ToList();

            if (selectedNodes.Count == 0)
            {
                await ShowErrorDialogAsync("No Selection", "Please select at least one category or link to export.");
                return null;
            }

            return (selectedBrowser, selectedNodes, folderName, syncCheckBox.IsChecked == true);
        }

        return null;
    }

    private List<TreeViewNode> GetBookmarkCategories()
    {
        var categories = new List<TreeViewNode>();

        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem category)
            {
                if (category.IsBookmarkCategory || HasWebUrlLinks(rootNode))
                {
                    categories.Add(rootNode);
                }
            }
        }

        return categories;
    }

    private bool HasWebUrlLinks(TreeViewNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                if (IsWebUrl(link.Url))
                    return true;

                if (HasWebUrlLinks(child))
                    return true;
            }
            else if (child.Content is CategoryItem)
            {
                if (HasWebUrlLinks(child))
                    return true;
            }
        }

        return false;
    }

    private static bool IsWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private TreeViewNode CloneNodeForExport(TreeViewNode source)
    {
        var clone = new TreeViewNode
        {
            Content = source.Content,
            IsExpanded = false
        };

        foreach (var child in source.Children)
        {
            if (child.Content is CategoryItem)
            {
                clone.Children.Add(CloneNodeForExport(child));
            }
            else if (child.Content is LinkItem link)
            {
                if (IsWebUrl(link.Url))
                {
                    var linkClone = new TreeViewNode
                    {
                        Content = link,
                        IsExpanded = false
                    };

                    foreach (var subChild in child.Children)
                    {
                        if (subChild.Content is LinkItem subLink && IsWebUrl(subLink.Url))
                        {
                            linkClone.Children.Add(new TreeViewNode { Content = subLink });
                        }
                    }

                    clone.Children.Add(linkClone);
                }
            }
        }

        return clone;
    }

    private void SelectRootNodesOnly(TreeView treeView)
    {
        treeView.SelectedNodes.Clear();
        foreach (var rootNode in treeView.RootNodes)
        {
            treeView.SelectedNodes.Add(rootNode);
        }
    }

    private void SelectAllNodes(TreeView treeView)
    {
        treeView.SelectedNodes.Clear();
        foreach (var rootNode in treeView.RootNodes)
        {
            SelectNodeRecursive(treeView, rootNode);
        }
    }

    private void SelectNodeRecursive(TreeView treeView, TreeViewNode node)
    {
        treeView.SelectedNodes.Add(node);
        foreach (var child in node.Children)
        {
            SelectNodeRecursive(treeView, child);
        }
    }

    private void DeselectAllNodes(TreeView treeView)
    {
        treeView.SelectedNodes.Clear();
    }

    /// <summary>
    /// Creates a section header with an icon and text.
    /// </summary>
    private StackPanel CreateSectionHeader(string glyph, string text)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 4)
        };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        panel.Children.Add(new TextBlock 
        { 
            Text = text, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    /// <summary>
    /// Creates a text block with an icon prefix.
    /// </summary>
    private StackPanel CreateIconTextBlock(string glyph, string text, Windows.UI.Color? color = null, int fontSize = 14)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        
        var icon = new FontIcon { Glyph = glyph, FontSize = fontSize };
        if (color.HasValue)
        {
            icon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color.Value);
        }
        panel.Children.Add(icon);
        
        var textBlock = new TextBlock 
        { 
            Text = text, 
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = fontSize
        };
        if (color.HasValue)
        {
            textBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color.Value);
        }
        panel.Children.Add(textBlock);
        
        return panel;
    }

    /// <summary>
    /// Creates a panel with an icon and text for use as checkbox content.
    /// </summary>
    private StackPanel CreateIconTextPanel(string glyph, string text)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
}
