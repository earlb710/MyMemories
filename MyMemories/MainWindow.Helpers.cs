using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services;
using MyMemories.Utilities;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Creates a visual element for a tree node with icon and optional badge.
    /// </summary>
    private FrameworkElement CreateNodeContent(object content)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // For LinkItem, show icon with potential badge
        if (content is LinkItem linkItem)
        {
            // Create icon container with badge overlay
            var iconGrid = new Grid
            {
                Width = 20,
                Height = 20
            };

            // Primary icon (emoji)
            var primaryIcon = new TextBlock
            {
                Text = linkItem.GetIcon(),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconGrid.Children.Add(primaryIcon);

            // Add link badge for LinkOnly folders
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry && linkItem.FolderType == FolderLinkType.LinkOnly)
            {
                var linkBadge = new FontIcon
                {
                    Glyph = "\uE71B", // Link icon
                    FontSize = 8,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2)
                };
                
                linkBadge.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                iconGrid.Children.Add(linkBadge);
            }

            // Check if folder has changed and add warning badge
            if (linkItem.IsDirectory && 
                !linkItem.IsCatalogEntry && 
                linkItem.LastCatalogUpdate.HasValue &&
                Directory.Exists(linkItem.Url))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(linkItem.Url);
                    if (dirInfo.LastWriteTime > linkItem.LastCatalogUpdate.Value)
                    {
                        // Add warning badge icon
                        var badgeIcon = new FontIcon
                        {
                            Glyph = "\uE7BA", // Warning icon
                            FontSize = 11,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(0, 0, -1, -1)
                        };

                        // Set badge color to bright red
                        badgeIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);

                        // Add tooltip
                        ToolTipService.SetToolTip(badgeIcon, "Folder has changed since last catalog");

                        iconGrid.Children.Add(badgeIcon);
                    }
                }
                catch
                {
                    // Ignore errors accessing directory
                }
            }

            // Add URL status badge for web URLs
            if (!linkItem.IsDirectory && 
                Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var uri) && 
                !uri.IsFile &&
                linkItem.UrlStatus != UrlStatus.Unknown)
            {
                var statusBadge = new FontIcon
                {
                    Glyph = "\uE734", // StatusCircle icon
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2)
                };

                // Set color based on status
                statusBadge.Foreground = new SolidColorBrush(linkItem.UrlStatus switch
                {
                    UrlStatus.Accessible => Microsoft.UI.Colors.LimeGreen,
                    UrlStatus.Error => Microsoft.UI.Colors.Yellow,
                    UrlStatus.NotFound => Microsoft.UI.Colors.Red,
                    _ => Microsoft.UI.Colors.Gray
                });

                iconGrid.Children.Add(statusBadge);
            }
            // Add black question mark for unknown URL status
            else if (!linkItem.IsDirectory && 
                     Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var unknownUri) && 
                     !unknownUri.IsFile &&
                     linkItem.UrlStatus == UrlStatus.Unknown)
            {
                var unknownBadge = new FontIcon
                {
                    Glyph = "\uE9CE", // Help/Question mark icon
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2)
                };

                // Set color to black
                unknownBadge.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

                iconGrid.Children.Add(unknownBadge);
            }

            stackPanel.Children.Add(iconGrid);

            // Set tooltip on the entire stack panel for URLs with status information
            if (!linkItem.IsDirectory && 
                Uri.TryCreate(linkItem.Url, UriKind.Absolute, out var tooltipUri) && 
                !tooltipUri.IsFile)
            {
                SetupUrlStatusTooltip(stackPanel, linkItem);
            }

            // Add text with file count if applicable
            var displayText = linkItem.Title;
            if (linkItem.IsDirectory && !linkItem.IsCatalogEntry && linkItem.CatalogFileCount > 0)
            {
                displayText = $"{linkItem.Title} ({linkItem.CatalogFileCount} file{(linkItem.CatalogFileCount != 1 ? "s" : "")})";
            }

            var textBlock = new TextBlock
            {
                Text = displayText,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }
        // For CategoryItem, just show icon and name
        else if (content is CategoryItem categoryItem)
        {
            var iconText = new TextBlock
            {
                Text = categoryItem.Icon,
                FontSize = 16
            };
            stackPanel.Children.Add(iconText);

            var textBlock = new TextBlock
            {
                Text = categoryItem.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }

        return stackPanel;
    }

    /// <summary>
    /// Sets up URL status tooltip and hover behavior for a link item.
    /// </summary>
    private void SetupUrlStatusTooltip(StackPanel stackPanel, LinkItem linkItem)
    {
        if (linkItem.UrlStatus != UrlStatus.Unknown)
        {
            var tooltipText = BuildUrlStatusTooltip(linkItem);
            ToolTipService.SetToolTip(stackPanel, tooltipText);

            stackPanel.PointerEntered += (s, e) =>
            {
                StatusText.Text = tooltipText.Replace("\r\n", " | ");
            };

            stackPanel.PointerExited += (s, e) =>
            {
                StatusText.Text = "Ready";
            };
        }
        else
        {
            var tooltipText = "URL status not checked yet\n\nClick 'Refresh URL State' on the category\nto check all URLs for accessibility";
            ToolTipService.SetToolTip(stackPanel, tooltipText);

            stackPanel.PointerEntered += (s, e) =>
            {
                StatusText.Text = "URL status not checked | Click 'Refresh URL State' on category to check";
            };

            stackPanel.PointerExited += (s, e) =>
            {
                StatusText.Text = "Ready";
            };
        }
    }

    /// <summary>
    /// Builds a detailed tooltip string for URL status.
    /// </summary>
    private static string BuildUrlStatusTooltip(LinkItem linkItem)
    {
        var tooltipBuilder = new StringBuilder();

        // Add main status
        tooltipBuilder.Append(linkItem.UrlStatus switch
        {
            UrlStatus.Accessible => "? URL is accessible",
            UrlStatus.Error => "? URL error",
            UrlStatus.NotFound => "? URL not found",
            _ => "URL status unknown"
        });

        // Add status message with HTTP code if available
        if (!string.IsNullOrWhiteSpace(linkItem.UrlStatusMessage))
        {
            tooltipBuilder.AppendLine();
            tooltipBuilder.Append($"Status: {linkItem.UrlStatusMessage}");

            // Add explanation for common HTTP codes
            var explanation = GetHttpStatusExplanation(linkItem.UrlStatusMessage);
            if (!string.IsNullOrEmpty(explanation))
            {
                tooltipBuilder.AppendLine();
                tooltipBuilder.Append(explanation);
            }
        }

        // Add last checked date
        if (linkItem.UrlLastChecked.HasValue)
        {
            tooltipBuilder.AppendLine();
            tooltipBuilder.AppendLine();
            tooltipBuilder.Append($"Last checked: {linkItem.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}");
        }

        return tooltipBuilder.ToString();
    }

    /// <summary>
    /// Gets a human-readable explanation for common HTTP status codes.
    /// </summary>
    private static string? GetHttpStatusExplanation(string statusMessage)
    {
        return statusMessage switch
        {
            _ when statusMessage.Contains("404") => "(The requested page does not exist)",
            _ when statusMessage.Contains("403") => "(Access forbidden - authentication required)",
            _ when statusMessage.Contains("500") => "(Internal server error)",
            _ when statusMessage.Contains("503") => "(Service unavailable - server may be down)",
            _ when statusMessage.Contains("301") || statusMessage.Contains("302") => "(Redirect - page moved to new location)",
            _ when statusMessage.Contains("410") => "(Page permanently removed)",
            _ when statusMessage.Contains("408") => "(Request timeout)",
            _ when statusMessage.Contains("429") => "(Too many requests - rate limited)",
            _ when statusMessage.Contains("401") => "(Authentication required)",
            _ when statusMessage.Contains("502") => "(Bad gateway - server error)",
            _ => null
        };
    }

    /// <summary>
    /// Refreshes the visual content of a tree node.
    /// </summary>
    public void RefreshNodeVisual(TreeViewNode node)
    {
        if (node.Content != null)
        {
            // Force update by recreating the visual
            var content = node.Content;
            node.Content = null;
            node.Content = content;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);

        if (parentObject == null)
            return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
    }

    /// <summary>
    /// Updates the ModifiedDate of all parent categories up to the root.
    /// Call this whenever a link is added, removed, edited, or catalog is refreshed.
    /// </summary>
    private void UpdateParentCategoriesModifiedDate(TreeViewNode node)
    {
        var now = DateTime.Now;
        var current = node.Parent;

        while (current != null)
        {
            if (current.Content is CategoryItem category)
            {
                category.ModifiedDate = now;
            }
            current = current.Parent;
        }
    }

    /// <summary>
    /// Updates the ModifiedDate of all parent categories and saves the root category.
    /// </summary>
    private async Task UpdateParentCategoriesAndSaveAsync(TreeViewNode node)
    {
        // Update all parent categories' ModifiedDate
        UpdateParentCategoriesModifiedDate(node);

        // Save the root category
        var rootNode = GetRootCategoryNode(node);
        await _categoryService!.SaveCategoryAsync(rootNode);
    }

    /// <summary>
    /// Refreshes (re-creates) a zip archive from its manifest category.
    /// </summary>
    private async Task RefreshArchiveFromManifestAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        try
        {
            // Get password if zip is password-protected
            string? zipPassword = await GetZipPasswordIfRequiredAsync(zipLinkItem, zipLinkNode);
            if (zipLinkItem.IsZipPasswordProtected && zipPassword == null)
            {
                StatusText.Text = "Archive refresh cancelled - password required";
                return;
            }

            // Get the root category from the manifest
            var rootCategoryName = await _archiveRefreshService!.GetManifestRootCategoryAsync(zipLinkItem.Url, zipPassword);

            if (string.IsNullOrEmpty(rootCategoryName))
            {
                await ShowNoManifestErrorAsync(zipLinkItem.Url);
                return;
            }

            // Find the manifest category node
            var manifestCategoryNode = FindManifestCategoryNode(zipLinkNode, rootCategoryName);
            if (manifestCategoryNode == null)
            {
                await ShowCategoryNotFoundErrorAsync(rootCategoryName);
                return;
            }

            var manifestCategory = manifestCategoryNode.Content as CategoryItem;

            // Confirm with user
            if (!await ConfirmArchiveRefreshAsync(manifestCategory!))
                return;

            // Perform the refresh
            await ExecuteArchiveRefreshAsync(zipLinkItem, zipLinkNode, manifestCategoryNode, zipPassword);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error refreshing archive: {ex.Message}";
            await ShowArchiveRefreshErrorAsync(ex);
        }
    }

    /// <summary>
    /// Gets the password for a password-protected zip file.
    /// </summary>
    private async Task<string?> GetZipPasswordIfRequiredAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        if (!zipLinkItem.IsZipPasswordProtected)
            return null;

        // First try to get password from root category
        var rootCategoryNode = GetRootCategoryNode(zipLinkNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;

        if (rootCategory?.PasswordProtection != PasswordProtectionType.None)
        {
            var passwordService = new PasswordDialogService(Content.XamlRoot, _categoryService!);
            return await passwordService.GetCategoryPasswordAsync(rootCategory);
        }

        // Prompt for zip-specific password
        return await PromptForZipPasswordAsync(zipLinkItem);
    }

    /// <summary>
    /// Prompts the user for a zip-specific password.
    /// </summary>
    private async Task<string?> PromptForZipPasswordAsync(LinkItem zipLinkItem)
    {
        var passwordDialog = new ContentDialog
        {
            Title = "Password Required",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"The zip file '{Path.GetFileName(zipLinkItem.Url)}' is password-protected.\n\nPlease enter the password to read the manifest:",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new PasswordBox
                    {
                        Name = "ZipPasswordBox",
                        PlaceholderText = "Enter zip password"
                    }
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot
        };

        var passwordBox = (passwordDialog.Content as StackPanel)?.Children[1] as PasswordBox;

        if (await passwordDialog.ShowAsync() == ContentDialogResult.Primary && passwordBox != null)
        {
            var password = passwordBox.Password;
            return string.IsNullOrEmpty(password) ? null : password;
        }

        return null;
    }

    /// <summary>
    /// Shows an error dialog when no manifest is found.
    /// </summary>
    private async Task ShowNoManifestErrorAsync(string zipFilePath)
    {
        var errorDialog = new ContentDialog
        {
            Title = "No Manifest Found",
            Content = $"This zip file does not contain a manifest (_MANIFEST.txt) or the manifest could not be parsed.\n\nZip file: {zipFilePath}\n\nPlease ensure the zip was created using 'Zip Category' feature.",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await errorDialog.ShowAsync();
    }

    /// <summary>
    /// Finds the category node that matches the manifest root category.
    /// </summary>
    private TreeViewNode? FindManifestCategoryNode(TreeViewNode zipLinkNode, string rootCategoryName)
    {
        // First check if the zip link's parent category matches
        if (zipLinkNode.Parent?.Content is CategoryItem zipParentCategory && zipParentCategory.Name == rootCategoryName)
        {
            return zipLinkNode.Parent;
        }

        // Search the entire tree for the category
        return FindCategoryByName(rootCategoryName);
    }

    /// <summary>
    /// Shows an error dialog when the manifest category is not found.
    /// </summary>
    private async Task ShowCategoryNotFoundErrorAsync(string categoryName)
    {
        var errorDialog = new ContentDialog
        {
            Title = "Category Not Found",
            Content = $"The manifest specifies that this zip was created from category '{categoryName}', but that category no longer exists in the tree.\n\n" +
                     $"The category may have been renamed or deleted.\n\n" +
                     $"Please create or rename a category to '{categoryName}' and try again.",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await errorDialog.ShowAsync();
    }

    /// <summary>
    /// Shows a confirmation dialog for archive refresh.
    /// </summary>
    private async Task<bool> ConfirmArchiveRefreshAsync(CategoryItem manifestCategory)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "Refresh Archive",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "This will re-create the zip archive from the current state of the category:",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = manifestCategory.Icon,
                                FontSize = 20
                            },
                            new TextBlock
                            {
                                Text = manifestCategory.Name,
                                FontSize = 16,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = "The existing zip file will be overwritten with a fresh archive containing all current folders in the category.\n\nDo you want to continue?",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = "Refresh Archive",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        return await confirmDialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Executes the archive refresh operation.
    /// </summary>
    private async Task ExecuteArchiveRefreshAsync(
        LinkItem zipLinkItem,
        TreeViewNode zipLinkNode,
        TreeViewNode manifestCategoryNode,
        string? zipPassword)
    {
        var zipFileInfo = new FileInfo(zipLinkItem.Url);
        var zipFileName = zipFileInfo.Name;
        var targetDirectory = zipFileInfo.DirectoryName ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var manifestCategory = manifestCategoryNode.Content as CategoryItem;

        // Navigate to the zip node
        LinksTreeView.SelectedNode = zipLinkNode;

        // Remove catalog entries to release file handles
        _archiveRefreshService!.RemoveCatalogEntries(zipLinkNode);

        // Add busy indicator
        var busyNode = CreateBusyIndicatorNode(zipLinkItem.CategoryPath);
        zipLinkNode.Children.Add(busyNode);
        zipLinkNode.IsExpanded = true;

        StatusText.Text = $"Refreshing archive '{zipFileName}'...";

        // Force garbage collection to release file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(500);

        // Re-zip the category
        await _archiveRefreshService.ReZipCategoryAsync(manifestCategoryNode, zipFileName, targetDirectory, zipPassword);

        StatusText.Text = $"Finalizing archive '{zipFileName}'...";
        await Task.Delay(100);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Remove busy indicator
        zipLinkNode.Children.Remove(busyNode);

        StatusText.Text = $"Cataloging archive '{zipFileName}'...";

        // Re-catalog with retry logic
        var (success, error) = await _archiveRefreshService.CatalogZipWithRetryAsync(zipLinkItem, zipLinkNode);

        if (!success && error != null)
        {
            await HandleCatalogErrorAsync(zipLinkItem, zipLinkNode, error);
            return;
        }

        // Update the zip link item
        _archiveRefreshService.UpdateZipLinkAfterRefresh(zipLinkItem, zipLinkNode);

        var refreshedNode = _archiveRefreshService.RefreshLinkNode(zipLinkNode, zipLinkItem);

        // Save the category
        if (refreshedNode.Parent != null)
        {
            await UpdateParentCategoriesAndSaveAsync(refreshedNode.Parent);
        }

        StatusText.Text = $"Successfully refreshed archive '{zipFileName}'";

        // Show success dialog
        await ShowArchiveRefreshSuccessAsync(manifestCategory!.Name, zipLinkItem);
    }

    /// <summary>
    /// Creates a busy indicator node for display during operations.
    /// </summary>
    private static TreeViewNode CreateBusyIndicatorNode(string categoryPath)
    {
        var busyLinkItem = new LinkItem
        {
            Title = "Busy creating...",
            Url = string.Empty,
            Description = "Zip archive is being refreshed",
            IsDirectory = false,
            CategoryPath = categoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            IsCatalogEntry = true
        };

        return new TreeViewNode { Content = busyLinkItem };
    }

    /// <summary>
    /// Handles catalog errors after zip creation.
    /// </summary>
    private async Task HandleCatalogErrorAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode, Exception error)
    {
        if (error is ICSharpCode.SharpZipLib.Zip.ZipException ||
            error is InvalidDataException ||
            (error is IOException && error.Message.Contains("being used")))
        {
            StatusText.Text = $"Warning: Created zip but cataloging failed - {error.Message}";

            await DialogFactory.ShowWarningAsync(
                "Zip Created with Warning",
                $"The zip archive was successfully created, but automatic cataloging failed.\n\n" +
                $"Error: {error.Message}\n\n" +
                $"The zip file is valid and can be opened externally. " +
                $"Try cataloging it manually later using the 'Create Catalog' button.",
                Content.XamlRoot);
        }
        else
        {
            throw error;
        }

        // Update without catalog
        zipLinkItem.LastCatalogUpdate = DateTime.Now;
        zipLinkItem.FileSize = (ulong)new FileInfo(zipLinkItem.Url).Length;

        var refreshedZipNode = _treeViewService!.RefreshLinkNode(zipLinkNode, zipLinkItem);

        if (refreshedZipNode.Parent != null)
        {
            await UpdateParentCategoriesAndSaveAsync(refreshedZipNode.Parent);
        }
    }

    /// <summary>
    /// Shows a success dialog after archive refresh.
    /// </summary>
    private async Task ShowArchiveRefreshSuccessAsync(string categoryName, LinkItem zipLinkItem)
    {
        await DialogFactory.ShowSuccessAsync(
            "Archive Refreshed",
            $"The zip archive has been successfully refreshed from the current state of category '{categoryName}'.\n\n" +
            $"Location: {zipLinkItem.Url}\n" +
            $"Size: {FileViewerService.FormatFileSize(zipLinkItem.FileSize ?? 0)}",
            Content.XamlRoot);
    }

    /// <summary>
    /// Shows an error dialog for archive refresh failures.
    /// </summary>
    private async Task ShowArchiveRefreshErrorAsync(Exception ex)
    {
        await DialogFactory.ShowErrorAsync(
            "Error Refreshing Archive",
            $"An error occurred while refreshing the zip archive:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
            Content.XamlRoot);
    }

    /// <summary>
    /// Finds a category node by name in the entire tree (searches recursively).
    /// </summary>
    private TreeViewNode? FindCategoryByName(string categoryName)
    {
        foreach (var rootNode in LinksTreeView.RootNodes)
        {
            if (rootNode.Content is CategoryItem rootCategory && rootCategory.Name == categoryName)
            {
                return rootNode;
            }

            var found = FindCategoryByNameRecursive(rootNode, categoryName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively searches for a category by name in a tree node's children.
    /// </summary>
    private TreeViewNode? FindCategoryByNameRecursive(TreeViewNode node, string categoryName)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is CategoryItem category && category.Name == categoryName)
            {
                return child;
            }

            var found = FindCategoryByNameRecursive(child, categoryName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
