using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Handles the request to update a URL to its redirect target.
    /// The URL has already been updated in the LinkItem by the caller.
    /// This handler saves the changes and refreshes the UI.
    /// </summary>
    private async void OnUpdateUrlFromRedirect(LinkItem linkItem)
    {
        if (linkItem == null)
        {
            return;
        }

        // Find the node for this link
        TreeViewNode? linkNode = FindNodeForLinkItem(linkItem);
        if (linkNode == null)
        {
            StatusText.Text = "Error: Could not find link in tree";
            return;
        }

        // The URL was already updated by the dialog - just refresh and save
        
        // Re-check the new URL to get fresh status
        if (_urlStateCheckerService != null)
        {
            var checkResult = await _urlStateCheckerService.CheckUrlWithRedirectAsync(linkItem.Url);
            linkItem.UrlStatus = checkResult.Status;
            linkItem.UrlStatusMessage = checkResult.Message;
            linkItem.UrlLastChecked = DateTime.Now;
            
            // If the new URL also redirects, store that
            if (checkResult.RedirectDetected && !string.IsNullOrEmpty(checkResult.RedirectUrl))
            {
                linkItem.RedirectUrl = checkResult.RedirectUrl;
            }
        }

        // Refresh the tree node
        var newNode = _treeViewService!.RefreshLinkNode(linkNode, linkItem);
        if (_contextMenuNode == linkNode)
        {
            _contextMenuNode = newNode;
        }

        // Save the category
        var rootNode = GetRootCategoryNode(newNode);
        if (rootNode != null)
        {
            await _categoryService!.SaveCategoryAsync(rootNode);

            // Audit log the URL update
            if (rootNode.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkChangeAsync(
                    rootCategory.Name,
                    "URL updated from redirect",
                    linkItem.Title,
                    $"New URL: {linkItem.Url}");
            }
        }

        StatusText.Text = $"Updated URL for '{linkItem.Title}' to redirect target";

        // Refresh the header to remove the redirect button (since we just updated)
        _detailsViewService!.ShowLinkHeader(
            linkItem.Title,
            linkItem.Description,
            linkItem.GetIcon(),
            linkItem.IsDirectory && linkItem.FolderType == FolderLinkType.LinkOnly,
            linkItem.FileSize,
            linkItem.CreatedDate,
            linkItem.ModifiedDate,
            linkItem);
    }

    /// <summary>
    /// Finds the TreeViewNode for a given LinkItem by searching the tree.
    /// </summary>
    private TreeViewNode? FindNodeForLinkItem(LinkItem linkItem)
    {
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            var found = FindNodeForLinkItemRecursive(rootNode, linkItem);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private TreeViewNode? FindNodeForLinkItemRecursive(TreeViewNode node, LinkItem linkItem)
    {
        if (node.Content == linkItem)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeForLinkItemRecursive(child, linkItem);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private async void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var categories = LinksTreeView.RootNodes
            .Where(n => n.Content is CategoryItem)
            .Select(n => new CategoryNode
            {
                Name = ((CategoryItem)n.Content).Name,
                Node = n
            })
            .ToList();

        if (categories.Count == 0)
        {
            StatusText.Text = "Please create a category first";
            return;
        }

        var selectedCategory = _treeViewService!.GetParentCategoryNode(LinksTreeView.SelectedNode) ?? _lastUsedCategory;
        var selectedCategoryNode = selectedCategory != null
            ? new CategoryNode { Name = ((CategoryItem)selectedCategory.Content).Name, Node = selectedCategory }
            : null;

        var result = await _linkDialog!.ShowAddAsync(categories, selectedCategoryNode);

        if (result?.CategoryNode != null)
        {
            var categoryPath = _treeViewService!.GetCategoryPath(result.CategoryNode);
            
            var linkNode = new TreeViewNode
            {
                Content = new LinkItem
                {
                    Title = result.Title,
                    Url = result.Url,
                    Description = result.Description,
                    Keywords = result.Keywords,
                    IsDirectory = result.IsDirectory,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    FolderType = result.FolderType,
                    FileFilters = result.FileFilters
                }
            };

            result.CategoryNode.Children.Add(linkNode);
            result.CategoryNode.IsExpanded = true;
            _lastUsedCategory = result.CategoryNode;

            // Update parent categories' ModifiedDate and save
            await UpdateParentCategoriesAndSaveAsync(result.CategoryNode);

            // Audit log the link addition
            var rootNode = GetRootCategoryNode(result.CategoryNode);
            if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkChangeAsync(
                    rootCategory.Name,
                    "added",
                    result.Title,
                    result.Url);
            }

            StatusText.Text = $"Added link '{result.Title}' to '{categoryPath}'";
        }
    }

    private async Task EditLinkAsync(LinkItem link, TreeViewNode node)
    {
        // Store original values for audit logging
        var originalTitle = link.Title;
        var originalUrl = link.Url;
        var originalDescription = link.Description;
        
        var editResult = await _linkDialog!.ShowEditAsync(link);
        
        if (editResult != null)
        {
            link.Title = editResult.Title;
            link.Url = editResult.Url;
            link.Description = editResult.Description;
            link.Keywords = editResult.Keywords;
            link.IsDirectory = editResult.IsDirectory;
            link.CategoryPath = _treeViewService!.GetCategoryPath(node.Parent);
            link.ModifiedDate = DateTime.Now;
            link.FolderType = editResult.FolderType;
            link.FileFilters = editResult.FileFilters;

            var newNode = _treeViewService!.RefreshLinkNode(node, link);

            if (_contextMenuNode == node)
            {
                _contextMenuNode = newNode;
            }

            var parentCategory = newNode.Parent;
            if (parentCategory != null)
            {
                // Update parent categories' ModifiedDate and save
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
                
                // Audit log the link edit
                var rootNode = GetRootCategoryNode(parentCategory);
                if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
                {
                    // Build change details
                    var changes = new System.Text.StringBuilder();
                    if (originalTitle != editResult.Title)
                        changes.Append($"Title: '{originalTitle}' ? '{editResult.Title}'; ");
                    if (originalUrl != editResult.Url)
                        changes.Append($"URL changed; ");
                    if (originalDescription != editResult.Description)
                        changes.Append($"Description changed; ");
                    
                    var changeDetails = changes.Length > 0 ? changes.ToString().TrimEnd(' ', ';') : "Properties updated";
                    
                    await _configService!.AuditLogService!.LogLinkChangeAsync(
                        rootCategory.Name,
                        "edited",
                        editResult.Title,
                        changeDetails);
                }
            }

            StatusText.Text = $"Updated link: {editResult.Title}";

            if (LinksTreeView.SelectedNode == newNode)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    newNode,
                    async () => await _catalogService!.CreateCatalogAsync(link, newNode),
                    async () => await _catalogService!.RefreshCatalogAsync(link, newNode)
                );
            }
        }
    }

    private async Task MoveLinkAsync(LinkItem link, TreeViewNode node)
    {
        if (node.Parent == null || node.Parent.Content is not CategoryItem)
        {
            StatusText.Text = "Cannot move link: Invalid parent category";
            return;
        }

        var currentCategoryNode = node.Parent;
        var allCategories = GetAllCategoriesFlat();
        
        var result = await _linkDialog!.ShowMoveLinkAsync(allCategories, currentCategoryNode, link.Title);

        if (result?.TargetCategoryNode != null)
        {
            var targetCategoryNode = result.TargetCategoryNode;
            var sourceCategoryPath = _treeViewService!.GetCategoryPath(currentCategoryNode);
            var targetCategoryPath = _treeViewService!.GetCategoryPath(targetCategoryNode);

            link.CategoryPath = targetCategoryPath;
            currentCategoryNode.Children.Remove(node);
            targetCategoryNode.Children.Add(node);
            targetCategoryNode.IsExpanded = true;

            // Update ModifiedDate for both source and target category hierarchies
            UpdateParentCategoriesModifiedDate(currentCategoryNode);
            UpdateParentCategoriesModifiedDate(targetCategoryNode);

            var sourceRootNode = GetRootCategoryNode(currentCategoryNode);
            var targetRootNode = GetRootCategoryNode(targetCategoryNode);

            await _categoryService!.SaveCategoryAsync(sourceRootNode);
            
            if (sourceRootNode != targetRootNode)
            {
                await _categoryService!.SaveCategoryAsync(targetRootNode);
            }

            // Log the move operation to audit log if enabled
            if (sourceRootNode.Content is CategoryItem sourceRootCategory && sourceRootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkMovedAsync(
                    sourceRootCategory.Name, 
                    link.Title, 
                    sourceCategoryPath, 
                    targetCategoryPath);
            }
            
            // If moving to a different root category with audit logging enabled, log there too
            if (sourceRootNode != targetRootNode && 
                targetRootNode.Content is CategoryItem targetRootCategory && 
                targetRootCategory.IsAuditLoggingEnabled)
            {
                await _configService!.AuditLogService!.LogLinkMovedAsync(
                    targetRootCategory.Name, 
                    link.Title, 
                    sourceCategoryPath, 
                    targetCategoryPath);
            }

            StatusText.Text = $"Moved link '{link.Title}' to '{targetCategoryPath}'";

            if (LinksTreeView.SelectedNode == node)
            {
                await _detailsViewService!.ShowLinkDetailsAsync(
                    link,
                    node,
                    async () => await _catalogService!.CreateCatalogAsync(link, node),
                    async () => await _catalogService!.RefreshCatalogAsync(link, node)
                );
            }
        }
    }

    private async Task DeleteLinkAsync(LinkItem link, TreeViewNode node)
    {
        // Check if this is a zip file
        bool isZipFile = link.IsDirectory && link.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && 
                        System.IO.File.Exists(link.Url);

        ContentDialog confirmDialog;

        if (isZipFile)
        {
            // Create dialog with checkbox for zip files
            var stackPanel = new StackPanel { Spacing = 12 };
            
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Remove link '{link.Title}'?",
                TextWrapping = TextWrapping.Wrap
            });

            var deleteFileCheckBox = new CheckBox
            {
                Content = "Also delete the zip file from disk",
                IsChecked = true,
                Margin = new Thickness(0, 8, 0, 0)
            };
            stackPanel.Children.Add(deleteFileCheckBox);

            // Show file path info
            var filePathInfo = new TextBlock
            {
                Text = $"?? {link.Url}",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stackPanel.Children.Add(filePathInfo);

            // Show file size if available
            if (link.FileSize.HasValue)
            {
                var fileSizeInfo = new TextBlock
                {
                    Text = $"Size: {MyMemories.Services.FileViewerService.FormatFileSize(link.FileSize.Value)}",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                stackPanel.Children.Add(fileSizeInfo);
            }

            confirmDialog = new ContentDialog
            {
                Title = "Remove Zip Link",
                Content = stackPanel,
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary && node.Parent != null)
            {
                var parentCategory = node.Parent;
                bool deletedFile = false;
                
                // Delete the physical file if checkbox is checked
                if (deleteFileCheckBox.IsChecked == true)
                {
                    try
                    {
                        System.IO.File.Delete(link.Url);
                        deletedFile = true;
                        StatusText.Text = $"Removed link and deleted zip file: {link.Title}";
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"Removed link but failed to delete zip file: {ex.Message}";
                        
                        var errorDialog = new ContentDialog
                        {
                            Title = "File Deletion Failed",
                            Content = $"The link was removed, but the zip file could not be deleted:\n\n{ex.Message}",
                            CloseButtonText = "OK",
                            XamlRoot = Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
                else
                {
                    StatusText.Text = $"Removed link (zip file kept): {link.Title}";
                }

                // Remove the link from the tree
                parentCategory.Children.Remove(node);

                // Update parent categories' ModifiedDate and save
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
                
                // Audit log the link removal
                var rootNode = GetRootCategoryNode(parentCategory);
                if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
                {
                    var details = deletedFile ? "Zip file deleted from disk" : "Zip file kept on disk";
                    await _configService!.AuditLogService!.LogLinkChangeAsync(
                        rootCategory.Name,
                        "removed",
                        link.Title,
                        $"URL: {link.Url}, {details}");
                }

                if (LinksTreeView.SelectedNode == node)
                {
                    ShowWelcome();
                }
            }
        }
        else
        {
            // Standard dialog for non-zip files
            confirmDialog = new ContentDialog
            {
                Title = "Remove Link",
                Content = $"Remove link '{link.Title}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary && node.Parent != null)
            {
                var parentCategory = node.Parent;
                parentCategory.Children.Remove(node);

                // Update parent categories' ModifiedDate and save
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
                
                // Audit log the link removal
                var rootNode = GetRootCategoryNode(parentCategory);
                if (rootNode?.Content is CategoryItem rootCategory && rootCategory.IsAuditLoggingEnabled)
                {
                    await _configService!.AuditLogService!.LogLinkChangeAsync(
                        rootCategory.Name,
                        "removed",
                        link.Title,
                        link.Url);
                }

                if (LinksTreeView.SelectedNode == node)
                {
                    ShowWelcome();
                }

                StatusText.Text = $"Removed link: {link.Title}";
            }
        }
    }
}