using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MyMemories.Utilities;

namespace MyMemories.Services;

public class ZipCatalogService
{
    private readonly CategoryService _categoryService;
    private readonly TreeViewService _treeViewService;

    public ZipCatalogService(CategoryService categoryService, TreeViewService treeViewService)
    {
        _categoryService = categoryService;
        _treeViewService = treeViewService;
    }

    /// <summary>
    /// Creates a catalog of all files inside a zip archive.
    /// </summary>
    public async Task CatalogZipFileAsync(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        if (!File.Exists(zipLinkItem.Url))
        {
            Debug.WriteLine($"[CatalogZipFileAsync] Zip file not found: {zipLinkItem.Url}");
            return;
        }

        try
        {
            // CRITICAL: Store the zip path at the start to ensure consistency
            var zipPath = zipLinkItem.Url;
            
            Debug.WriteLine($"[CatalogZipFileAsync] Cataloging zip file: {zipPath}");

            // Wait for file to be fully available with retry logic
            int maxWaitAttempts = 10;
            bool fileReady = false;
            
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                try
                {
                    // Try to open the file exclusively to verify it's not locked
                    using (var testStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // Check if file has minimum valid zip size (end of central directory = 22 bytes)
                        if (testStream.Length < 22)
                        {
                            Debug.WriteLine($"[CatalogZipFileAsync] Zip file too small ({testStream.Length} bytes), waiting...");
                            throw new IOException("Zip file not ready");
                        }
                        
                        // Look for end of central directory signature (PK\x05\x06)
                        // It can be anywhere in the last 65557 bytes (65535 comment + 22 EOCD)
                        var searchLength = Math.Min(testStream.Length, 65557);
                        testStream.Seek(-searchLength, SeekOrigin.End);
                        
                        var buffer = new byte[searchLength];
                        var bytesRead = testStream.Read(buffer, 0, (int)searchLength);
                        
                        bool found = false;
                        // Search backwards for the signature
                        for (int j = bytesRead - 22; j >= 0; j--)
                        {
                            if (buffer[j] == 0x50 && buffer[j + 1] == 0x4b && 
                                buffer[j + 2] == 0x05 && buffer[j + 3] == 0x06)
                            {
                                found = true;
                                Debug.WriteLine($"[CatalogZipFileAsync] Found EOCD signature at offset {j} from search start");
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            Debug.WriteLine($"[CatalogZipFileAsync] Cannot find central directory signature in {bytesRead} bytes, waiting...");
                            throw new IOException("Central directory not found");
                        }
                        
                        Debug.WriteLine($"[CatalogZipFileAsync] Zip file is ready (size: {testStream.Length} bytes)");
                        fileReady = true;
                    }
                    break; // File is ready
                }
                catch (IOException ex) when (i < maxWaitAttempts - 1)
                {
                    Debug.WriteLine($"[CatalogZipFileAsync] Attempt {i + 1}/{maxWaitAttempts}: {ex.Message}");
                    await Task.Delay(500 * (i + 1)); // Increasing delay: 500ms, 1s, 1.5s, etc.
                    
                    // Force GC to release any handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
            
            if (!fileReady)
            {
                throw new IOException($"Zip file is not valid after {maxWaitAttempts} attempts. The central directory could not be found.");
            }

            // Read zip archive on background thread and collect data
            var catalogData = await Task.Run(() =>
            {
                var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
                
                try
                {
                    // Try standard .NET ZipFile first - USE zipPath
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        var rootEntries = archive.Entries
                            .Where(entry => !string.IsNullOrEmpty(entry.Name))
                            .Where(entry => !entry.FullName.Contains('/') || entry.FullName.Split('/').Length == 2)
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
                }
                catch (InvalidDataException)
                {
                    // This is likely a password-protected zip, try SharpZipLib
                    Debug.WriteLine($"[CatalogZipFileAsync] Standard ZipFile failed, trying SharpZipLib for password-protected zip");
                    
                    using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipPath))
                    {
                        // Note: For password-protected zips, we can enumerate entries but not read their contents
                        // The password would be needed to extract/view files
                        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zipFile)
                        {
                            // Only get root-level entries
                            var entryPath = entry.Name.Replace('\\', '/');
                            var pathParts = entryPath.Split('/');
                            
                            // Root level: either no slashes, or one slash at end (directory), or exactly one path segment before file
                            bool isRootLevel = pathParts.Length == 1 || 
                                             (pathParts.Length == 2 && string.IsNullOrEmpty(pathParts[1]));
                            

                            if (isRootLevel && !string.IsNullOrEmpty(entry.Name))
                            {
                                entries.Add((
                                    Path.GetFileName(entry.Name.TrimEnd('/', '\\')),
                                    entryPath,
                                    entry.IsDirectory,
                                    entry.Size,
                                    entry.DateTime
                                ));
                            }
                        }
                    }
                }
                
                return entries;
            });

            Debug.WriteLine($"[CatalogZipFileAsync] Found {catalogData.Count} root entries");

            // Create TreeViewNode objects on UI thread
            foreach (var (name, fullName, isDirectory, length, lastWrite) in catalogData)
            {
                var entryName = isDirectory 
                    ? fullName.TrimEnd('/').Split('/').Last()
                    : name;

                var catalogEntry = new LinkItem
                {
                    Title = entryName,
                    Url = $"{zipPath}::{fullName}",
                    Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FileUtilities.FormatFileSize((ulong)length)})",
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
                    await CatalogZipDirectoryRecursiveAsync(zipPath, fullName, catalogNode, zipLinkItem);
                }

                zipLinkNode.Children.Add(catalogNode);
            }

            // Update file count
            _categoryService.UpdateCatalogFileCount(zipLinkNode);
            
            // Trigger property change notification
            if (zipLinkNode.Content is LinkItem linkItem)
            {
                linkItem.RefreshChangeStatus();
            }

            Debug.WriteLine($"[CatalogZipFileAsync] Successfully cataloged zip with {zipLinkNode.Children.Count} entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogZipFileAsync] Error cataloging zip file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Recursively catalogs subdirectories within a zip archive.
    /// </summary>
    private async Task CatalogZipDirectoryRecursiveAsync(string zipPath, string directoryPath, TreeViewNode parentNode, LinkItem zipLinkItem)
    {
        Debug.WriteLine($"[CatalogZipDirectoryRecursiveAsync] Cataloging subdirectory: {directoryPath}");

        var subCatalogData = await Task.Run(() =>
        {
            var entries = new List<(string name, string fullName, bool isDirectory, long length, DateTime lastWrite)>();
            
            var dirPath = directoryPath.TrimEnd('/') + "/";
            
            try
            {
                // Try standard .NET ZipFile first
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var subEntries = archive.Entries
                        .Where(entry => !string.IsNullOrEmpty(entry.Name))
                        .Where(entry => entry.FullName.StartsWith(dirPath) && entry.FullName != dirPath)
                        .Where(entry => entry.FullName.Substring(dirPath.Length).Split('/').Length <= 2)
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
            }
            catch (InvalidDataException)
            {
                // Password-protected zip, use SharpZipLib
                Debug.WriteLine($"[CatalogZipDirectoryRecursiveAsync] Using SharpZipLib for password-protected zip");
                
                using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipPath))
                {
                    foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zipFile)
                    {
                        var entryPath = entry.Name.Replace('\\', '/');
                        
                        if (entryPath.StartsWith(dirPath) && entryPath != dirPath)
                        {
                            var relativePath = entryPath.Substring(dirPath.Length);
                            var pathParts = relativePath.Split('/');
                            
                            // Direct children only
                            bool isDirectChild = pathParts.Length == 1 || 
                                               (pathParts.Length == 2 && string.IsNullOrEmpty(pathParts[1]));

                            if (isDirectChild)
                            {
                                entries.Add((
                                    Path.GetFileName(entry.Name.TrimEnd('/', '\\')),
                                    entryPath,
                                    entry.IsDirectory,
                                    entry.Size,
                                    entry.DateTime
                                ));
                            }
                        }
                    }
                }
            }
            
            return entries;
        });

        Debug.WriteLine($"[CatalogZipDirectoryRecursiveAsync] Found {subCatalogData.Count} entries in {directoryPath}");

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
                Url = $"{zipPath}::{fullName}",
                Description = isDirectory ? "Folder in zip archive" : $"File in zip archive ({FileUtilities.FormatFileSize((ulong)length)})",
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
}