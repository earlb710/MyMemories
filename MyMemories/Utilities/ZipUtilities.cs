using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace MyMemories.Utilities;

public static class ZipUtilities
{
    /// <summary>
    /// Parses a zip entry URL (format: "zipPath::entryPath").
    /// Returns (zipPath, entryPath) or (null, null) if invalid.
    /// </summary>
    public static (string? zipPath, string? entryPath) ParseZipEntryUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", "URL is null or empty");
            return (null, null);
        }

        var parts = url.Split("::", 2);
        if (parts.Length != 2)
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Invalid URL format (missing ::): {url}");
            return (null, null);
        }

        var zipPath = parts[0]?.Trim();
        var entryPath = parts[1]?.Trim();

        if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(entryPath))
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Invalid parts - zipPath: '{zipPath}', entryPath: '{entryPath}'");
            return (null, null);
        }

        // Fix corrupted URLs where the zip filename is duplicated
        // Example: "C:\path\file.zip\file.zip" should be "C:\path\file.zip"
        if (zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !File.Exists(zipPath))
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Zip file not found, checking for corruption: {zipPath}");
            
            var lastBackslashIndex = zipPath.LastIndexOf('\\');
            if (lastBackslashIndex > 0)
            {
                var potentialZipPath = zipPath.Substring(0, lastBackslashIndex);
                if (potentialZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(potentialZipPath))
                {
                    LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Found corrupted path, repairing: {potentialZipPath}");
                    zipPath = potentialZipPath;
                }
            }
        }

        // Validate that the zip path doesn't already contain :: (another corruption pattern)
        if (zipPath.Contains("::"))
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Corrupted URL - zip path contains ::: {zipPath}");
            // Try to extract the actual zip path
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                zipPath = zipPath.Substring(0, lastZipIndex + 4);
                LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Recovered zip path: {zipPath}");
            }
        }

        // Final validation
        if (!File.Exists(zipPath))
        {
            LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Final validation failed - zip file not found: {zipPath}");
            // Last attempt: try to find the .zip file in the path
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                var attemptPath = zipPath.Substring(0, lastZipIndex + 4);
                if (File.Exists(attemptPath))
                {
                    LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Found valid zip at: {attemptPath}");
                    zipPath = attemptPath;
                }
            }
        }

        LogUtilities.LogDebug("ZipUtilities.ParseZipEntryUrl", $"Parsed - zipPath: '{zipPath}', entryPath: '{entryPath}'");
        return (zipPath, entryPath);
    }

    /// <summary>
    /// Checks if a URL represents a zip entry (contains "::").
    /// </summary>
    public static bool IsZipEntryUrl(string url)
    {
        return !string.IsNullOrEmpty(url) && url.Contains("::");
    }

    /// <summary>
    /// Extracts a zip entry to a memory stream for viewing.
    /// Returns null if the entry doesn't exist or cannot be read.
    /// </summary>
    public static async Task<Stream?> ExtractZipEntryToStreamAsync(string zipPath, string entryPath)
    {
        try
        {
            if (string.IsNullOrEmpty(zipPath))
            {
                LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", "Zip path is null or empty");
                return null;
            }

            if (string.IsNullOrEmpty(entryPath))
            {
                LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", "Entry path is null or empty");
                return null;
            }

            if (!File.Exists(zipPath))
            {
                LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Zip file not found: {zipPath}");
                return null;
            }

            FileInfo zipFileInfo;
            try
            {
                zipFileInfo = new FileInfo(zipPath);
                if (zipFileInfo.Length == 0)
                {
                    LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Zip file is empty: {zipPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Cannot access zip file: {ex.Message}");
                return null;
            }

            LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Extracting '{entryPath}' from '{zipPath}'");

            return await Task.Run(() =>
            {
                ZipArchive? archive = null;
                try
                {
                    try
                    {
                        archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                    }
                    catch (InvalidDataException ex)
                    {
                        LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", "Invalid zip file format", ex);
                        return null;
                    }
                    catch (IOException ex)
                    {
                        LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", "IO error opening zip", ex);
                        return null;
                    }

                    var normalizedEntryPath = entryPath.Replace('\\', '/');
                    var entry = archive.GetEntry(normalizedEntryPath);

                    if (entry == null)
                    {
                        entry = archive.GetEntry(entryPath);
                    }

                    if (entry == null)
                    {
                        LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", "Entry not found, trying case-insensitive search");
                        var lowerEntryPath = normalizedEntryPath.ToLowerInvariant();

                        foreach (var e in archive.Entries)
                        {
                            if (e.FullName.ToLowerInvariant() == lowerEntryPath)
                            {
                                entry = e;
                                LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Found entry with different case: {e.FullName}");
                                break;
                            }
                        }
                    }

                    if (entry == null)
                    {
                        LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Entry not found in archive: {entryPath}");
                        LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", "Available entries:");
                        foreach (var e in archive.Entries)
                        {
                            LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"  - {e.FullName}");
                        }
                        return null;
                    }

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Entry is a directory, not a file: {entryPath}");
                        return null;
                    }

                    if (entry.Length == 0)
                    {
                        LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Entry is empty (0 bytes): {entryPath}");
                        return new MemoryStream();
                    }

                    const long MaxSizeInBytes = 500 * 1024 * 1024; // 500 MB limit
                    if (entry.Length > MaxSizeInBytes)
                    {
                        LogUtilities.LogWarning("ZipUtilities.ExtractZipEntryToStreamAsync", $"Entry too large ({entry.Length} bytes): {entryPath}");
                        return null;
                    }

                    LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Entry size: {entry.Length} bytes");

                    var memoryStream = new MemoryStream((int)entry.Length);

                    try
                    {
                        using (var entryStream = entry.Open())
                        {
                            entryStream.CopyTo(memoryStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", "Error extracting entry", ex);
                        memoryStream.Dispose();
                        return null;
                    }

                    memoryStream.Position = 0;
                    LogUtilities.LogDebug("ZipUtilities.ExtractZipEntryToStreamAsync", $"Successfully extracted {memoryStream.Length} bytes");
                    return (Stream)memoryStream;
                }
                catch (Exception ex)
                {
                    LogUtilities.LogError("ZipUtilities.ExtractZipEntryToStreamAsync", "Unexpected error", ex);
                    return null;
                }
                finally
                {
                    archive?.Dispose();
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
        catch (InvalidDataException ex)
        {
            LogUtilities.LogError("ZipUtilities.ValidateZipFile", $"Invalid zip file: {zipPath}", ex);
            return false;
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
                        LogUtilities.LogDebug("ZipUtilities.CreatePasswordProtectedZipAsync", $"Added: {entryName}");
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

                LogUtilities.LogDebug("ZipUtilities.CreatePasswordProtectedZipFromFileAsync", $"Successfully added file: {fileName}");
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
}