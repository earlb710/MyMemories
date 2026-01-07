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

        // Get the parent category to check for password protection
        var parentCategoryNode = _treeViewService!.GetParentCategoryNode(linkNode);
        CategoryItem? parentCategory = parentCategoryNode?.Content as CategoryItem;
        
        // CRITICAL FIX: Get the ROOT category to check for password protection
        var rootCategoryNode = GetRootCategoryNode(linkNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;
        
        // DEBUG OUTPUT
        System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] Link: {linkItem.Title}");
        System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] Parent category: {parentCategory?.Name}, PasswordProtection: {parentCategory?.PasswordProtection}");
        System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] Root category: {rootCategory?.Name}, PasswordProtection: {rootCategory?.PasswordProtection}");
        
        // Check if ROOT category has password protection
        bool categoryHasPassword = rootCategory?.PasswordProtection != PasswordProtectionType.None;
        string? categoryPassword = null;

        System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] categoryHasPassword: {categoryHasPassword}");

        if (categoryHasPassword && rootCategory != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] Attempting to get password for root category: {rootCategory.Name}");
            // Get the password from the ROOT category
            categoryPassword = await GetCategoryPasswordAsync(rootCategory);
            System.Diagnostics.Debug.WriteLine($"[ZipFolderAsync] Password retrieved: {(categoryPassword != null ? "Yes" : "No")}");
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
            categoryPassword
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

        // Create zip file
        try
        {
            StatusText.Text = $"Creating{(result.UsePassword ? " password-protected" : "")} zip file '{zipFileName}'...";

            // Check if this is a catalog folder
            bool isCatalogFolder = linkItem.FolderType == FolderLinkType.CatalogueFiles || 
                                   linkItem.FolderType == FolderLinkType.FilteredCatalogue;

            if (isCatalogFolder)
            {
                // CRITICAL FIX: Collect file paths on UI thread BEFORE entering background thread
                var filesToZip = CollectCatalogedFilePaths(linkNode, linkItem.Url);

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

            // If user wants to link the zip to the parent category
            if (result.LinkToCategory && linkNode.Parent != null)
            {
                var parentCategoryNodeForLink = _treeViewService!.GetParentCategoryNode(linkNode);
                if (parentCategoryNodeForLink != null)
                {
                    var categoryPath = _treeViewService.GetCategoryPath(parentCategoryNodeForLink);

                    // Create a new link for the zip file AS A CATALOG FOLDER
                    var zipLinkItem = new LinkItem
                    {
                        Title = Path.GetFileNameWithoutExtension(zipFileName),
                        Url = zipFilePath,
                        Description = (isCatalogFolder 
                            ? $"Zip archive of cataloged files from '{linkItem.Title}'"
                            : $"Zip archive of '{linkItem.Title}'")
                            + (result.UsePassword ? " (password-protected)" : ""),
                        IsDirectory = true, // Treat zip as a directory/catalog folder
                        CategoryPath = categoryPath,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        FolderType = FolderLinkType.CatalogueFiles, // Make it a catalog folder
                        FileSize = (ulong)new FileInfo(zipFilePath).Length,
                        LastCatalogUpdate = DateTime.Now
                    };

                    var zipLinkNode = new TreeViewNode { Content = zipLinkItem };

                    // Catalog the zip file contents immediately using CatalogService
                    await _catalogService!.CreateCatalogAsync(zipLinkItem, zipLinkNode);

                    // Add the zip link to the parent category
                    parentCategoryNodeForLink.Children.Add(zipLinkNode);

                    // Save the updated category
                    var rootNode = GetRootCategoryNode(parentCategoryNodeForLink);
                    await _categoryService!.SaveCategoryAsync(rootNode);

                    StatusText.Text = $"Created '{zipFileName}' and linked to '{categoryPath}'";
                }
            }

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The folder has been successfully zipped to:\n\n{zipFilePath}\n\nSize: {FileViewerService.FormatFileSize((ulong)new FileInfo(zipFilePath).Length)}"
                    + (isCatalogFolder ? "\n\nNote: Only cataloged files were included." : "")
                    + (result.UsePassword ? "\n\n🔒 Zip file is password-protected" : ""),
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error creating zip file: {ex.Message}";

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
}
