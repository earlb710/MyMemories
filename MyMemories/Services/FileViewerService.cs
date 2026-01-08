using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using MyMemories.Utilities;

namespace MyMemories.Services;

/// <summary>
/// Service for loading and displaying different file types.
/// </summary>
public class FileViewerService
{
    private readonly Image _imageViewer;
    private readonly WebView2 _webViewer;
    private readonly TextBox _textViewer;

    public FileViewerService(Image imageViewer, WebView2 webViewer, TextBox textViewer)
    {
        _imageViewer = imageViewer;
        _webViewer = webViewer;
        _textViewer = textViewer;
    }

    /// <summary>
    /// Loads a file based on its type (supports both regular files and zip entries).
    /// </summary>
    public async Task<FileLoadResult> LoadFileAsync(StorageFile file)
    {
        var extension = file.FileType.ToLowerInvariant();

        try
        {
            if (IsImageFile(extension))
            {
                var bitmap = await LoadImageAsync(file);
                return new FileLoadResult(FileViewerType.Image, file.Name, bitmap);
            }
            else if (extension is ".html" or ".htm")
            {
                await LoadHtmlAsync(file);
                return new FileLoadResult(FileViewerType.Web, file.Name, null);
            }
            else if (extension == ".pdf")
            {
                await LoadPdfAsync(file);
                return new FileLoadResult(FileViewerType.Web, file.Name, null);
            }
            else if (IsTextFile(extension))
            {
                await LoadTextAsync(file);
                return new FileLoadResult(FileViewerType.Text, file.Name, null);
            }
            else
            {
                await LoadTextAsync(file);
                return new FileLoadResult(FileViewerType.Text, file.Name, null);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("FileViewerService.LoadFileAsync", "Error loading file", ex);
            throw new InvalidOperationException($"Error loading file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a file from a zip entry URL (format: "zipPath::entryPath").
    /// </summary>
    public async Task<FileLoadResult> LoadZipEntryAsync(string zipEntryUrl, string? password = null)
    {
        var (zipPath, entryPath) = ZipUtilities.ParseZipEntryUrl(zipEntryUrl);

        if (zipPath == null || entryPath == null)
        {
            var error = "Invalid zip entry URL format. Expected format: 'zipPath::entryPath'";
            LogUtilities.LogError("FileViewerService.LoadZipEntryAsync", error);
            throw new InvalidOperationException(error);
        }

        if (!File.Exists(zipPath))
        {
            var error = $"Zip file not found: {zipPath}";
            LogUtilities.LogError("FileViewerService.LoadZipEntryAsync", error);
            throw new FileNotFoundException(error, zipPath);
        }

        // Check if it's password-protected
        try
        {
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                // Standard zip, not password-protected
            }
        }
        catch (InvalidDataException)
        {
            // Password-protected
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    $"Cannot view files in password-protected archives without password.\n\n" +
                    $"Archive: {Path.GetFileName(zipPath)}\n" +
                    $"File: {Path.GetFileName(entryPath)}\n\n" +
                    $"This archive requires a password to extract files."
                );
            }
        }

        var extension = ZipUtilities.GetZipEntryExtension(entryPath);
        var fileName = Path.GetFileName(entryPath);

        try
        {
            if (IsImageFile(extension))
            {
                var bitmap = await LoadImageFromZipAsync(zipPath, entryPath, password);
                return new FileLoadResult(FileViewerType.Image, fileName, bitmap);
            }
            else if (extension == ".pdf")
            {
                await LoadPdfFromZipAsync(zipPath, entryPath, password);
                return new FileLoadResult(FileViewerType.Web, fileName, null);
            }
            else if (extension is ".html" or ".htm")
            {
                await LoadHtmlFromZipAsync(zipPath, entryPath, password);
                return new FileLoadResult(FileViewerType.Web, fileName, null);
            }
            else if (IsTextFile(extension))
            {
                await LoadTextFromZipAsync(zipPath, entryPath, password);
                return new FileLoadResult(FileViewerType.Text, fileName, null);
            }
            else
            {
                await LoadTextFromZipAsync(zipPath, entryPath, password);
                return new FileLoadResult(FileViewerType.Text, fileName, null);
            }
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("FileViewerService.LoadZipEntryAsync", $"Error loading entry '{fileName}' from zip", ex);
            throw new InvalidOperationException($"Error loading '{fileName}' from zip: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a URL in the web viewer.
    /// </summary>
    public async Task LoadUrlAsync(Uri uri)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }

        _webViewer.Source = uri;
    }

    /// <summary>
    /// Loads a URL in the web viewer with URL bar support.
    /// </summary>
    public async Task LoadUrlAsync(Uri uri, TextBox? urlTextBox)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }

        _webViewer.Source = uri;
        
        // Update URL text box if provided
        if (urlTextBox != null)
        {
            urlTextBox.Text = uri.ToString();
        }
    }

    private async Task<BitmapImage> LoadImageAsync(StorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        _imageViewer.Source = bitmap;
        return bitmap;
    }

    private async Task<BitmapImage> LoadImageFromZipAsync(string zipPath, string entryPath, string? password = null)
    {
        var stream = await ZipUtilities.ExtractZipEntryToStreamAsync(zipPath, entryPath, password);
        if (stream == null)
        {
            throw new InvalidOperationException($"Failed to extract image '{entryPath}' from zip archive");
        }

        try
        {
            var bitmap = new BitmapImage();

            using (var memStream = new InMemoryRandomAccessStream())
            {
                var writeStream = memStream.AsStreamForWrite();
                await stream.CopyToAsync(writeStream);
                await writeStream.FlushAsync();

                memStream.Seek(0);

                await bitmap.SetSourceAsync(memStream);
            }

            _imageViewer.Source = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            LogUtilities.LogError("FileViewerService.LoadImageFromZipAsync", "Error setting bitmap", ex);
            throw new InvalidOperationException($"Failed to load image: {ex.Message}", ex);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private async Task LoadHtmlAsync(StorageFile file)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }
        _webViewer.Source = new Uri(file.Path);
    }

    private async Task LoadHtmlFromZipAsync(string zipPath, string entryPath, string? password = null)
    {
        var stream = await ZipUtilities.ExtractZipEntryToStreamAsync(zipPath, entryPath, password);
        if (stream == null)
        {
            throw new InvalidOperationException($"Failed to extract HTML '{entryPath}' from zip archive");
        }

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            var htmlContent = await reader.ReadToEndAsync();

            if (_webViewer.CoreWebView2 == null)
            {
                await _webViewer.EnsureCoreWebView2Async();
            }

            _webViewer.NavigateToString(htmlContent);
        }
    }

    private async Task LoadPdfAsync(StorageFile file)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }
        _webViewer.Source = new Uri(file.Path);
    }

    private async Task LoadPdfFromZipAsync(string zipPath, string entryPath, string? password = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MyMemories", Path.GetFileNameWithoutExtension(zipPath));
        Directory.CreateDirectory(tempDir);

        var tempFilePath = Path.Combine(tempDir, Path.GetFileName(entryPath));

        var stream = await ZipUtilities.ExtractZipEntryToStreamAsync(zipPath, entryPath, password);
        if (stream == null)
        {
            throw new InvalidOperationException($"Failed to extract PDF '{entryPath}' from zip archive");
        }

        using (stream)
        using (var fileStream = File.Create(tempFilePath))
        {
            await stream.CopyToAsync(fileStream);
        }

        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }

        _webViewer.Source = new Uri(tempFilePath);
    }

    private async Task LoadTextAsync(StorageFile file)
    {
        string content = await FileIO.ReadTextAsync(file);
        _textViewer.Text = content;
    }

    private async Task LoadTextFromZipAsync(string zipPath, string entryPath, string? password = null)
    {
        var stream = await ZipUtilities.ExtractZipEntryToStreamAsync(zipPath, entryPath, password);
        if (stream == null)
        {
            throw new InvalidOperationException($"Failed to extract text file '{entryPath}' from zip archive");
        }

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            var content = await reader.ReadToEndAsync();
            _textViewer.Text = content;
        }
    }

    private static bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".ico";
    }

    private static bool IsTextFile(string extension)
    {
        return extension is ".txt" or ".xml" or ".json" or ".md" or ".log" or ".cs"
            or ".xaml" or ".config" or ".ini" or ".yaml" or ".yml" or ".csv";
    }

    public static string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
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

/// <summary>
/// Result of a file load operation.
/// </summary>
public record FileLoadResult(FileViewerType ViewerType, string FileName, BitmapImage? Bitmap);

/// <summary>
/// Type of file viewer being used.
/// </summary>
public enum FileViewerType
{
    Image,
    Web,
    Text
}