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

                // Add tooltip
                var tooltipText = linkItem.UrlStatus switch
                {
                    UrlStatus.Accessible => "URL is accessible",
                    UrlStatus.Error => $"URL error: {linkItem.UrlStatusMessage}",
                    UrlStatus.NotFound => "URL not found (404)",
                    _ => "URL status unknown"
                };
                
                if (linkItem.UrlLastChecked.HasValue)
                {
                    tooltipText += $"\nLast checked: {linkItem.UrlLastChecked.Value:yyyy-MM-dd HH:mm:ss}";
                }
                
                ToolTipService.SetToolTip(statusBadge, tooltipText);

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

                // Add tooltip
                ToolTipService.SetToolTip(unknownBadge, "URL status not checked yet\nClick 'Refresh URL State' to check");

                iconGrid.Children.Add(unknownBadge);
            }

            stackPanel.Children.Add(iconGrid);

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
                Debug.WriteLine("[RefreshArchiveFromManifestAsync] Zip is password-protected, prompting for password");
                
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

            // Find the root category node
            var rootCategoryNode = LinksTreeView.RootNodes
                .FirstOrDefault(n => n.Content is CategoryItem cat && cat.Name == rootCategoryName);

            if (rootCategoryNode == null)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Category Not Found",
                    Content = $"The root category '{rootCategoryName}' specified in the manifest was not found in the tree.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // Confirm with user
            var confirmDialog = new ContentDialog
            {
                Title = "Refresh Archive",
                Content = $"This will re-create the zip archive from the current state of the category:\n\n" +
                         $"?? {rootCategoryName}\n\n" +
                         $"The existing zip file will be overwritten with a fresh archive containing all current folders in the category.\n\n" +
                         $"Do you want to continue?",
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

            // Call the category zipping method
            StatusText.Text = $"Refreshing archive '{zipFileName}'...";

            // Re-zip the category (this will overwrite the existing zip)
            // Use the same password if the original was password-protected
            await ReZipCategoryAsync(rootCategoryNode, zipFileName, targetDirectory, zipPassword);

            // Small delay to ensure file is fully written and released
            await Task.Delay(100);

            // Re-catalog the updated zip
            _categoryService!.RemoveCatalogEntries(zipLinkNode);
            
            try
            {
                await _catalogService!.CreateCatalogAsync(zipLinkItem, zipLinkNode);
            }
            catch (InvalidDataException ex)
            {
                // If we get an unsupported compression method error, show a helpful message
                StatusText.Text = $"Warning: Created zip but cataloging failed - {ex.Message}";
                
                var warningDialog = new ContentDialog
                {
                    Title = "Zip Created with Warning",
                    Content = $"The zip archive was successfully created, but automatic cataloging failed.\n\n" +
                             $"Error: {ex.Message}\n\n" +
                             $"The zip file is valid and can be opened externally. " +
                             $"You may need to manually catalog it using the 'Create Catalog' button.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await warningDialog.ShowAsync();
                
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
                Content = $"The zip archive has been successfully refreshed from the current state of category '{rootCategoryName}'.\n\n" +
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

        // Delete existing file
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        // Create new zip with manifest using SharpZipLib for maximum compatibility
        await Task.Run(() =>
        {
            using (var zipOutputStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(File.Create(zipFilePath)))
            {
                // Use Deflate compression (method 8) with level 1 for compatibility
                zipOutputStream.SetLevel(1); // 1 = fastest, 9 = best compression

                // Set password if provided
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password;
                    zipOutputStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
                }

                // Create and add the manifest file
                var manifestContent = GenerateManifestContent(folderInfoList, category.Name);
                var manifestBytes = Encoding.UTF8.GetBytes(manifestContent);
                
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

                // Add all folder contents
                foreach (var folderPath in folderPaths)
                {
                    if (!Directory.Exists(folderPath))
                        continue;

                    var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
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
                            
                            using var fileStream = File.OpenRead(file);
                            fileStream.CopyTo(zipOutputStream);
                            
                            zipOutputStream.CloseEntry();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ReZipCategoryAsync] Error adding file {file}: {ex.Message}");
                            // Continue with other files
                        }
                    }
                }

                zipOutputStream.Finish();
            }
        });
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
}
