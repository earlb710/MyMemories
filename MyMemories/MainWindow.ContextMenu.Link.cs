using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Link context menu event handlers - Basic operations (Add, Edit, Copy, Move, Remove, etc.).
/// </summary>
public sealed partial class MainWindow
{
    // ========================================
    // LINK MENU EVENT HANDLERS - BASIC OPS
    // ========================================

    private async void LinkMenu_AddTagItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tagName)
            return;

        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (!link.Tags.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase)))
        {
            link.Tags.Add(tagName);
            link.ModifiedDate = DateTime.Now;
            link.NotifyTagsChanged();

            var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
            _contextMenuNode = updatedNode;

            var rootNode = GetRootCategoryNode(updatedNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            StatusText.Text = $"Added tag '{tagName}' to {itemType} '{link.Title}'";
        }
    }

    private async void LinkMenu_Move_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            await MoveLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            if (link.IsCatalogEntry)
            {
                await ShowErrorDialogAsync(
                    "Cannot Edit Catalog Entry",
                    "Catalog entries are read-only and cannot be edited. Use 'Refresh Catalog' to update them.");
                return;
            }

            await EditLinkAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.IsCatalogEntry)
        {
            await ShowErrorDialogAsync(
                "Cannot Copy Catalog Entry",
                "Catalog entries are read-only and cannot be copied.");
            return;
        }

        var parentNode = FindParentNode(_contextMenuNode);
        if (parentNode == null)
        {
            StatusText.Text = "Could not find parent category";
            return;
        }

        // OPTIMIZED: Uses HashSet-based unique title generation
        var newTitle = GenerateUniqueTitle(link.Title, parentNode);

        var copiedLink = new LinkItem
        {
            Title = newTitle,
            Url = link.Url,
            Description = link.Description,
            Keywords = link.Keywords,
            IsDirectory = link.IsDirectory,
            CategoryPath = link.CategoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            FolderType = link.FolderType,
            FileFilters = link.FileFilters,
            UrlStatus = link.UrlStatus,
            UrlLastChecked = link.UrlLastChecked,
            UrlStatusMessage = link.UrlStatusMessage
        };

        var copiedNode = new TreeViewNode { Content = copiedLink };

        var insertIndex = parentNode.Children.IndexOf(_contextMenuNode) + 1;
        if (insertIndex > 0 && insertIndex <= parentNode.Children.Count)
        {
            parentNode.Children.Insert(insertIndex, copiedNode);
        }
        else
        {
            parentNode.Children.Add(copiedNode);
        }

        var rootNode = GetRootCategoryNode(parentNode);
        await _categoryService!.SaveCategoryAsync(rootNode);

        StatusText.Text = $"Copied link as '{newTitle}'";

        LinksTreeView.SelectedNode = copiedNode;
        _contextMenuNode = copiedNode;

        await EditLinkAsync(copiedLink, copiedNode);
    }

    private async void LinkMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        
        if (!isZipFile)
        {
            await ShowErrorDialogAsync(
                "Not a Zip File",
                "Password protection can only be changed for zip archive files.");
            return;
        }

        await ShowChangeZipPasswordDialogAsync(link, _contextMenuNode);
    }

    private async void LinkMenu_BackupZip_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        
        if (!isZipFile)
        {
            StatusText.Text = "Backup is only available for zip archive files.";
            return;
        }

        var dialog = new Dialogs.BackupDirectoryDialog(Content.XamlRoot, _folderPickerService!);
        var result = await dialog.ShowAsync(link.Title, link.BackupDirectories);

        if (result != null)
        {
            link.BackupDirectories = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var count = result.Count;
            if (count > 0)
            {
                StatusText.Text = $"Configured {count} backup directory(s) for '{link.Title}'";
                
                if (File.Exists(link.Url))
                {
                    bool shouldBackup = await ShowConfirmAsync(
                        "Backup Now?",
                        $"Do you want to backup '{link.Title}' to the configured directories now?",
                        "Backup Now",
                        "Later");

                    if (shouldBackup)
                    {
                        StatusText.Text = $"Backing up '{link.Title}'...";
                        await BackupZipFileAsync(link.Url, link.BackupDirectories, link.Title);
                    }
                }
            }
            else
            {
                StatusText.Text = $"Removed backup directories for '{link.Title}'";
            }
        }
    }

    private async void LinkMenu_ZipFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            bool isZipArchive = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            
            if (isZipArchive)
            {
                await ShowErrorDialogAsync(
                    "Already a Zip Archive",
                    "This is already a zip archive. You cannot zip a zip file.\n\nUse 'Explore Here' to open the zip file location if needed.");
                return;
            }

            if (!link.IsDirectory)
            {
                await ShowErrorDialogAsync(
                    "Cannot Zip File",
                    "Only folders can be zipped. This is a file link.");
                return;
            }

            if (link.IsCatalogEntry)
            {
                await ShowErrorDialogAsync(
                    "Cannot Zip Catalog Entry",
                    "Catalog entries cannot be zipped directly. Please zip the parent folder instead.");
                return;
            }

            await ZipFolderAsync(link, _contextMenuNode);
        }
    }

    private async void LinkMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;
            
        if (link.IsCatalogEntry)
        {
            await ShowErrorDialogAsync(
                "Cannot Remove Catalog Entry",
                "Catalog entries cannot be removed individually. Use 'Refresh Catalog' to update them.");
            return;
        }

        // Archive the link instead of permanently deleting
        await ArchiveLinkAsync(_contextMenuNode);
    }

    private async void LinkMenu_AddSubLink_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem parentLink)
            return;

        if (parentLink.IsCatalogEntry || parentLink.IsDirectory)
        {
            await ShowErrorDialogAsync(
                "Cannot Add Sub-Link",
                parentLink.IsCatalogEntry 
                    ? "Catalog entries cannot have sub-links." 
                    : "Directory links cannot have sub-links. Use 'Create Catalog' instead.");
            return;
        }

        var result = await _linkDialog!.ShowEditAsync(new LinkItem
        {
            Title = string.Empty,
            Url = string.Empty,
            Description = string.Empty,
            Keywords = string.Empty,
            CategoryPath = parentLink.CategoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        });

        if (result != null)
        {
            var subLinkItem = new LinkItem
            {
                Title = result.Title,
                Url = result.Url,
                Description = result.Description,
                Keywords = result.Keywords,
                IsDirectory = result.IsDirectory,
                CategoryPath = parentLink.CategoryPath,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                FolderType = result.FolderType,
                FileFilters = result.FileFilters
            };

            var subLinkNode = new TreeViewNode { Content = subLinkItem };
            _contextMenuNode.Children.Add(subLinkNode);
            _contextMenuNode.IsExpanded = true;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkChangeAsync(
                    rootCategory.Name,
                    "added sub-link to",
                    parentLink.Title,
                    $"Sub-link: {result.Title}");
            }

            StatusText.Text = $"Added sub-link '{result.Title}' to '{parentLink.Title}'";
        }
    }

    private async void LinkMenu_ExploreHere_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is LinkItem link)
        {
            // Extract URL if it's a zip file link
            string targetPath = link.Url;
            if (link.IsDirectory && targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Get the parent directory of the zip file
                var zipParentDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(zipParentDir) && Directory.Exists(zipParentDir))
                {
                    targetPath = zipParentDir;
                }
            }

            try
            {
                var uri = new Uri(targetPath);
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error Opening Location", $"Could not open the location:\n\n{ex.Message}");
            }
        }
    }

    private async void LinkMenu_RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (link.Tags.Count == 0)
        {
            StatusText.Text = "This item has no tags to remove";
            return;
        }

        var tagService = TagManagementService.Instance;
        if (tagService == null)
            return;

        var tagsInfo = tagService.GetTagsInfo(link.Tags);
        if (tagsInfo.Count == 0)
        {
            StatusText.Text = "No valid tags found";
            return;
        }

        var tagComboBox = new ComboBox
        {
            PlaceholderText = "Select tag to remove",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        foreach (var (name, _) in tagsInfo)
        {
            tagComboBox.Items.Add(name);
        }
        tagComboBox.SelectedIndex = 0;

        var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
        var dialog = new ContentDialog
        {
            Title = "Remove Tag",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = $"Select tag to remove from {itemType} '{link.Title}':" },
                    tagComboBox
                }
            },
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && tagComboBox.SelectedItem is string selectedTagName)
        {
            var tagToRemove = link.Tags.FirstOrDefault(t => 
                string.Equals(t, selectedTagName, StringComparison.OrdinalIgnoreCase));
            
            if (tagToRemove != null)
            {
                link.Tags.Remove(tagToRemove);
                link.ModifiedDate = DateTime.Now;
                link.NotifyTagsChanged();

                var updatedNode = _treeViewService!.RefreshLinkNode(_contextMenuNode, link);
                _contextMenuNode = updatedNode;

                var rootNode = GetRootCategoryNode(updatedNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                StatusText.Text = $"Removed tag '{selectedTagName}' from {itemType} '{link.Title}'";
            }
        }
    }

    private async void LinkMenu_SortCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (!link.IsDirectory)
        {
            StatusText.Text = "Sort is only available for directories";
            return;
        }

        await ShowSortDialogAsync(_contextMenuNode, link.CatalogSortOrder, isCategory: false);
    }

    private async void LinkMenu_RatingTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string templateName)
            return;

        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        // Store old ratings for archiving
        var oldRatings = new List<RatingValue>(link.Ratings);

        var originalTemplate = _ratingService.CurrentTemplateName;
        _ratingService.SwitchTemplate(templateName);

        // Get archived ratings for this item
        var archivedRatings = GetArchivedRatingsForItem(link.Title);

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(link.Title, link.Ratings, archivedRatings);

        _ratingService.SwitchTemplate(originalTemplate);

        if (result != null)
        {
            // Archive changed ratings
            await ArchiveChangedRatingsAsync(link.Title, oldRatings, result);
            
            link.Ratings = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            var displayName = string.IsNullOrEmpty(templateName) ? "Default" : templateName;
            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for {itemType} '{link.Title}' using template '{displayName}'"
                : $"Removed all ratings from {itemType} '{link.Title}'";
            
            // Refresh the details view if this node is currently selected
            if (LinksTreeView.SelectedNode == _contextMenuNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    _contextMenuNode,
                    async () => await _catalogService!.CreateCatalogAsync(link, _contextMenuNode),
                    async () => await _catalogService!.RefreshCatalogAsync(link, _contextMenuNode)
                );
                
                // Also refresh the header to show updated ratings
                _detailsViewService.ShowLinkHeader(
                    link.Title,
                    link.Description,
                    link.GetIcon(),
                    showLinkBadge: false,
                    fileSize: null,
                    createdDate: link.CreatedDate,
                    modifiedDate: link.ModifiedDate,
                    linkItem: link);
            }
        }
    }

    private async void LinkMenu_Ratings_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode?.Content is not LinkItem link)
            return;

        if (_ratingService == null)
        {
            StatusText.Text = "Rating service not initialized";
            return;
        }

        // Store old ratings for archiving
        var oldRatings = new List<RatingValue>(link.Ratings);

        // Get archived ratings for this item
        var archivedRatings = GetArchivedRatingsForItem(link.Title);

        var dialog = new Dialogs.RatingAssignmentDialog(Content.XamlRoot, _ratingService);
        var result = await dialog.ShowAsync(link.Title, link.Ratings, archivedRatings);

        if (result != null)
        {
            // Archive changed ratings
            await ArchiveChangedRatingsAsync(link.Title, oldRatings, result);
            
            link.Ratings = result;
            link.ModifiedDate = DateTime.Now;

            var rootNode = GetRootCategoryNode(_contextMenuNode);
            await _categoryService!.SaveCategoryAsync(rootNode);

            var itemType = link.IsCatalogEntry ? "catalog entry" : "link";
            StatusText.Text = result.Count > 0 
                ? $"Saved {result.Count} rating(s) for {itemType} '{link.Title}'"
                : $"Removed all ratings from {itemType} '{link.Title}'";
            
            // Refresh the details view if this node is currently selected
            if (LinksTreeView.SelectedNode == _contextMenuNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    _contextMenuNode,
                    async () => await _catalogService!.CreateCatalogAsync(link, _contextMenuNode),
                    async () => await _catalogService!.RefreshCatalogAsync(link, _contextMenuNode)
                );
                
                // Also refresh the header to show updated ratings
                _detailsViewService.ShowLinkHeader(
                    link.Title,
                    link.Description,
                    link.GetIcon(),
                    showLinkBadge: false,
                    fileSize: null,
                    createdDate: link.CreatedDate,
                    modifiedDate: link.ModifiedDate,
                    linkItem: link);
            }
        }
    }
    
    /// <summary>
    /// Archives ratings that have changed.
    /// </summary>
    private async Task ArchiveChangedRatingsAsync(string parentName, List<RatingValue> oldRatings, List<RatingValue> newRatings)
    {
        foreach (var oldRating in oldRatings)
        {
            // Check if this rating was changed or removed
            var newRating = newRatings.FirstOrDefault(r => r.Rating == oldRating.Rating);
            
            if (newRating == null || newRating.Score != oldRating.Score || newRating.Reason != oldRating.Reason)
            {
                // Rating was changed or removed - archive the old value
                await ArchiveRatingChangeAsync(parentName, oldRating.RatingName, oldRating);
            }
        }
    }
}
