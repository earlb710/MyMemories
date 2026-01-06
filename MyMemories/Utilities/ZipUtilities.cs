using System;
using System.Diagnostics;
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
            Debug.WriteLine("[ZipUtilities.ParseZipEntryUrl] URL is null or empty");
            return (null, null);
        }

        var parts = url.Split("::", 2);
        if (parts.Length != 2)
        {
            Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Invalid URL format (missing ::): {url}");
            return (null, null);
        }

        var zipPath = parts[0]?.Trim();
        var entryPath = parts[1]?.Trim();

        if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(entryPath))
        {
            Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Invalid parts - zipPath: '{zipPath}', entryPath: '{entryPath}'");
            return (null, null);
        }

        // Fix corrupted URLs where the zip filename is duplicated
        // Example: "C:\path\file.zip\file.zip" should be "C:\path\file.zip"
        if (zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !File.Exists(zipPath))
        {
            Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Zip file not found, checking for corruption: {zipPath}");
            
            // Check if the path has a duplicated zip filename
            // Pattern: "C:\path\file.zip\file.zip" or "C:\path\file.zip\somefile.zip"
            var lastBackslashIndex = zipPath.LastIndexOf('\\');
            if (lastBackslashIndex > 0)
            {
                var potentialZipPath = zipPath.Substring(0, lastBackslashIndex);
                if (potentialZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(potentialZipPath))
                {
                    Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Found corrupted path, repairing: {potentialZipPath}");
                    zipPath = potentialZipPath;
                }
            }
        }

        // Validate that the zip path doesn't already contain :: (another corruption pattern)
        if (zipPath.Contains("::"))
        {
            Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Corrupted URL - zip path contains ::: {zipPath}");
            // Try to extract the actual zip path
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                zipPath = zipPath.Substring(0, lastZipIndex + 4);
                Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Recovered zip path: {zipPath}");
            }
        }

        // Final validation
        if (!File.Exists(zipPath))
        {
            Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Final validation failed - zip file not found: {zipPath}");
            // Last attempt: try to find the .zip file in the path
            var lastZipIndex = zipPath.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (lastZipIndex > 0)
            {
                var attemptPath = zipPath.Substring(0, lastZipIndex + 4);
                if (File.Exists(attemptPath))
                {
                    Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Found valid zip at: {attemptPath}");
                    zipPath = attemptPath;
                }
            }
        }

        Debug.WriteLine($"[ZipUtilities.ParseZipEntryUrl] Parsed - zipPath: '{zipPath}', entryPath: '{entryPath}'");
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
            // Validate inputs
            if (string.IsNullOrEmpty(zipPath))
            {
                Debug.WriteLine("[ZipUtilities.ExtractZipEntryToStreamAsync] Zip path is null or empty");
                return null;
            }

            if (string.IsNullOrEmpty(entryPath))
            {
                Debug.WriteLine("[ZipUtilities.ExtractZipEntryToStreamAsync] Entry path is null or empty");
                return null;
            }

            // Check if zip file exists
            if (!File.Exists(zipPath))
            {
                Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Zip file not found: {zipPath}");
                return null;
            }

            // Check if file is accessible
            FileInfo zipFileInfo;
            try
            {
                zipFileInfo = new FileInfo(zipPath);
                if (zipFileInfo.Length == 0)
                {
                    Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Zip file is empty: {zipPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Cannot access zip file: {ex.Message}");
                return null;
            }

            Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Extracting '{entryPath}' from '{zipPath}'");

            // Run extraction on background thread
            return await Task.Run(() =>
            {
                ZipArchive? archive = null;
                try
                {
                    // Open zip archive - use System.IO.Compression
                    try
                    {
                        archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                    }
                    catch (InvalidDataException ex)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Invalid zip file format: {ex.Message}");
                        return null;
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] IO error opening zip: {ex.Message}");
                        return null;
                    }

                    // Normalize entry path (handle both forward and backward slashes)
                    var normalizedEntryPath = entryPath.Replace('\\', '/');

                    // Try to get the entry
                    var entry = archive.GetEntry(normalizedEntryPath);

                    // If not found, try without normalization
                    if (entry == null)
                    {
                        entry = archive.GetEntry(entryPath);
                    }

                    // If still not found, try case-insensitive search
                    if (entry == null)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry not found, trying case-insensitive search");
                        var lowerEntryPath = normalizedEntryPath.ToLowerInvariant();

                        foreach (var e in archive.Entries)
                        {
                            if (e.FullName.ToLowerInvariant() == lowerEntryPath)
                            {
                                entry = e;
                                Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Found entry with different case: {e.FullName}");
                                break;
                            }
                        }
                    }

                    if (entry == null)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry not found in archive: {entryPath}");
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Available entries:");
                        foreach (var e in archive.Entries)
                        {
                            Debug.WriteLine($"  - {e.FullName}");
                        }
                        return null;
                    }

                    // Check if entry is a directory
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry is a directory, not a file: {entryPath}");
                        return null;
                    }

                    // Check entry size
                    if (entry.Length == 0)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry is empty (0 bytes): {entryPath}");
                        return new MemoryStream(); // Return empty stream
                    }

                    // Validate size (prevent memory exhaustion)
                    const long MaxSizeInBytes = 500 * 1024 * 1024; // 500 MB limit
                    if (entry.Length > MaxSizeInBytes)
                    {
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry too large ({entry.Length} bytes): {entryPath}");
                        return null;
                    }

                    Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Entry size: {entry.Length} bytes");

                    // Create a memory stream to hold the extracted data
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
                        Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Error extracting entry: {ex.Message}");
                        memoryStream.Dispose();
                        return null;
                    }

                    // Reset position for reading
                    memoryStream.Position = 0;

                    Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Successfully extracted {memoryStream.Length} bytes");
                    return (Stream)memoryStream;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Unexpected error: {ex.GetType().Name} - {ex.Message}");
                    Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Stack trace: {ex.StackTrace}");
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
            Debug.WriteLine($"[ZipUtilities.ExtractZipEntryToStreamAsync] Outer exception: {ex.GetType().Name} - {ex.Message}");
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
            // Try to enumerate entries to detect corruption
            var count = 0;
            foreach (var entry in archive.Entries)
            {
                count++;
                if (count > 0) break; // Just need to verify we can read
            }
            return true;
        }
        catch (InvalidDataException)
        {
            Debug.WriteLine($"[ZipUtilities.ValidateZipFile] Invalid zip file: {zipPath}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZipUtilities.ValidateZipFile] Error validating zip: {ex.Message}");
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
    /// <param name="sourceDirectory">The directory to compress</param>
    /// <param name="zipFilePath">The output zip file path</param>
    /// <param name="password">Optional password for encryption (AES-256)</param>
    /// <param name="compressionLevel">Compression level (0-9, default is 6)</param>
    /// <returns>True if successful, false otherwise</returns>
    public static async Task<bool> CreatePasswordProtectedZipAsync(
        string sourceDirectory, 
        string zipFilePath, 
        string? password = null,
        int compressionLevel = 6)
    {
        if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Source directory not found: {sourceDirectory}");
            return false;
        }

        if (string.IsNullOrEmpty(zipFilePath))
        {
            Debug.WriteLine("[ZipUtilities.CreatePasswordProtectedZipAsync] Zip file path is null or empty");
            return false;
        }

        try
        {
            // Ensure the output directory exists
            var outputDir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await Task.Run(() =>
            {
                using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                // Set compression level (0-9)
                zipStream.SetLevel(Math.Clamp(compressionLevel, 0, 9));

                // Set password if provided
                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                    // Use Zip64 for better compatibility
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
                }

                // Get all files recursively
                var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                var baseDirectory = sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                foreach (var filePath in files)
                {
                    try
                    {
                        // Get relative path for entry name
                        var entryName = filePath.Substring(baseDirectory.Length + 1)
                            .Replace(Path.DirectorySeparatorChar, '/');

                        var fileInfo = new FileInfo(filePath);
                        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(entryName)
                        {
                            DateTime = fileInfo.LastWriteTime,
                            Size = fileInfo.Length
                        };

                        // Enable AES encryption if password is set
                        if (!string.IsNullOrEmpty(password))
                        {
                            entry.AESKeySize = 256; // Use AES-256
                        }

                        zipStream.PutNextEntry(entry);

                        // Copy file data
                        using var fileStream = File.OpenRead(filePath);
                        fileStream.CopyTo(zipStream);

                        zipStream.CloseEntry();

                        Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Added: {entryName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Error adding file {filePath}: {ex.Message}");
                        // Continue with other files
                    }
                }

                zipStream.Finish();
            });

            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Successfully created zip: {zipFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Error: {ex.Message}");
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipAsync] Stack trace: {ex.StackTrace}");
            
            // Clean up partial file if it exists
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
    /// <param name="sourceFilePath">The file to compress</param>
    /// <param name="zipFilePath">The output zip file path</param>
    /// <param name="password">Optional password for encryption (AES-256)</param>
    /// <param name="compressionLevel">Compression level (0-9, default is 6)</param>
    /// <returns>True if successful, false otherwise</returns>
    public static async Task<bool> CreatePasswordProtectedZipFromFileAsync(
        string sourceFilePath, 
        string zipFilePath, 
        string? password = null,
        int compressionLevel = 6)
    {
        if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Source file not found: {sourceFilePath}");
            return false;
        }

        if (string.IsNullOrEmpty(zipFilePath))
        {
            Debug.WriteLine("[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Zip file path is null or empty");
            return false;
        }

        try
        {
            // Ensure the output directory exists
            var outputDir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await Task.Run(() =>
            {
                using var outputStream = new FileStream(zipFilePath, FileMode.Create);
                using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(outputStream);

                // Set compression level (0-9)
                zipStream.SetLevel(Math.Clamp(compressionLevel, 0, 9));

                // Set password if provided
                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                    zipStream.UseZip64 = ICSharpCode.SharpZipLib.Zip.UseZip64.On;
                }

                // Get file name for entry
                var fileName = Path.GetFileName(sourceFilePath);
                var fileInfo = new FileInfo(sourceFilePath);

                var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry(fileName)
                {
                    DateTime = fileInfo.LastWriteTime,
                    Size = fileInfo.Length
                };

                // Enable AES encryption if password is set
                if (!string.IsNullOrEmpty(password))
                {
                    entry.AESKeySize = 256; // Use AES-256
                }

                zipStream.PutNextEntry(entry);

                // Copy file data
                using var fileStream = File.OpenRead(sourceFilePath);
                fileStream.CopyTo(zipStream);

                zipStream.CloseEntry();
                zipStream.Finish();

                Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Successfully added file: {fileName}");
            });

            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Successfully created zip: {zipFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Error: {ex.Message}");
            Debug.WriteLine($"[ZipUtilities.CreatePasswordProtectedZipFromFileAsync] Stack trace: {ex.StackTrace}");
            
            // Clean up partial file if it exists
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