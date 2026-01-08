using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using MyMemories.Utilities;

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
                LogUtilities.LogWarning("FileLauncherService.OpenFileAsync", errorMsg);
                setStatus(errorMsg);
                return false;
            }

            var file = await StorageFile.GetFileFromPathAsync(filePath);
            await Launcher.LaunchFileAsync(file);
            setStatus($"Opened: {Path.GetFileName(filePath)}");
            LogUtilities.LogInfo("FileLauncherService.OpenFileAsync", $"Successfully opened: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            LogUtilities.LogErrorWithContext(
                "FileLauncherService.OpenFileAsync",
                "Failed to open file",
                ex,
                new { FilePath = filePath },
                setStatus);
            return false;
        }
    }

    public async Task<bool> OpenLinkAsync(LinkItem linkItem, Action<string> setStatus)
    {
        if (string.IsNullOrEmpty(linkItem.Url))
        {
            LogUtilities.LogWarning("FileLauncherService.OpenLinkAsync", "Link has no URL");
            setStatus("Link has no URL to open");
            return false;
        }

        try
        {
            if (linkItem.IsDirectory || Directory.Exists(linkItem.Url))
            {
                await Launcher.LaunchFolderPathAsync(linkItem.Url);
                setStatus($"Opened directory: {linkItem.Title}");
                LogUtilities.LogInfo("FileLauncherService.OpenLinkAsync", $"Opened directory: {linkItem.Url}");
                return true;
            }
            else if (Uri.TryCreate(linkItem.Url, UriKind.Absolute, out Uri? uri))
            {
                if (uri.IsFile)
                {
                    var file = await StorageFile.GetFileFromPathAsync(linkItem.Url);
                    await Launcher.LaunchFileAsync(file);
                    setStatus($"Opened file: {linkItem.Title}");
                    LogUtilities.LogInfo("FileLauncherService.OpenLinkAsync", $"Opened file: {linkItem.Url}");
                }
                else
                {
                    await Launcher.LaunchUriAsync(uri);
                    setStatus($"Opened URL: {linkItem.Title}");
                    LogUtilities.LogInfo("FileLauncherService.OpenLinkAsync", $"Opened URL: {uri}");
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            LogUtilities.LogErrorWithContext(
                "FileLauncherService.OpenLinkAsync",
                "Failed to open link",
                ex,
                new { 
                    LinkTitle = linkItem.Title,
                    LinkUrl = linkItem.Url,
                    IsDirectory = linkItem.IsDirectory
                },
                setStatus);
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
                LogUtilities.LogWarning(
                    "FileLauncherService.OpenZipEntryAsync",
                    $"Invalid zip entry URL format: {zipEntry.Url}");
                return;
            }

            var zipPath = parts[0];
            var entryPath = parts[1];

            LogUtilities.LogDebug(
                "FileLauncherService.OpenZipEntryAsync",
                $"Opening zip entry: {entryPath} from {zipPath}");

            if (!File.Exists(zipPath))
            {
                var errorMsg = "Zip file not found";
                LogUtilities.LogWarning(
                    "FileLauncherService.OpenZipEntryAsync",
                    $"{errorMsg}: {zipPath}");
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
                    LogUtilities.LogInfo(
                        "FileLauncherService.OpenZipEntryAsync",
                        $"Successfully extracted and opened: {entryPath}");
                }
                else
                {
                    LogUtilities.LogWarning(
                        "FileLauncherService.OpenZipEntryAsync",
                        $"Entry not found in archive: {entryPath}");
                }
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogErrorWithContext(
                "FileLauncherService.OpenZipEntryAsync",
                "Failed to open zip entry",
                ex,
                new {
                    ZipEntryTitle = zipEntry.Title,
                    ZipEntryUrl = zipEntry.Url
                },
                setStatus);
        }
    }
}