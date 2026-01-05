using Microsoft.UI.Xaml.Controls;
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

        // Show zip configuration dialog
        var result = await _linkDialog!.ShowZipFolderDialogAsync(
            linkItem.Title,
            parentDirectory
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
            StatusText.Text = $"Creating zip file '{zipFileName}'...";

            // Check if this is a catalog folder
            bool isCatalogFolder = linkItem.FolderType == FolderLinkType.CatalogueFiles || 
                                   linkItem.FolderType == FolderLinkType.FilteredCatalogue;

            if (isCatalogFolder)
            {
                // Zip only the cataloged files (from TreeView children)
                await Task.Run(() =>
                {
                    using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        // Get all catalog entries from the TreeView node
                        var catalogEntries = linkNode.Children
                            .Where(child => child.Content is LinkItem link && link.IsCatalogEntry)
                            .Select(child => (LinkItem)child.Content)
                            .ToList();

                        foreach (var catalogEntry in catalogEntries)
                        {
                            if (catalogEntry.IsDirectory)
                            {
                                // Add subdirectory and its cataloged contents
                                AddDirectoryToCatalogZip(archive, catalogEntry, linkNode, linkItem.Url);
                            }
                            else if (File.Exists(catalogEntry.Url))
                            {
                                // Add individual file
                                var relativePath = Path.GetRelativePath(linkItem.Url, catalogEntry.Url);
                                archive.CreateEntryFromFile(catalogEntry.Url, relativePath, CompressionLevel.Optimal);
                            }
                        }
                    }
                });

                StatusText.Text = $"Successfully created '{zipFileName}' (cataloged files only)";
            }
            else
            {
                // Zip entire folder contents (normal behavior)
                await Task.Run(() =>
                {
                    ZipFile.CreateFromDirectory(linkItem.Url, zipFilePath, CompressionLevel.Optimal, false);
                });

                StatusText.Text = $"Successfully created '{zipFileName}'";
            }

            // If user wants to link the zip to the parent category
            if (result.LinkToCategory && linkNode.Parent != null)
            {
                var parentCategoryNode = _treeViewService!.GetParentCategoryNode(linkNode);
                if (parentCategoryNode != null)
                {
                    var categoryPath = _treeViewService.GetCategoryPath(parentCategoryNode);

                    // Create a new link for the zip file AS A CATALOG FOLDER
                    var zipLinkItem = new LinkItem
                    {
                        Title = Path.GetFileNameWithoutExtension(zipFileName),
                        Url = zipFilePath,
                        Description = isCatalogFolder 
                            ? $"Zip archive of cataloged files from '{linkItem.Title}'"
                            : $"Zip archive of '{linkItem.Title}'",
                        IsDirectory = true, // Treat zip as a directory/catalog folder
                        CategoryPath = categoryPath,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        FolderType = FolderLinkType.CatalogueFiles, // Make it a catalog folder
                        FileSize = (ulong)new FileInfo(zipFilePath).Length,
                        LastCatalogUpdate = DateTime.Now
                    };

                    var zipLinkNode = new TreeViewNode { Content = zipLinkItem };

                    // Catalog the zip file contents immediately
                    await CatalogZipFileAsync(zipLinkItem, zipLinkNode);

                    // Add the zip link to the parent category
                    parentCategoryNode.Children.Add(zipLinkNode);

                    // Save the updated category
                    var rootNode = GetRootCategoryNode(parentCategoryNode);
                    await _categoryService!.SaveCategoryAsync(rootNode);

                    StatusText.Text = $"Created '{zipFileName}' and linked to '{categoryPath}'";
                }
            }

            // Show success dialog
            var successDialog = new ContentDialog
            {
                Title = "Zip Created Successfully",
                Content = $"The folder has been successfully zipped to:\n\n{zipFilePath}\n\nSize: {FormatFileSize((ulong)new FileInfo(zipFilePath).Length)}"
                    + (isCatalogFolder ? "\n\nNote: Only cataloged files were included." : ""),
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
    /// Creates a catalog of all files inside a zip archive.
    /// </summary>
    private async Task CatalogZipFileAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        if (!File.Exists(zipLinkItem.Url))
        {
            return;
        }

        try
        {
            // Read zip archive on background thread and collect data
            var catalogData = await Task.Run(() =>
            {
                var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
                
                using (var archive = ZipFile.OpenRead(zipLinkItem.Url))
                {
                    // Group entries by their immediate parent directory
                    var rootEntries = archive.Entries
                        .Where(entry => !string.IsNullOrEmpty(entry.Name)) // Skip directory-only entries
                        .Where(entry => !entry.FullName.Contains('/') || entry.FullName.Split('/').Length == 2) // Root level files or first-level subdirs
                        .ToList();

                    foreach (var entry in rootEntries)
                    {
                        entries.Add((
                            entry.Name,
                            entry.FullName,
                            entry.FullName.EndsWith("/"),
                            entry.Length,
                            entry.LastWriteTime.DateTime
                        ));
                    }
                }
                
                return entries;
            });

            // Create TreeViewNode objects on UI thread
            foreach (var (name, fullName, isDirectory, length, lastWrite) in catalogData)
            {
                var entryName = isDirectory 
                    ? fullName.TrimEnd('/').Split('/').Last()
                    : name;

                var catalogEntry = new LinkItem
                {
                    Title = entryName,
                    Url = $"{zipLinkItem.Url}::{fullName}", // Special format for zip entries
                    Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FormatFileSize((ulong)length)})",
                    IsDirectory = isDirectory,
                    CategoryPath = zipLinkItem.CategoryPath,
                    CreatedDate = lastWrite,
                    ModifiedDate = lastWrite,
                    FolderType = FolderLinkType.LinkOnly,
                    IsCatalogEntry = true,
                    FileSize = isDirectory ? null : (ulong)length
                };

                var catalogNode = new TreeViewNode { Content = catalogEntry };

                // If it's a directory, catalog its contents recursively
                if (isDirectory)
                {
                    await CatalogZipDirectoryRecursiveAsync(zipLinkItem.Url, fullName, catalogNode, zipLinkItem);
                }

                zipLinkNode.Children.Add(catalogNode);
            }

            // Update file count - this will populate CatalogFileCount
            _categoryService!.UpdateCatalogFileCount(zipLinkNode);
            
            // Trigger property change notification to update the display text
            if (zipLinkNode.Content is LinkItem linkItem)
            {
                linkItem.RefreshChangeStatus();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error cataloging zip file: {ex.Message}";
        }
    }

    /// <summary>
    /// Recursively catalogs subdirectories within a zip archive.
    /// </summary>
    private async Task CatalogZipDirectoryRecursiveAsync(string zipPath, string directoryPath, TreeViewNode parentNode, LinkItem zipLinkItem)
    {
        // Read subdirectory entries on background thread
        var subCatalogData = await Task.Run(() =>
        {
            var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
            
            var dirPath = directoryPath.TrimEnd('/') + "/";
            
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var subEntries = archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name))
                    .Where(entry => entry.FullName.StartsWith(dirPath) && entry.FullName != dirPath)
                    .Where(entry => entry.FullName.Substring(dirPath.Length).Split('/').Length <= 2) // Direct children only
                    .ToList();

                foreach (var entry in subEntries)
                {
                    entries.Add((
                        entry.Name,
                        entry.FullName,
                        entry.FullName.EndsWith("/"),
                        entry.Length,
                        entry.LastWriteTime.DateTime
                    ));
                }
            }
            
            return entries;
        });

        // Create TreeViewNode objects on UI thread
        foreach (var (name, fullName, isDirectory, length, lastWrite) in subCatalogData)
        {
            var relativePath = fullName.Substring(directoryPath.TrimEnd('/').Length + 1);
            var entryName = isDirectory 
                ? relativePath.TrimEnd('/').Split('/').Last()
                : name;

            var catalogEntry = new LinkItem
            {
                Title = entryName,
                Url = $"{zipLinkItem.Url}::{fullName}",
                Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FormatFileSize((ulong)length)})",
                IsDirectory = isDirectory,
                CategoryPath = zipLinkItem.CategoryPath,
                CreatedDate = lastWrite,
                ModifiedDate = lastWrite,
                FolderType = FolderLinkType.LinkOnly,
                IsCatalogEntry = true,
                FileSize = isDirectory ? null : (ulong)length
            };

            var catalogNode = new TreeViewNode { Content = catalogEntry };

            // Recursively catalog subdirectories
            if (isDirectory)
            {
                await CatalogZipDirectoryRecursiveAsync(zipPath, fullName, catalogNode, zipLinkItem);
            }

            parentNode.Children.Add(catalogNode);
        }
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
