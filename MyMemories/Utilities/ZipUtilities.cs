using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace MyMemories.Utilities;

public static class ZipUtilities
{
    /// <summary>
    /// Parses a zip entry URL (format: "zipPath::entryPath" or legacy "zipPath\entryPath").
    /// Returns (zipPath, entryPath) or (null, null) if invalid.
    /// </summary>
    public static (string? zipPath, string? entryPath) ParseZipEntryUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return (null, null);

        string? zipPath = null;
        string? entryPath = null;

        // Try new format first (with ::)
        if (url.Contains("::"))
        {
            var parts = url.Split("::", 2);
            if (parts.Length == 2)
            {
                zipPath = parts[0]?.Trim();
                entryPath = parts[1]?.Trim();
            }
        }
        else
        {
            // Try legacy format: find .zip followed by path separator
            var zipIndex = url.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (zipIndex > 0)
            {
                var afterZip = zipIndex + 4;
                if (afterZip < url.Length && (url[afterZip] == '\\' || url[afterZip] == '/'))
                {
                    zipPath = url.Substring(0, afterZip);
                    entryPath = url.Substring(afterZip + 1);
                }
            }
        }

        if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(entryPath))
            return (null, null);

        // Fix corrupted URLs where the zip filename is duplicated
        if (zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !File.Exists(zipPath))
        {
            var lastBackslashIndex = zipPath.LastIndexOf('\\');
            if (lastBackslashIndex > 0)
            {
                var potentialZipPath = zipPath.Substring(0, lastBackslashIndex);
                if (potentialZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(potentialZipPath))
                {
                    zipPath = potentialZipPath;
                }
            }
        }

        // Validate that the zip path doesn't contain :: (another corruption pattern)
        if (zipPath.Contains("::"))
        {
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                zipPath = zipPath.Substring(0, lastZipIndex + 4);
            }
        }

        // Final validation - try to find the .zip file in the path
        if (!File.Exists(zipPath))
        {
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                var attemptPath = zipPath.Substring(0, lastZipIndex + 4);
                if (File.Exists(attemptPath))
                {
                    zipPath = attemptPath;
                }
            }
        }

        return (zipPath, entryPath);
    }

    /// <summary>
    /// Checks if a URL represents a zip entry (contains "::" or has .zip followed by path separator).
    /// </summary>
    public static bool IsZipEntryUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Check for new format with ::
        if (url.Contains("::"))
            return true;

        // Check for legacy format: .zip followed by \ or /
        var zipIndex = url.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
        if (zipIndex > 0)
        {
            var afterZip = zipIndex + 4;
            if (afterZip < url.Length && (url[afterZip] == '\\' || url[afterZip] == '/'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts a zip entry to a memory stream for viewing.
    /// Returns null if the entry doesn't exist or cannot be read.
    /// </summary>
    public static async Task<Stream?> ExtractZipEntryToStreamAsync(string zipPath, string entryPath, string? password = null)
    {
        try
        {
            if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(entryPath))
            {
                LogUtilities.LogWarning("ZipUtilities.ExtractZipEntryToStreamAsync", $"Empty zipPath or entryPath: zipPath='{zipPath}', entryPath='{entryPath}'");
                return null;
            }

            if (!File.Exists(zipPath))
            {
                LogUtilities.LogWarning("ZipUtilities.ExtractZipEntryToStreamAsync", $"Zip file not found: {zipPath}");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] zipPath: {zipPath}");
            System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] entryPath: {entryPath}");

            return await Task.Run(() =>
            {
                try
                {
                    // Try standard .NET ZipArchive first (for non-password-protected)
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                    {
                        // Try multiple path variations
                        var normalizedEntryPath = entryPath.Replace('\\', '/');
                        var backslashEntryPath = entryPath.Replace('/', '\\');
                        
                        System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Looking for entry: '{entryPath}' or '{normalizedEntryPath}'");
                        System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Archive has {archive.Entries.Count} entries");
                        
                        // Log first few entries for debugging
                        int logged = 0;
                        foreach (var e in archive.Entries)
                        {
                            if (logged < 5)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Entry[{logged}]: '{e.FullName}'");
                                logged++;
                            }
                        }
                        
                        var entry = archive.GetEntry(normalizedEntryPath) 
                                 ?? archive.GetEntry(entryPath)
                                 ?? archive.GetEntry(backslashEntryPath);
                        
                        // If still not found, try case-insensitive search
                        if (entry == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Entry not found by exact name, trying case-insensitive search");
                            foreach (var e in archive.Entries)
                            {
                                if (e.FullName.Equals(normalizedEntryPath, StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.Equals(entryPath, StringComparison.OrdinalIgnoreCase) ||
                                    e.Name.Equals(entryPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    entry = e;
                                    System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Found entry via case-insensitive match: '{e.FullName}'");
                                    break;
                                }
                            }
                        }

                        if (entry == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Entry not found in archive");
                            return null;
                        }

                        System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Found entry: '{entry.FullName}', Length: {entry.Length}");

                        if (entry.Length == 0)
                            return new MemoryStream();

                        var memoryStream = new MemoryStream((int)entry.Length);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.CopyTo(memoryStream);
                        }

                        memoryStream.Position = 0;
                        System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Successfully extracted {memoryStream.Length} bytes");
                        return (Stream)memoryStream;
                    }
                }
                catch (InvalidDataException)
                {
                    // Password-protected zip - use SharpZipLib with password
                    System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Standard ZipFile failed, trying SharpZipLib");
                    
                    if (string.IsNullOrEmpty(password))
                    {
                        LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", 
                            "Cannot extract from password-protected zip without password", null);
                        return null;
                    }

                    try
                    {
                        using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipPath))
                        {
                            zipFile.Password = password;

                            var normalizedEntryPath = entryPath.Replace('\\', '/');
                            var entry = zipFile.GetEntry(normalizedEntryPath);

                            if (entry == null)
                                entry = zipFile.GetEntry(entryPath);
                            
                            // Try case-insensitive search
                            if (entry == null)
                            {
                                foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry e in zipFile)
                                {
                                    if (e.Name.Equals(normalizedEntryPath, StringComparison.OrdinalIgnoreCase) ||
                                        e.Name.Equals(entryPath, StringComparison.OrdinalIgnoreCase) ||
                                        Path.GetFileName(e.Name).Equals(entryPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        entry = e;
                                        break;
                                    }
                                }
                            }

                            if (entry == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ExtractZipEntryToStreamAsync] Entry not found in password-protected archive");
                                return null;
                            }

                            if (entry.Size == 0)
                                return new MemoryStream();

                            var memoryStream = new MemoryStream((int)entry.Size);
                            
                            using (var zipStream = zipFile.GetInputStream(entry))
                            {
                                zipStream.CopyTo(memoryStream);
                            }

                            memoryStream.Position = 0;
                            return (Stream)memoryStream;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", 
                            "Error extracting from password-protected zip", ex);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", $"Unexpected error: {ex.Message}", ex);
                    return null;
                }
            });
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", "Outer exception", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets the file extension from a zip entry path.
    /// </summary>
    public static string GetZipEntryExtension(string entryPath)
    {
        if (string.IsNullOrEmpty(entryPath))
            return string.Empty;

        try
        {
            return Path.GetExtension(entryPath).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Validates that a zip file is readable and not corrupted.
    /// </summary>
    public static bool ValidateZipFile(string zipPath)
    {
        if (!File.Exists(zipPath))
            return false;

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var count = 0;
            foreach (var entry in archive.Entries)
            {
                count++;
                if (count > 0) break;
            }
            return true;
        }
        catch (InvalidDataException)
        {
            // Might be password-protected, try SharpZipLib
            try
            {
                using var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(zipPath);
                return zipFile.Count > 0;
            }
            catch (Exception ex)
            {
                LogUtilities.LogError("ZipUtilities.ValidateZipFile", $"Invalid zip file: {zipPath}", ex);
                return false;
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipUtilities.ValidateZipFile", "Error validating zip", ex);
            return false;
        }
    }

    /// <summary>
    /// Gets entry information without extracting it.
    /// </summary>
    public static (bool exists, long size, DateTime modified) GetEntryInfo(string zipPath, string entryPath)
    {
        try
        {
            if (!File.Exists(zipPath))
                return (false, 0, DateTime.MinValue);

            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var normalizedPath = entryPath.Replace('\\', '/');
            var entry = archive.GetEntry(normalizedPath) ?? archive.GetEntry(entryPath);

            if (entry == null)
                return (false, 0, DateTime.MinValue);

            return (true, entry.Length, entry.LastWriteTime.DateTime);
        }
        catch
        {
            return (false, 0, DateTime.MinValue);
        }
    }

    /// <summary>
    /// Creates a password-protected zip file from a directory using SharpZipLib.
    /// </summary>
    public static async Task<bool> CreatePasswordProtectedZipAsync(
        string sourceDirectory, 
        string zipFilePath, 
        string? password = null,
        int compressionLevel = 6)
    {
        if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipAsync", $"Source directory not found: {sourceDirectory}");
            return false;
        }

        if (string.IsNullOrEmpty(zipFilePath))
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipAsync", "Zip file path is null or empty");
            return false;
        }

        try
        {
            var outputDir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await Task.Run(() =>
            {
                using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                zipStream.SetLevel(Math.Clamp(compressionLevel, 0, 9));

                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
                }

                var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                var baseDirectory = sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                foreach (var filePath in files)
                {
                    try
                    {
                        var entryName = filePath.Substring(baseDirectory.Length + 1)
                            .Replace(Path.DirectorySeparatorChar, '/');

                        var fileInfo = new FileInfo(filePath);
                        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(entryName)
                        {
                            DateTime = fileInfo.LastWriteTime,
                            Size = fileInfo.Length
                        };

                        if (!string.IsNullOrEmpty(password))
                        {
                            entry.AESKeySize = 256;
                        }

                        zipStream.PutNextEntry(entry);

                        using var fileStream = File.OpenRead(filePath);
                        fileStream.CopyTo(zipStream);

                        zipStream.CloseEntry();
                    }
                    catch (Exception ex)
                    {
                        LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipAsync", $"Error adding file {filePath}", ex);
                    }
                }

                zipStream.Finish();
            });

            LogUtilities.LogInfo("ZipUtilities.CreatePasswordProtectedZipAsync", $"Successfully created zip: {zipFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipAsync", "Error creating zip", ex);
            
            try
            {
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
            }
            catch { }

            return false;
        }
    }

    /// <summary>
    /// Creates a password-protected zip file from a single file using SharpZipLib.
    /// </summary>
    public static async Task<bool> CreatePasswordProtectedZipFromFileAsync(
        string sourceFilePath, 
        string zipFilePath, 
        string? password = null,
        int compressionLevel = 6)
    {
        if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipFromFileAsync", $"Source file not found: {sourceFilePath}");
            return false;
        }

        if (string.IsNullOrEmpty(zipFilePath))
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipFromFileAsync", "Zip file path is null or empty");
            return false;
        }

        try
        {
            var outputDir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await Task.Run(() =>
            {
                using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                zipStream.SetLevel(Math.Clamp(compressionLevel, 0, 9));

                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
                }

                var fileName = Path.GetFileName(sourceFilePath);
                var fileInfo = new FileInfo(sourceFilePath);

                var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(fileName)
                {
                    DateTime = fileInfo.LastWriteTime,
                    Size = fileInfo.Length
                };

                if (!string.IsNullOrEmpty(password))
                {
                    entry.AESKeySize = 256;
                }

                zipStream.PutNextEntry(entry);

                using var fileStream = File.OpenRead(sourceFilePath);
                fileStream.CopyTo(zipStream);

                zipStream.CloseEntry();
                zipStream.Finish();
            });

            LogUtilities.LogInfo("ZipUtilities.CreatePasswordProtectedZipFromFileAsync", $"Successfully created zip: {zipFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedZipFromFileAsync", "Error creating zip", ex);
            
            try
            {
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
            }
            catch { }

            return false;
        }
    }

    /// <summary>
    /// Creates a password-protected zip file from multiple folders using SharpZipLib.
    /// Note: This method does NOT include manifest generation.
    /// </summary>
    public static async Task CreatePasswordProtectedMultiFolderZipAsync(
        string[] folderPaths, 
        string zipFilePath, 
        string password,
        int compressionLevel = 6)
    {
        await Task.Run(() =>
        {
            using var outputStream = new FileStream(zipFilePath, FileMode.Create);
            using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

            zipStream.SetLevel(Math.Clamp(compressionLevel, 0, 9));
            zipStream.Password = password;
            zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;

            foreach (var folderPath in folderPaths)
            {
                if (!Directory.Exists(folderPath))
                    continue;

                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                var folderName = new DirectoryInfo(folderPath).Name;

                foreach (var filePath in files)
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(folderPath, filePath);
                        var entryName = Path.Combine(folderName, relativePath).Replace(Path.DirectorySeparatorChar, '/');

                        var fileInfo = new FileInfo(filePath);
                        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(entryName)
                        {
                            DateTime = fileInfo.LastWriteTime,
                            Size = fileInfo.Length,
                            AESKeySize = 256
                        };

                        zipStream.PutNextEntry(entry);

                        using var fileStream = File.OpenRead(filePath);
                        fileStream.CopyTo(zipStream);

                        zipStream.CloseEntry();
                    }
                    catch (Exception ex)
                    {
                        LogUtilities.LogError("ZipUtilities.CreatePasswordProtectedMultiFolderZipAsync", $"Error adding file {filePath}", ex);
                    }
                }
            }

            zipStream.Finish();
        });
    }
}