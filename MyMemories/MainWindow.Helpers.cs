using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyMemories.Services; 

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Helper class to store folder and category information.
    /// </summary>
    private class FolderCategoryInfo
    {
        public string FolderPath { get; set; } = string.Empty;
        public string FolderTitle { get; set; } = string.Empty;
        public string CategoryPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

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
                if (linkItem.UrlStatus != UrlStatus.Unknown)
                {
                    // Build detailed tooltip with status code explanation
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
                        if (linkItem.UrlStatusMessage.Contains("404"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(The requested page does not exist)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("403"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Access forbidden - authentication required)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("500"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Internal server error)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("503"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Service unavailable - server may be down)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("301") || linkItem.UrlStatusMessage.Contains("302"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Redirect - page moved to new location)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("410"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Page permanently removed)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("408"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Request timeout)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("429"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Too many requests - rate limited)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("401"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Authentication required)");
                        }
                        else if (linkItem.UrlStatusMessage.Contains("502"))
                        {
                            tooltipBuilder.AppendLine();
                            tooltipBuilder.Append("(Bad gateway - server error)");
                        }
                    }
                    
                    // Add last checked date
                    if (linkItem.UrlLastChecked.HasValue)
                    {
                        tooltipBuilder.AppendLine();
                        tooltipBuilder.AppendLine();
                        tooltipBuilder.Append($"Last checked: {linkItem.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    var tooltipText = tooltipBuilder.ToString();
                    
                    // Set up tooltip (WinUI 3 doesn't support delay customization via ToolTipService)
                    ToolTipService.SetToolTip(stackPanel, tooltipText);
                    
                    // Set up mouse enter/leave to show in status bar immediately
                    stackPanel.PointerEntered += (s, e) =>
                    {
                        StatusText.Text = tooltipText.Replace("\r\n", " | ");
                    };
                    
                    stackPanel.PointerExited += (s, e) =>
                    {
                        StatusText.Text = "Ready";
                    };
                }
                else if (linkItem.UrlStatus == UrlStatus.Unknown)
                {
                    var tooltipText = "URL status not checked yet\n\nClick 'Refresh URL State' on the category\nto check all URLs for accessibility";
                    
                    // Set up tooltip (WinUI 3 doesn't support delay customization via ToolTipService)
                    ToolTipService.SetToolTip(stackPanel, tooltipText);
                    
                    // Set up mouse enter/leave to show in status bar immediately
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
    /// Checks if a zip file contains a manifest and extracts the root category name.
    /// </summary>
    private async Task<string?> GetManifestRootCategoryAsync(string zipFilePath, string? password = null)
    {
        if (!File.Exists(zipFilePath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try standard .NET ZipFile first
                    using var archive = ZipFile.OpenRead(zipFilePath);
                    var manifestEntry = archive.GetEntry("_MANIFEST.txt");
                    
                    if (manifestEntry == null)
                        return null;

                    using var stream = manifestEntry.Open();
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();

                    // Parse the manifest to find "Root Category: [name]"
                    var match = Regex.Match(content, @"Root Category:\s*(.+)", RegexOptions.Multiline);
                    if (match.Success)
                    {
                        return match.Groups[1].Value.Trim();
                    }

                    return null;
                }
                catch (InvalidDataException)
                {
                    // Fallback to SharpZipLib for unsupported compression methods or encrypted zips
                    Debug.WriteLine("[GetManifestRootCategoryAsync] Using SharpZipLib fallback");
                    
                    try
                    {
                        using var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipFilePath);
                        
                        // Set password if provided
                        if (!string.IsNullOrEmpty(password))
                        {
                            zipFile.Password = password;
                        }
                        
                        var manifestEntry = zipFile.GetEntry("_MANIFEST.txt");
                        
                        if (manifestEntry == null)
                            return null;

                        using var stream = zipFile.GetInputStream(manifestEntry);
                        using var reader = new StreamReader(stream);
                        var content = reader.ReadToEnd();

                        // Parse the manifest to find "Root Category: [name]"
                        var match = Regex.Match(content, @"Root Category:\s*(.+)", RegexOptions.Multiline);
                        if (match.Success)
                        {
                            return match.Groups[1].Value.Trim();
                        }

                        return null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GetManifestRootCategoryAsync] SharpZipLib also failed: {ex.Message}");
                        return null;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetManifestRootCategoryAsync] Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Refreshes (re-creates) a zip archive from its manifest category.
    /// </summary>
    private async Task RefreshArchiveFromManifestAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        try
        {
            Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Starting for zip: {zipLinkItem.Url}");
            
            // Check if zip is password-protected and get password if needed
            string? zipPassword = null;
            if (zipLinkItem.IsZipPasswordProtected)
            {
                Debug.WriteLine("[RefreshArchiveFromManifestAsync] Zip is password-protected, attempting to get password");
                
                // First try to get password from root category (global or category password)
                var rootCategoryNode = GetRootCategoryNode(zipLinkNode);
                var rootCategory = rootCategoryNode?.Content as CategoryItem;
                
                if (rootCategory?.PasswordProtection != PasswordProtectionType.None)
                {
                    Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Root category has password protection: {rootCategory.PasswordProtection}");
                    
                    // Try to get the password from the service (which has it cached)
                    var passwordService = new PasswordDialogService(Content.XamlRoot, _categoryService!);
                    zipPassword = await passwordService.GetCategoryPasswordAsync(rootCategory);
                    
                    if (!string.IsNullOrEmpty(zipPassword))
                    {
                        Debug.WriteLine("[RefreshArchiveFromManifestAsync] Successfully retrieved password from category");
                    }
                    else
                    {
                        Debug.WriteLine("[RefreshArchiveFromManifestAsync] Failed to retrieve password from category");
                        StatusText.Text = "Archive refresh cancelled - password required";
                        return;
                    }
                }
                else
                {
                    // Category has no password protection, but zip is password-protected
                    // This means it has its own password - prompt for it
                    Debug.WriteLine("[RefreshArchiveFromManifestAsync] Zip has own password, prompting user");
                    
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
                        zipPassword = passwordBox.Password;
                        
                        if (string.IsNullOrEmpty(zipPassword))
                        {
                            StatusText.Text = "Password required to refresh archive";
                            return;
                        }
                    }
                    else
                    {
                        StatusText.Text = "Archive refresh cancelled";
                        return;
                    }
                }
            }
            
            // Get the root category from the manifest (with password if needed)
            var rootCategoryName = await GetManifestRootCategoryAsync(zipLinkItem.Url, zipPassword);
            
            Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Got root category name: '{rootCategoryName ?? "(null)"}'");
            
            if (string.IsNullOrEmpty(rootCategoryName))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "No Manifest Found",
                    Content = $"This zip file does not contain a manifest (_MANIFEST.txt) or the manifest could not be parsed.\n\nZip file: {zipLinkItem.Url}\n\nPlease ensure the zip was created using 'Zip Category' feature.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // The manifest stores the name of the category that was zipped
            // We need to find that category node in the tree (could be at any level)
            // Start by searching from the parent of this zip link
            TreeViewNode? manifestCategoryNode = null;
            
            // First check if the zip link's parent category matches
            if (zipLinkNode.Parent?.Content is CategoryItem zipParentCategory && zipParentCategory.Name == rootCategoryName)
            {
                manifestCategoryNode = zipLinkNode.Parent;
                Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Found manifest category as parent: {zipParentCategory.Name}");
            }
            else
            {
                // Search the entire tree for the category
                manifestCategoryNode = FindCategoryByName(rootCategoryName);
                Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Searched tree for category '{rootCategoryName}': {(manifestCategoryNode != null ? "Found" : "Not Found")}");
            }

            if (manifestCategoryNode == null)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Category Not Found",
                    Content = $"The manifest specifies that this zip was created from category '{rootCategoryName}', but that category no longer exists in the tree.\n\n" +
                             $"The category may have been renamed or deleted.\n\n" +
                             $"Please create or rename a category to '{rootCategoryName}' and try again.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var manifestCategory = manifestCategoryNode.Content as CategoryItem;

            // Confirm with user
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
                                    Text = manifestCategory!.Icon,
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

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            // Get the zip file info
            var zipFileInfo = new FileInfo(zipLinkItem.Url);
            var zipFileName = zipFileInfo.Name;
            var targetDirectory = zipFileInfo.DirectoryName ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Navigate to the zip node and show busy indicator
            LinksTreeView.SelectedNode = zipLinkNode;
            
            // IMPORTANT: Remove catalog entries FIRST to release all file handles to the zip
            _categoryService!.RemoveCatalogEntries(zipLinkNode);
            
            // Add busy indicator as a temporary child (matching the pattern from ZipFolderAsync)
            var busyLinkItem = new LinkItem
            {
                Title = "Busy creating...",
                Url = string.Empty,
                Description = "Zip archive is being refreshed",
                IsDirectory = false,
                CategoryPath = zipLinkItem.CategoryPath,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                IsCatalogEntry = true
            };
            
            var busyNode = new TreeViewNode { Content = busyLinkItem };
            zipLinkNode.Children.Add(busyNode);
            zipLinkNode.IsExpanded = true;

            // Call the category zipping method
            StatusText.Text = $"Refreshing archive '{zipFileName}'...";

            // Force garbage collection to ensure file handles are released
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Add longer delay to ensure the zip file is fully released
            await Task.Delay(500);

            // Re-zip the category (this will overwrite the existing zip)
            // Use the same password if the original was password-protected
            await ReZipCategoryAsync(manifestCategoryNode, zipFileName, targetDirectory, zipPassword);

            // Brief delay to ensure file system sync (reduced from 2000ms since we now properly close handles)
            StatusText.Text = $"Finalizing archive '{zipFileName}'...";
            await Task.Delay(100);

            // Force GC to release any lingering references
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Remove busy indicator
            zipLinkNode.Children.Remove(busyNode);

            StatusText.Text = $"Cataloging archive '{zipFileName}'...";

            // Re-catalog the updated zip with retry logic (reduced retries since file is properly closed)
            int maxRetries = 3;
            int retryDelay = 500; // Reduced from 2000ms
            Exception? lastException = null;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Retry attempt {attempt + 1} for cataloging");
                        StatusText.Text = $"Retrying catalog creation (attempt {attempt + 1}/{maxRetries})...";
                        
                        // Force GC before each retry
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        
                        await Task.Delay(retryDelay);
                        retryDelay *= 2; // Exponential backoff: 500ms, 1s, 2s
                    }
                    
                    await _catalogService!.CreateCatalogAsync(zipLinkItem, zipLinkNode);
                    lastException = null;
                    break; // Success!
                }
                catch (ICSharpCode.SharpZipLib.Zip.ZipException ex) when (ex.Message.Contains("Cannot find central directory") && attempt < maxRetries - 1)
                {
                    // Zip file not fully written yet, retry
                    Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Zip not ready yet (attempt {attempt + 1}): {ex.Message}");
                    lastException = ex;
                    continue;
                }
                catch (InvalidDataException ex) when (attempt < maxRetries - 1)
                {
                    // Might be temporary file corruption or file still being written, retry
                    Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Invalid data (attempt {attempt + 1}), retrying: {ex.Message}");
                    lastException = ex;
                    continue;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process") && attempt < maxRetries - 1)
                {
                    // File is still locked, retry
                    Debug.WriteLine($"[RefreshArchiveFromManifestAsync] File locked (attempt {attempt + 1}), retrying: {ex.Message}");
                    lastException = ex;
                    continue;
                }
                catch (Exception ex)
                {
                    // Other errors, don't retry
                    Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Non-retryable error: {ex.GetType().Name} - {ex.Message}");
                    lastException = ex;
                    break;
                }
            }
            
            if (lastException != null)
            {
                // All retries failed
                Debug.WriteLine($"[RefreshArchiveFromManifestAsync] All {maxRetries} attempts failed. Last error: {lastException.Message}");
                
                if (lastException is ICSharpCode.SharpZipLib.Zip.ZipException || 
                    lastException is InvalidDataException ||
                    (lastException is IOException && lastException.Message.Contains("being used")))
                {
                    StatusText.Text = $"Warning: Created zip but cataloging failed after {maxRetries} attempts - {lastException.Message}";
                    
                    var warningDialog = new ContentDialog
                    {
                        Title = "Zip Created with Warning",
                        Content = $"The zip archive was successfully created, but automatic cataloging failed after {maxRetries} attempts.\n\n" +
                                 $"Error: {lastException.Message}\n\n" +
                                 $"Possible reasons:\n" +
                                 $"• The zip file may still be locked by another process\n" +
                                 $"• Antivirus software may be scanning the file\n" +
                                 $"• The file system may be slow to sync\n\n" +
                                 $"The zip file is valid and can be opened externally. " +
                                 $"Try cataloging it manually later using the 'Create Catalog' button.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await warningDialog.ShowAsync();
                }
                else
                {
                    // Re-throw unexpected errors
                    throw lastException;
                }
                
                // Continue without catalog
                zipLinkItem.LastCatalogUpdate = DateTime.Now;
                zipLinkItem.FileSize = (ulong)new FileInfo(zipLinkItem.Url).Length;
                
                var refreshedZipNode = _treeViewService!.RefreshLinkNode(zipLinkNode, zipLinkItem);
                
                // Save the category
                var parentCat = refreshedZipNode.Parent;
                if (parentCat != null)
                {
                    await UpdateParentCategoriesAndSaveAsync(parentCat);
                }
                
                return;
            }

            // Update the zip link item
            zipLinkItem.LastCatalogUpdate = DateTime.Now;
            zipLinkItem.FileSize = (ulong)new FileInfo(zipLinkItem.Url).Length;
            _categoryService.UpdateCatalogFileCount(zipLinkNode);

            var refreshedNode = _treeViewService!.RefreshLinkNode(zipLinkNode, zipLinkItem);

            // Save the category
            var parentCategory = refreshedNode.Parent;
            if (parentCategory != null)
            {
                await UpdateParentCategoriesAndSaveAsync(parentCategory);
            }

            StatusText.Text = $"Successfully refreshed archive '{zipFileName}'";

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Archive Refreshed",
                Content = $"The zip archive has been successfully refreshed from the current state of category '{manifestCategory.Name}'.\n\n" +
                         $"Location: {zipLinkItem.Url}\n" +
                         $"Size: {FileViewerService.FormatFileSize(zipLinkItem.FileSize ?? 0)}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RefreshArchiveFromManifestAsync] Error: {ex}");
            StatusText.Text = $"Error refreshing archive: {ex.Message}";

            var errorDialog = new ContentDialog
            {
                Title = "Error Refreshing Archive",
                Content = $"An error occurred while refreshing the zip archive:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    /// <summary>
    /// Re-creates a zip file from a category (used by refresh archive).
    /// Uses SharpZipLib for maximum compatibility.
    /// </summary>
    private async Task ReZipCategoryAsync(TreeViewNode categoryNode, string zipFileName, string targetDirectory, string? password = null)
    {
        if (categoryNode.Content is not CategoryItem category)
            return;

        // Collect all folder links from the category
        var folderInfoList = CollectFolderInfoFromCategory(categoryNode, category.Name);
        var folderPaths = folderInfoList.Select(f => f.FolderPath).ToArray();

        if (folderPaths.Length == 0)
        {
            throw new InvalidOperationException("No folders found in category to zip.");
        }

        // Build full zip file path
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        var zipFilePath = Path.Combine(targetDirectory, zipFileName);

        // Delete existing file with retry logic
        if (File.Exists(zipFilePath))
        {
            int deleteRetries = 3;
            for (int i = 0; i < deleteRetries; i++)
            {
                try
                {
                    File.Delete(zipFilePath);
                    break;
                }
                catch (IOException) when (i < deleteRetries - 1)
                {
                    await Task.Delay(500);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        // Generate manifest content on UI thread (before entering Task.Run)
        var manifestContent = GenerateManifestContent(folderInfoList, category.Name);

        // Create new zip with manifest using SharpZipLib for maximum compatibility
        Debug.WriteLine($"[ReZipCategoryAsync] Creating zip file: {zipFilePath}");
        Debug.WriteLine($"[ReZipCategoryAsync] Folder paths to zip: {string.Join(", ", folderPaths)}");
        Debug.WriteLine($"[ReZipCategoryAsync] Manifest content length: {manifestContent.Length} chars");
        
        int filesAdded = 0;
        
        await Task.Run(() =>
        {
            // Use explicit file stream with no buffering for immediate write
            using var fileStream = new FileStream(
                zipFilePath, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize: 4096, 
                FileOptions.WriteThrough); // WriteThrough bypasses OS cache
            
            using var zipOutputStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fileStream, 8192);
            
            // IMPORTANT: Tell SharpZipLib NOT to close the underlying stream
            // We'll close it ourselves to ensure proper cleanup
            zipOutputStream.IsStreamOwner = false;
            
            // Use Deflate compression (method 8) with level 6 for good balance
            zipOutputStream.SetLevel(6);

            // Set password if provided
            if (!string.IsNullOrEmpty(password))
            {
                zipOutputStream.Password = password;
                zipOutputStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
            }

            // Create and add the manifest file
            var manifestBytes = Encoding.UTF8.GetBytes(manifestContent);
            Debug.WriteLine($"[ReZipCategoryAsync] Adding manifest file ({manifestBytes.Length} bytes)");
            
            var manifestEntry = new ICSharpCode.SharpZipLib.Zip.ZipEntry("_MANIFEST.txt")
            {
                DateTime = DateTime.Now,
                Size = manifestBytes.Length
            };
            
            // Set AES encryption if password is provided
            if (!string.IsNullOrEmpty(password))
            {
                manifestEntry.AESKeySize = 256;
            }
            
            zipOutputStream.PutNextEntry(manifestEntry);
            zipOutputStream.Write(manifestBytes, 0, manifestBytes.Length);
            zipOutputStream.CloseEntry();
            filesAdded++;

            // Add all folder contents
            foreach (var folderPath in folderPaths)
            {
                Debug.WriteLine($"[ReZipCategoryAsync] Processing folder: {folderPath}");
                
                if (!Directory.Exists(folderPath))
                {
                    Debug.WriteLine($"[ReZipCategoryAsync] Folder does not exist: {folderPath}");
                    continue;
                }

                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                Debug.WriteLine($"[ReZipCategoryAsync] Found {files.Length} files in folder");
                var folderName = new DirectoryInfo(folderPath).Name;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var relativePath = Path.GetRelativePath(folderPath, file);
                        var entryName = Path.Combine(folderName, relativePath).Replace(Path.DirectorySeparatorChar, '/');
                        
                        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(entryName)
                        {
                            DateTime = fileInfo.LastWriteTime,
                            Size = fileInfo.Length
                        };
                        
                        // Set AES encryption if password is provided
                        if (!string.IsNullOrEmpty(password))
                        {
                            entry.AESKeySize = 256;
                        }
                        
                        zipOutputStream.PutNextEntry(entry);
                        
                        // Read and write file contents
                        using (var inputFileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            inputFileStream.CopyTo(zipOutputStream);
                        }
                        
                        zipOutputStream.CloseEntry();
                        filesAdded++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ReZipCategoryAsync] Error adding file {file}: {ex.Message}");
                        // Continue with other files
                    }
                }
            }

            Debug.WriteLine($"[ReZipCategoryAsync] Added {filesAdded} entries to zip");
            
            // CRITICAL: Finish writing the central directory
            Debug.WriteLine($"[ReZipCategoryAsync] Calling Finish()...");
            zipOutputStream.Finish();
            
            // Flush the zip stream
            Debug.WriteLine($"[ReZipCategoryAsync] Calling Flush()...");
            zipOutputStream.Flush();
            
            // Close the zip stream (this writes any remaining data)
            Debug.WriteLine($"[ReZipCategoryAsync] Closing zip stream...");
            zipOutputStream.Close();
            
            // Now flush and close the file stream
            Debug.WriteLine($"[ReZipCategoryAsync] Flushing file stream...");
            fileStream.Flush(true); // Force flush to disk
            
            Debug.WriteLine($"[ReZipCategoryAsync] Closing file stream...");
            fileStream.Close();
            
            Debug.WriteLine($"[ReZipCategoryAsync] Successfully created zip file: {zipFilePath}");
        });
        
        Debug.WriteLine($"[ReZipCategoryAsync] Task.Run completed, checking file...");
        
        // Verify the file was created and is valid
        if (!File.Exists(zipFilePath))
        {
            throw new IOException($"Zip file was not created: {zipFilePath}");
        }
        
        var createdFileInfo = new FileInfo(zipFilePath);
        Debug.WriteLine($"[ReZipCategoryAsync] Zip file size: {createdFileInfo.Length} bytes");
        
        if (createdFileInfo.Length < 22) // Minimum valid zip file size (empty zip with end of central directory)
        {
            throw new IOException($"Zip file is too small to be valid: {createdFileInfo.Length} bytes");
        }
    }

    /// <summary>
    /// Collects folder information including their category paths.
    /// </summary>
    private List<FolderCategoryInfo> CollectFolderInfoFromCategory(TreeViewNode categoryNode, string parentCategoryPath)
    {
        var folderInfoList = new List<FolderCategoryInfo>();

        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Only include directory links that are not catalog entries
                if (link.IsDirectory && !link.IsCatalogEntry && Directory.Exists(link.Url))
                {
                    folderInfoList.Add(new FolderCategoryInfo
                    {
                        FolderPath = link.Url,
                        FolderTitle = link.Title,
                        CategoryPath = parentCategoryPath,
                        Description = link.Description,
                        CreatedDate = link.CreatedDate,
                        ModifiedDate = link.ModifiedDate
                    });
                }
            }
            else if (child.Content is CategoryItem subCategory)
            {
                // Recursively collect from subcategories
                var subCategoryPath = string.IsNullOrEmpty(parentCategoryPath) 
                    ? subCategory.Name 
                    : $"{parentCategoryPath} > {subCategory.Name}";
                folderInfoList.AddRange(CollectFolderInfoFromCategory(child, subCategoryPath));
            }
        }

        return folderInfoList;
    }

    /// <summary>
    /// Generates the manifest file content.
    /// </summary>
    private string GenerateManifestContent(List<FolderCategoryInfo> folderInfoList, string rootCategoryName)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("================================================================================");
        sb.AppendLine("                    ZIP ARCHIVE MANIFEST");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Root Category: {rootCategoryName}");
        sb.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Folders: {folderInfoList.Count}");
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                    DIRECTORY-TO-CATEGORY MAPPINGS");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        // Group by category for better organization
        var groupedByCategory = folderInfoList
            .GroupBy(f => f.CategoryPath)
            .OrderBy(g => g.Key);

        foreach (var categoryGroup in groupedByCategory)
        {
            sb.AppendLine($"Category: {categoryGroup.Key}");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine();

            foreach (var folder in categoryGroup.OrderBy(f => f.FolderTitle))
            {
                sb.AppendLine($"  Title: {folder.FolderTitle}");
                sb.AppendLine($"  Path:  {folder.FolderPath}");
                
                if (!string.IsNullOrWhiteSpace(folder.Description))
                {
                    sb.AppendLine($"  Desc:  {folder.Description}");
                }
                
                sb.AppendLine($"  Created:  {folder.CreatedDate:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Modified: {folder.ModifiedDate:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("================================================================================");
        sb.AppendLine("                         END OF MANIFEST");
        sb.AppendLine("================================================================================");

        return sb.ToString();
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
            
            // Search recursively in children
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
            
            // Recursively search in subcategories
            var found = FindCategoryByNameRecursive(child, categoryName);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
