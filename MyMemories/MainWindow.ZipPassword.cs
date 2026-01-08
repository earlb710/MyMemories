using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyMemories.Services;
using MyMemories.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyMemories;

/// <summary>
/// Helper methods for zip password change operations.
/// </summary>
public sealed partial class MainWindow
{
    private async Task ChangeZipPasswordAsync(LinkItem zipLink, TreeViewNode zipNode, bool usePassword, string? password, bool keepBackup)
    {
        var zipFilePath = zipLink.Url;
        var backupPath = zipFilePath + ".backup.zip";
        var tempExtractPath = Path.Combine(Path.GetTempPath(), $"MyMemories_ZipChange_{Guid.NewGuid()}");

        // Verify the original zip file exists
        if (!File.Exists(zipFilePath))
        {
            await ShowErrorDialogAsync("Zip File Not Found", $"The zip file '{zipFilePath}' does not exist.");
            return;
        }

        // CRITICAL: Verify the zipNode has a parent (is not a root node)
        if (zipNode.Parent == null)
        {
            await ShowErrorDialogAsync("Invalid Node Structure", "The zip file node has no parent category. This should not happen.");
            LogUtilities.LogError("MainWindow.ChangeZipPasswordAsync", 
                $"Zip node '{zipLink.Title}' has no parent. Node hierarchy corrupted.", null);
            return;
        }

        // CRITICAL: Store parent node reference BEFORE any tree manipulation
        var parentNode = zipNode.Parent;
        
        // Log the parent structure for debugging
        LogUtilities.LogDebug("MainWindow.ChangeZipPasswordAsync", 
            $"Starting password change. Parent: {(parentNode.Content is CategoryItem cat ? cat.Name : "Unknown")}, " +
            $"ZipNode: {zipLink.Title}");

        // Store original file name to ensure it's preserved
        var originalFileName = Path.GetFileName(zipFilePath);
        var originalDirectory = Path.GetDirectoryName(zipFilePath);

        // Store original catalog state
        bool hadCatalog = zipNode.Children.Any(c => c.Content is LinkItem link && link.IsCatalogEntry);
        
        // Store the original URL temporarily and mark as unavailable
        var originalUrl = zipLink.Url;
        zipLink.Url = string.Empty; // Temporarily clear URL to prevent file access attempts
        
        // Remove all child nodes and add busy indicator
        TreeViewNode? busyNode = null;
        zipNode.Children.Clear();
        
        var categoryPath = _treeViewService!.GetCategoryPath(parentNode);
        var busyLinkItem = new LinkItem
        {
            Title = "? Changing password...",
            Url = string.Empty,
            Description = "Please wait while the zip password is being changed",
            IsDirectory = false,
            CategoryPath = categoryPath,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };
        
        busyNode = new TreeViewNode { Content = busyLinkItem };
        zipNode.Children.Add(busyNode);
        zipNode.IsExpanded = true;

        try
        {
            StatusText.Text = $"Changing password for '{zipLink.Title}'...";

            // Step 1: Create backup (overwrite if exists)
            // Check if backup already exists
            if (File.Exists(backupPath))
            {
                StatusText.Text = $"Overwriting existing backup...";
                try
                {
                    File.Delete(backupPath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Cannot overwrite existing backup file: {ex.Message}. Please delete '{Path.GetFileName(backupPath)}' manually and try again.");
                }
            }
            else
            {
                StatusText.Text = $"Creating backup: {Path.GetFileName(backupPath)}";
            }

            // Move original to backup
            File.Move(originalUrl, backupPath);

            try
            {
                // Step 2: Extract original zip to temp
                StatusText.Text = "Extracting original zip...";
                Directory.CreateDirectory(tempExtractPath);

                // Determine if original was password protected
                var originalPassword = zipLink.IsZipPasswordProtected
                    ? await GetOriginalZipPasswordAsync(zipLink, zipNode)
                    : null;

                if (zipLink.IsZipPasswordProtected && string.IsNullOrEmpty(originalPassword))
                {
                    throw new Exception("Original zip password required but not available");
                }

                await ExtractZipAsync(backupPath, tempExtractPath, originalPassword);

                // Step 3: Create new zip with new password settings
                // IMPORTANT: Use the exact same zipFilePath to preserve the filename
                StatusText.Text = "Creating new zip with updated password...";
                var compressionLevel = _configService?.ZipCompressionLevel ?? 2;

                if (usePassword && !string.IsNullOrEmpty(password))
                {
                    // Create password-protected zip with EXACT same filename
                    var success = await ZipUtilities.CreatePasswordProtectedZipAsync(
                        tempExtractPath, 
                        zipFilePath,  // Use original path to maintain filename
                        password, 
                        compressionLevel);
                    
                    if (!success)
                    {
                        throw new Exception("Failed to create password-protected zip file");
                    }
                }
                else
                {
                    // Create standard zip with EXACT same filename
                    await Task.Run(() =>
                    {
                        System.IO.Compression.ZipFile.CreateFromDirectory(
                            tempExtractPath,
                            zipFilePath,  // Use original path to maintain filename
                            (System.IO.Compression.CompressionLevel)Math.Min(compressionLevel, 2),
                            false);
                    });
                }

                // Verify the new zip was created with the correct name
                if (!File.Exists(zipFilePath))
                {
                    throw new Exception($"New zip file was not created at expected location: {zipFilePath}");
                }

                var newFileName = Path.GetFileName(zipFilePath);
                if (newFileName != originalFileName)
                {
                    throw new Exception($"Filename mismatch! Expected: '{originalFileName}', Got: '{newFileName}'");
                }

                // Step 4: Update link metadata and restore URL
                zipLink.IsZipPasswordProtected = usePassword;
                zipLink.FileSize = (ulong)new FileInfo(zipFilePath).Length;
                zipLink.ModifiedDate = DateTime.Now;
                zipLink.Url = originalUrl; // Restore the URL now that the file exists

                // Step 5: Remove busy node
                if (busyNode != null)
                {
                    zipNode.Children.Remove(busyNode);
                }

                // Step 6: Re-catalog if it was cataloged
                if (hadCatalog)
                {
                    StatusText.Text = "Re-cataloging zip contents...";
                    await _catalogService!.CreateCatalogAsync(zipLink, zipNode);
                }

                // Step 7: Save category - Use the stored parentNode reference
                // This is safe because we stored it before any tree manipulation
                LogUtilities.LogDebug("MainWindow.ChangeZipPasswordAsync", 
                    $"About to save category. Parent is null: {parentNode == null}");
                
                var rootNode = GetRootCategoryNode(parentNode);
                await _categoryService!.SaveCategoryAsync(rootNode);

                // Step 8: Delete backup if not keeping
                if (!keepBackup && File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    StatusText.Text = $"Password changed successfully for '{zipLink.Title}'";
                }
                else
                {
                    StatusText.Text = $"Password changed successfully for '{zipLink.Title}' (backup kept)";
                }

                // Refresh node visual
                RefreshNodeVisual(zipNode);

                // Show success
                var successDialog = new ContentDialog
                {
                    Title = "Password Changed Successfully",
                    Content = $"The zip file password has been updated.\n\n" +
                             $"File: {originalFileName}\n" +
                             $"New Size: {Services.FileViewerService.FormatFileSize(zipLink.FileSize ?? 0)}\n" +
                             (keepBackup ? $"\n?? Backup saved as:\n{Path.GetFileName(backupPath)}" : "\n? Backup deleted (original overwritten)"),
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (Exception innerEx)
            {
                // Error during processing - restore backup
                StatusText.Text = $"Error occurred, restoring original...";

                // Remove busy node if still there
                if (busyNode != null && zipNode.Children.Contains(busyNode))
                {
                    zipNode.Children.Remove(busyNode);
                }

                // Delete partial new zip if it exists
                if (File.Exists(zipFilePath))
                {
                    try { File.Delete(zipFilePath); } catch { }
                }

                // Restore backup with ORIGINAL filename
                if (File.Exists(backupPath))
                {
                    File.Move(backupPath, originalUrl);
                }

                // Restore URL
                zipLink.Url = originalUrl;

                // Restore catalog if it had one
                if (hadCatalog)
                {
                    try
                    {
                        await _catalogService!.CreateCatalogAsync(zipLink, zipNode);
                    }
                    catch
                    {
                        // Failed to restore catalog, but backup is restored
                    }
                }

                throw new Exception($"Failed to change password: {innerEx.Message}", innerEx);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to change password for '{zipLink.Title}'";

            // Restore URL if it was cleared
            if (string.IsNullOrEmpty(zipLink.Url))
            {
                zipLink.Url = originalUrl;
            }

            // Remove busy node if still there
            if (busyNode != null && zipNode.Children.Contains(busyNode))
            {
                zipNode.Children.Remove(busyNode);
            }

            var errorDialog = new ContentDialog
            {
                Title = "Error Changing Password",
                Content = $"An error occurred while changing the zip password:\n\n{ex.Message}\n\n" +
                         "The original zip file has been restored.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
            }
            catch { }
        }
    }

    private async Task<string?> GetOriginalZipPasswordAsync(LinkItem zipLink, TreeViewNode zipNode)
    {
        // Try to get password from category or global
        var rootCategoryNode = GetRootCategoryNode(zipNode);
        var rootCategory = rootCategoryNode?.Content as CategoryItem;

        if (rootCategory?.PasswordProtection == PasswordProtectionType.GlobalPassword)
        {
            return _categoryService?.GetCachedGlobalPassword();
        }
        else if (rootCategory?.PasswordProtection == PasswordProtectionType.OwnPassword)
        {
            return await GetCategoryPasswordAsync(rootCategory);
        }

        // If no category password, prompt user
        var passwordDialog = new ContentDialog
        {
            Title = "Original Zip Password Required",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Enter the password for the original zip file '{zipLink.Title}':",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    new PasswordBox
                    {
                        Name = "OriginalPasswordInput",
                        PlaceholderText = "Enter original password"
                    }
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await passwordDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var passwordBox = (passwordDialog.Content as StackPanel)
                ?.Children.OfType<PasswordBox>()
                .FirstOrDefault();

            return passwordBox?.Password;
        }

        return null;
    }

    private async Task ExtractZipAsync(string zipPath, string extractPath, string? password)
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(password))
            {
                // Try standard extraction first
                try
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);
                    return;
                }
                catch (InvalidDataException)
                {
                    // Might be a non-password-protected SharpZipLib created zip
                    // Fall through to SharpZipLib extraction
                }
            }

            // Use SharpZipLib for all password-protected zips OR if standard extraction failed
            using var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipPath);
            
            if (!string.IsNullOrEmpty(password))
            {
                zipFile.Password = password;
            }

            foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zipFile)
            {
                if (!entry.IsFile) continue;

                var entryFileName = entry.Name;
                var fullPath = Path.Combine(extractPath, entryFileName);
                var directoryName = Path.GetDirectoryName(fullPath);

                if (!string.IsNullOrEmpty(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                using var zipStream = zipFile.GetInputStream(entry);
                using var fileStream = File.Create(fullPath);
                zipStream.CopyTo(fileStream);
            }
        });
    }
}
