using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    /// <summary>
    /// Zips a folder and optionally links it to the parent category.
    /// Respects catalog settings: only zips cataloged files if folder is a catalog folder.
    /// </summary>
    private async Task ZipFolderAsync(LinkItem linkItem, TreeViewNode linkNode)
    {
        if (!Directory.Exists(linkItem.Url))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Folder Not Found",
                Content = $"The folder '{linkItem.Url}' does not exist or is not accessible.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        // Get parent directory for default target location
        var folderInfo = new DirectoryInfo(linkItem.Url);
        var parentDirectory = folderInfo.Parent?.FullName ?? folderInfo.Root.FullName;

        // Get the ROOT category to check for password protection
        var rootCategoryNode = GetRootCategoryNode(linkNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;

        // Check if ROOT category has password protection
        bool categoryHasPassword = rootCategory?.PasswordProtection != PasswordProtectionType.None;
        string? categoryPassword = null;

        if (categoryHasPassword && rootCategory != null)
        {
            // Get the password from the ROOT category
            categoryPassword = await GetCategoryPasswordAsync(rootCategory);
            if (categoryPassword == null)
            {
                // User cancelled password entry or password retrieval failed
                return;
            }
        }

        // Show zip configuration dialog with folder statistics and password option
        var result = await _linkDialog!.ShowZipFolderDialogAsync(
            linkItem.Title,
            parentDirectory,
            new[] { linkItem.Url },
            categoryHasPassword,
            categoryPassword,
            (zipTitle) => {
                var parentCategoryNode = _treeViewService!.GetParentCategoryNode(linkNode);
                if (parentCategoryNode != null && LinkExistsInCategory(parentCategoryNode, zipTitle))
                {
                    var categoryPath = _treeViewService.GetCategoryPath(parentCategoryNode);
                    return (false, $"A link named '{zipTitle}' already exists in '{categoryPath}'. Please choose a different name.");
                }
                return (true, null);
            },
            _treeViewService!.GetParentCategoryNode(linkNode)
        );

        if (result == null)
        {
            return; // User cancelled
        }

        // Build full zip file path
        var zipFileName = result.ZipFileName;
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        var zipFilePath = Path.Combine(result.TargetDirectory, zipFileName);

        // Check if zip file already exists
        if (File.Exists(zipFilePath))
        {
            var confirmDialog = new ContentDialog
            {
                Title = "File Already Exists",
                Content = $"The file '{zipFileName}' already exists in the target directory. Do you want to overwrite it?",
                PrimaryButtonText = "Overwrite",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var confirmResult = await confirmDialog.ShowAsync();
            if (confirmResult != ContentDialogResult.Primary)
            {
                return; // User cancelled overwrite
            }

            // Delete existing file
            try
            {
                File.Delete(zipFilePath);
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error Deleting File",
                    Content = $"Could not delete existing file: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
        }

        // Create temporary zip node with "busy creating" indicator if linking to category
        TreeViewNode? zipLinkNode = null;
        TreeViewNode? busyNode = null;
        TreeViewNode? parentCategoryNodeForLink = null;

        if (result.LinkToCategory && linkNode.Parent != null)
        {
            parentCategoryNodeForLink = _treeViewService!.GetParentCategoryNode(linkNode);
            if (parentCategoryNodeForLink != null)
            {
                var categoryPath = _treeViewService.GetCategoryPath(parentCategoryNodeForLink);

                // Create the zip link item (but with temporary status)
                var zipLinkItem = new LinkItem
                {
                    Title = Path.GetFileNameWithoutExtension(zipFileName),
                    Url = zipFilePath,
                    Description = "Creating zip archive...",
                    IsDirectory = true,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    FolderType = FolderLinkType.CatalogueFiles
                };

                zipLinkNode = new TreeViewNode { Content = zipLinkItem };

                // Create busy indicator child node
                var busyLinkItem = new LinkItem
                {
                    Title = "Busy creating...",
                    Url = string.Empty,
                    Description = "Zip archive is being created",
                    IsDirectory = false,
                    CategoryPath = categoryPath,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };

                busyNode = new TreeViewNode { Content = busyLinkItem };
                zipLinkNode.Children.Add(busyNode);
                zipLinkNode.IsExpanded = true;

                // Add the zip node to the parent category
                parentCategoryNodeForLink.Children.Add(zipLinkNode);
                parentCategoryNodeForLink.IsExpanded = true;

                // Navigate to the newly created zip node
                LinksTreeView.SelectedNode = zipLinkNode;
            }
        }

        // Create zip file
        try
        {
            StatusText.Text = $"Creating{(result.UsePassword ? " password-protected" : "")} zip file '{zipFileName}'...";

            // Start timing
            var startTime = DateTime.Now;

            // Check if this is a catalog folder
            bool isCatalogFolder = linkItem.FolderType == FolderLinkType.CatalogueFiles ||
                                   linkItem.FolderType == FolderLinkType.FilteredCatalogue;

            // Calculate original size for compression ratio
            ulong originalSize = 0;

            if (isCatalogFolder)
            {
                // Collect file paths on UI thread BEFORE entering background thread
                var filesToZip = CollectCatalogedFilePaths(linkNode, linkItem.Url);

                // Calculate original size from cataloged files
                foreach (var (filePath, _) in filesToZip)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            originalSize += (ulong)new FileInfo(filePath).Length;
                        }
                    }
                    catch { }
                }

                if (result.UsePassword && !string.IsNullOrEmpty(result.Password))
                {
                    // Password-protected catalog zip
                    await ZipUtilities.CreatePasswordProtectedZipAsync(linkItem.Url, zipFilePath, result.Password);
                }
                else
                {
                    // Zip only the cataloged files (from TreeView children)
                    await Task.Run(() =>
                    {
                        using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                        {
                            foreach (var (filePath, relativePath) in filesToZip)
                            {
                                if (File.Exists(filePath))
                                {
                                    archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
                                }
                            }
                        }
                    });
                }

                StatusText.Text = $"Successfully created '{zipFileName}' (cataloged files only)";
            }
            else
            {
                // Calculate original size for entire folder
                originalSize = await Task.Run(() =>
                {
                    ulong totalSize = 0;
                    try
                    {
                        var files = Directory.GetFiles(linkItem.Url, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                totalSize += (ulong)new FileInfo(file).Length;
                            }
                            catch { }
                        }
                    }
                    catch { }
                    return totalSize;
                });

                // Zip entire folder contents
                if (result.UsePassword && !string.IsNullOrEmpty(result.Password))
                {
                    // Use password-protected zip creation from ZipUtilities
                    var success = await ZipUtilities.CreatePasswordProtectedZipAsync(
                        linkItem.Url,
                        zipFilePath,
                        result.Password
                    );

                    if (!success)
                    {
                        throw new Exception("Failed to create password-protected zip file");
                    }
                }
                else
                {
                    // Standard zip creation
                    await Task.Run(() =>
                    {
                        ZipFile.CreateFromDirectory(linkItem.Url, zipFilePath, CompressionLevel.Optimal, false);
                    });
                }

                StatusText.Text = $"Successfully created '{zipFileName}'";
            }

            // Calculate compression statistics
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            var compressedSize = (ulong)new FileInfo(zipFilePath).Length;
            var compressionRatio = originalSize > 0 ? (1.0 - ((double)compressedSize / originalSize)) * 100.0 : 0.0;

            // Remove busy node and update the zip node with catalog contents
            if (result.LinkToCategory && zipLinkNode != null && parentCategoryNodeForLink != null)
            {
                // Remove the busy indicator
                if (busyNode != null)
                {
                    zipLinkNode.Children.Remove(busyNode);
                }

                // Update the zip link item with final information
                var finalZipLinkItem = zipLinkNode.Content as LinkItem;
                if (finalZipLinkItem != null)
                {
                    finalZipLinkItem.Description = (isCatalogFolder
                        ? $"Zip archive of cataloged files from '{linkItem.Title}'"
                        : $"Zip archive of '{linkItem.Title}'")
                        + (result.UsePassword ? " (password-protected)" : "");
                    finalZipLinkItem.FileSize = (ulong)new FileInfo(zipFilePath).Length;
                    finalZipLinkItem.LastCatalogUpdate = DateTime.Now;
                    finalZipLinkItem.IsZipPasswordProtected = result.UsePassword;
                }

                // Catalog the zip file contents
                await _catalogService!.CreateCatalogAsync(finalZipLinkItem!, zipLinkNode);

                // Save the updated category
                var rootNode = GetRootCategoryNode(parentCategoryNodeForLink);
                await _categoryService!.SaveCategoryAsync(rootNode);

                var categoryPath = _treeViewService!.GetCategoryPath(parentCategoryNodeForLink);
                StatusText.Text = $"Created '{zipFileName}' and linked to '{categoryPath}'";
            }

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The folder has been successfully zipped to:\n\n{zipFilePath}\n\n" +
                         $"📊 Statistics:\n" +
                         $"   • Original Size: {FileViewerService.FormatFileSize(originalSize)}\n" +
                         $"   • Compressed Size: {FileViewerService.FormatFileSize(compressedSize)}\n" +
                         $"   • Compression: {compressionRatio:F1}% reduction\n" +
                         $"   • Time Taken: {duration.TotalSeconds:F1} seconds" +
                         (isCatalogFolder ? "\n\n💡 Only cataloged files were included." : "") +
                         (result.UsePassword ? "\n\n🔒 Zip file is password-protected" : ""),
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error creating zip file: {ex.Message}";

            // Remove the temporary zip node if it was created
            if (zipLinkNode != null && parentCategoryNodeForLink != null)
            {
                parentCategoryNodeForLink.Children.Remove(zipLinkNode);
            }

            var errorDialog = new ContentDialog
            {
                Title = "Error Creating Zip File",
                Content = $"An error occurred while creating the zip file:\n\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    /// <summary>
    /// Collects all cataloged file paths from the TreeView hierarchy.
    /// MUST be called on UI thread before background processing.
    /// </summary>
    private List<(string filePath, string relativePath)> CollectCatalogedFilePaths(TreeViewNode linkNode, string rootPath)
    {
        var filesToZip = new List<(string, string)>();

        // Get all catalog entries from the TreeView node
        var catalogEntries = linkNode.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .Select(child => (node: child, link: (LinkItem)child.Content))
            .ToList();

        foreach (var (node, catalogEntry) in catalogEntries)
        {
            if (catalogEntry.IsDirectory)
            {
                // Recursively collect files from subdirectory
                CollectCatalogedFilePathsRecursive(node, catalogEntry, rootPath, filesToZip);
            }
            else
            {
                // Add individual file
                var relativePath = Path.GetRelativePath(rootPath, catalogEntry.Url);
                filesToZip.Add((catalogEntry.Url, relativePath));
            }
        }

        return filesToZip;
    }

    /// <summary>
    /// Recursively collects cataloged file paths from a directory node.
    /// MUST be called on UI thread.
    /// </summary>
    private void CollectCatalogedFilePathsRecursive(TreeViewNode dirNode, LinkItem directoryEntry, string rootPath, List<(string, string)> filesToZip)
    {
        if (!Directory.Exists(directoryEntry.Url))
        {
            return;
        }

        // Add all cataloged files from this directory
        var catalogEntries = dirNode.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .Select(child => (node: child, link: (LinkItem)child.Content))
            .ToList();

        foreach (var (node, catalogEntry) in catalogEntries)
        {
            if (catalogEntry.IsDirectory)
            {
                // Recursively add subdirectory files
                CollectCatalogedFilePathsRecursive(node, catalogEntry, rootPath, filesToZip);
            }
            else
            {
                // Add file
                var relativePath = Path.GetRelativePath(rootPath, catalogEntry.Url);
                filesToZip.Add((catalogEntry.Url, relativePath));
            }
        }
    }

    /// <summary>
    /// Recursively adds a directory and its cataloged contents to a zip archive.
    /// </summary>
    private void AddDirectoryToCatalogZip(ZipArchive archive, LinkItem directoryEntry, TreeViewNode parentLinkNode, string rootPath)
    {
        // Find the TreeViewNode for this directory entry
        var dirNode = parentLinkNode.Children
            .FirstOrDefault(child => child.Content is LinkItem link &&
                                     link.Url == directoryEntry.Url &&
                                     link.IsDirectory);

        if (dirNode == null || !Directory.Exists(directoryEntry.Url))
        {
            return;
        }

        // Get relative path for the directory
        var relativeDirPath = Path.GetRelativePath(rootPath, directoryEntry.Url);

        // Create directory entry in zip (for empty directories)
        archive.CreateEntry(relativeDirPath + Path.DirectorySeparatorChar);

        // Add all cataloged files from this directory
        var catalogEntries = dirNode.Children
            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
            .Select(child => (LinkItem)child.Content)
            .ToList();

        foreach (var catalogEntry in catalogEntries)
        {
            if (catalogEntry.IsDirectory)
            {
                // Recursively add subdirectory
                AddDirectoryToCatalogZip(archive, catalogEntry, dirNode, rootPath);
            }
            else if (File.Exists(catalogEntry.Url))
            {
                // Add file
                var relativePath = Path.GetRelativePath(rootPath, catalogEntry.Url);
                archive.CreateEntryFromFile(catalogEntry.Url, relativePath, CompressionLevel.Optimal);
            }
        }
    }

    /// <summary>
    /// Checks if a link with the given title already exists in the category (excluding catalog entries).
    /// </summary>
    private bool LinkExistsInCategory(TreeViewNode categoryNode, string linkTitle)
    {
        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link && !link.IsCatalogEntry)
            {
                if (string.Equals(link.Title, linkTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
