using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace MyMemories.Services;

public class FileLauncherService
{
    public async Task<bool> OpenFileAsync(string filePath, Action<string> setStatus)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var errorMsg = $"File not found: {filePath}";
                Debug.WriteLine($"[OpenFileAsync] {errorMsg}");
                setStatus(errorMsg);
                return false;
            }

            var file = await StorageFile.GetFileFromPathAsync(filePath);
            await Launcher.LaunchFileAsync(file);
            setStatus($"Opened: {Path.GetFileName(filePath)}");
            Debug.WriteLine($"[OpenFileAsync] Successfully opened: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening file: {ex.Message}";
            Debug.WriteLine($"[OpenFileAsync] Exception: {ex}");
            setStatus(errorMsg);
            return false;
        }
    }

    public async Task<bool> OpenLinkAsync(LinkItem linkItem, Action<string> setStatus)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            Debug.WriteLine("[OpenLinkAsync] Link has no URL");
            setStatus("Link has no URL to open");
            return false;
        }

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
            {
                await Launcher.LaunchFolderPathAsync(linkItem.Url);
                setStatus($"Opened directory: {linkItem.Title}");
                Debug.WriteLine($"[OpenLinkAsync] Opened directory: {linkItem.Url}");
                return true;
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await Launcher.LaunchFileAsync(file);
                    setStatus($"Opened file: {linkItem.Title}");
                    Debug.WriteLine($"[OpenLinkAsync] Opened file: {linkItem.Url}");
                }
                else
                {
                    await Launcher.LaunchUriAsync(uri);
                    setStatus($"Opened URL: {linkItem.Title}");
                    Debug.WriteLine($"[OpenLinkAsync] Opened URL: {uri}");
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening link: {ex.Message}";
            Debug.WriteLine($"[OpenLinkAsync] Exception for '{linkItem.Title}': {ex}");
            setStatus(errorMsg);
            return false;
        }
    }

    public async Task OpenZipEntryAsync(LinkItem zipEntry, Action<string> setStatus)
    {
        try
        {
            var parts = zipEntry.Url.Split("::", 2);
            if (parts.Length != 2)
            {
                Debug.WriteLine($"[OpenZipEntryAsync] Invalid zip entry URL format: {zipEntry.Url}");
                return;
            }

            var zipPath = parts[0];
            var entryPath = parts[1];

            Debug.WriteLine($"[OpenZipEntryAsync] Opening zip entry: {entryPath} from {zipPath}");

            if (!File.Exists(zipPath))
            {
                var errorMsg = "Zip file not found";
                Debug.WriteLine($"[OpenZipEntryAsync] {errorMsg}: {zipPath}");
                setStatus(errorMsg);
                return;
            }

            // Create temp directory for extraction
            var tempDir = Path.Combine(Path.GetTempPath(), "MyMemories", Path.GetFileNameWithoutExtension(zipPath));
            Directory.CreateDirectory(tempDir);

            var extractedPath = Path.Combine(tempDir, entryPath.Replace('/', Path.DirectorySeparatorChar));

            // Extract the specific file
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entry = archive.GetEntry(entryPath);
                if (entry != null)
                {
                    // Ensure directory exists
                    var extractedDir = Path.GetDirectoryName(extractedPath);
                    if (!string.IsNullOrEmpty(extractedDir))
                    {
                        Directory.CreateDirectory(extractedDir);
                    }

                    entry.ExtractToFile(extractedPath, true);

                    // Open the extracted file
                    await OpenFileAsync(extractedPath, setStatus);
                    setStatus($"Opened file from zip: {zipEntry.Title}");
                    Debug.WriteLine($"[OpenZipEntryAsync] Successfully extracted and opened: {entryPath}");
                }
                else
                {
                    Debug.WriteLine($"[OpenZipEntryAsync] Entry not found in archive: {entryPath}");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error opening zip entry: {ex.Message}";
            Debug.WriteLine($"[OpenZipEntryAsync] Exception for '{zipEntry.Title}': {ex}");
            setStatus(errorMsg);
        }
    }
}