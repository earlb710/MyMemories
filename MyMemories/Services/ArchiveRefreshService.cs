using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Services;

/// <summary>
/// Service for refreshing and managing zip archive operations.
/// Extracted from MainWindow.Helpers.cs for better separation of concerns.
/// </summary>
public class ArchiveRefreshService
{
    private readonly CategoryService _categoryService;
    private readonly CatalogService _catalogService;
    private readonly TreeViewService _treeViewService;

    /// <summary>
    /// Helper class to store folder and category information.
    /// </summary>
    public class FolderCategoryInfo
    {
        public string FolderPath { get; set; } = string.Empty;
        public string FolderTitle { get; set; } = string.Empty;
        public string CategoryPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    /// <summary>
    /// Result of an archive refresh operation.
    /// </summary>
    public class ArchiveRefreshResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
        public string? ZipFilePath { get; set; }
        public ulong? FileSize { get; set; }
        public string? CategoryName { get; set; }
    }

    public ArchiveRefreshService(
        CategoryService categoryService,
        CatalogService catalogService,
        TreeViewService treeViewService)
    {
        _categoryService = categoryService;
        _catalogService = catalogService;
        _treeViewService = treeViewService;
    }

    /// <summary>
    /// Checks if a zip file contains a manifest and extracts the root category name.
    /// </summary>
    public async Task<string?> GetManifestRootCategoryAsync(string zipFilePath, string? password = null)
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

                    return ParseManifestRootCategory(content);
                }
                catch (InvalidDataException)
                {
                    // Fallback to SharpZipLib for unsupported compression methods or encrypted zips
                    return TryReadManifestWithSharpZipLib(zipFilePath, password);
                }
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the manifest content to extract the root category name.
    /// </summary>
    private static string? ParseManifestRootCategory(string content)
    {
        var match = Regex.Match(content, @"Root Category:\s*(.+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Attempts to read manifest using SharpZipLib (for encrypted or special compression).
    /// </summary>
    private static string? TryReadManifestWithSharpZipLib(string zipFilePath, string? password)
    {
        try
        {
            using var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipFilePath);

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

            return ParseManifestRootCategory(content);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Re-creates a zip file from a category.
    /// Uses SharpZipLib for maximum compatibility.
    /// </summary>
    public async Task ReZipCategoryAsync(
        TreeViewNode categoryNode,
        string zipFileName,
        string targetDirectory,
        string? password = null)
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

        // Ensure .zip extension
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        var zipFilePath = Path.Combine(targetDirectory, zipFileName);

        // Delete existing file with retry logic
        await DeleteExistingZipWithRetryAsync(zipFilePath);

        // Generate manifest content
        var manifestContent = GenerateManifestContent(folderInfoList, category.Name);

        // Create new zip with manifest using SharpZipLib
        await CreateZipArchiveAsync(zipFilePath, folderPaths, manifestContent, password);

        // Verify the file was created and is valid
        ValidateCreatedZipFile(zipFilePath);
    }

    /// <summary>
    /// Deletes an existing zip file with retry logic for locked files.
    /// </summary>
    private static async Task DeleteExistingZipWithRetryAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            return;

        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.Delete(zipFilePath);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(500);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    /// <summary>
    /// Creates the zip archive with all folders and manifest.
    /// </summary>
    private static async Task CreateZipArchiveAsync(
        string zipFilePath,
        string[] folderPaths,
        string manifestContent,
        string? password)
    {
        await Task.Run(() =>
        {
            using var fileStream = new FileStream(
                zipFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);

            using var zipOutputStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fileStream, 8192);

            zipOutputStream.IsStreamOwner = false;
            zipOutputStream.SetLevel(6);

            if (!string.IsNullOrEmpty(password))
            {
                zipOutputStream.Password = password;
                zipOutputStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
            }

            // Add manifest
            AddManifestToZip(zipOutputStream, manifestContent, password);

            // Add all folder contents
            foreach (var folderPath in folderPaths)
            {
                AddFolderToZip(zipOutputStream, folderPath, password);
            }

            zipOutputStream.Finish();
            zipOutputStream.Flush();
            zipOutputStream.Close();

            fileStream.Flush(true);
            fileStream.Close();
        });
    }

    /// <summary>
    /// Adds the manifest file to the zip archive.
    /// </summary>
    private static void AddManifestToZip(
        ICSharpCode.SharpZipLib.Zip.ZipOutputStream zipOutputStream,
        string manifestContent,
        string? password)
    {
        var manifestBytes = Encoding.UTF8.GetBytes(manifestContent);

        var manifestEntry = new ICSharpCode.SharpZipLib.Zip.ZipEntry("_MANIFEST.txt")
        {
            DateTime = DateTime.Now,
            Size = manifestBytes.Length
        };

        if (!string.IsNullOrEmpty(password))
        {
            manifestEntry.AESKeySize = 256;
        }

        zipOutputStream.PutNextEntry(manifestEntry);
        zipOutputStream.Write(manifestBytes, 0, manifestBytes.Length);
        zipOutputStream.CloseEntry();
    }

    /// <summary>
    /// Adds a folder and its contents to the zip archive.
    /// </summary>
    private static void AddFolderToZip(
        ICSharpCode.SharpZipLib.Zip.ZipOutputStream zipOutputStream,
        string folderPath,
        string? password)
    {
        if (!Directory.Exists(folderPath))
            return;

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

                if (!string.IsNullOrEmpty(password))
                {
                    entry.AESKeySize = 256;
                }

                zipOutputStream.PutNextEntry(entry);

                using var inputFileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                inputFileStream.CopyTo(zipOutputStream);

                zipOutputStream.CloseEntry();
            }
            catch
            {
                // Continue with other files
            }
        }
    }

    /// <summary>
    /// Validates that a created zip file is valid.
    /// </summary>
    private static void ValidateCreatedZipFile(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new IOException($"Zip file was not created: {zipFilePath}");
        }

        var createdFileInfo = new FileInfo(zipFilePath);

        // Minimum valid zip file size (empty zip with end of central directory)
        if (createdFileInfo.Length < 22)
        {
            throw new IOException($"Zip file is too small to be valid: {createdFileInfo.Length} bytes");
        }
    }

    /// <summary>
    /// Collects folder information including their category paths.
    /// </summary>
    public List<FolderCategoryInfo> CollectFolderInfoFromCategory(TreeViewNode categoryNode, string parentCategoryPath)
    {
        var folderInfoList = new List<FolderCategoryInfo>();

        foreach (var child in categoryNode.Children)
        {
            if (child.Content is LinkItem link)
            {
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
    public string GenerateManifestContent(List<FolderCategoryInfo> folderInfoList, string rootCategoryName)
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
    /// Catalogs a zip file with retry logic for file locking issues.
    /// </summary>
    public async Task<(bool Success, Exception? Error)> CatalogZipWithRetryAsync(
        LinkItem zipLinkItem,
        TreeViewNode zipLinkNode,
        int maxRetries = 3,
        int initialDelayMs = 500)
    {
        int retryDelay = initialDelayMs;
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // Exponential backoff
                }

                await _catalogService.CreateCatalogAsync(zipLinkItem, zipLinkNode);
                return (true, null);
            }
            catch (ICSharpCode.SharpZipLib.Zip.ZipException ex)
                when (ex.Message.Contains("Cannot find central directory") && attempt < maxRetries - 1)
            {
                lastException = ex;
            }
            catch (InvalidDataException ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
            }
            catch (IOException ex)
                when (ex.Message.Contains("being used by another process") && attempt < maxRetries - 1)
            {
                lastException = ex;
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        return (false, lastException);
    }

    /// <summary>
    /// Updates a zip link item after successful refresh.
    /// </summary>
    public void UpdateZipLinkAfterRefresh(LinkItem zipLinkItem, TreeViewNode zipLinkNode)
    {
        zipLinkItem.LastCatalogUpdate = DateTime.Now;
        zipLinkItem.FileSize = (ulong)new FileInfo(zipLinkItem.Url).Length;
        _categoryService.UpdateCatalogFileCount(zipLinkNode);
    }

    /// <summary>
    /// Removes catalog entries from a zip node to release file handles.
    /// </summary>
    public void RemoveCatalogEntries(TreeViewNode zipLinkNode)
    {
        _categoryService.RemoveCatalogEntries(zipLinkNode);
    }

    /// <summary>
    /// Refreshes a link node with updated item data.
    /// </summary>
    public TreeViewNode RefreshLinkNode(TreeViewNode node, LinkItem linkItem)
    {
        return _treeViewService.RefreshLinkNode(node, linkItem);
    }
}
