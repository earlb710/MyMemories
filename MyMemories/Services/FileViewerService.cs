using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

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
    /// Loads a file based on its type.
    /// </summary>
    public async Task<FileLoadResult> LoadFileAsync(StorageFile file)
    {
        var extension = file.FileType.ToLowerInvariant();

        try
        {
            if (IsImageFile(extension))
            {
                await LoadImageAsync(file);
                return new FileLoadResult(FileViewerType.Image, file.Name);
            }
            else if (extension is ".html" or ".htm")
            {
                await LoadHtmlAsync(file);
                return new FileLoadResult(FileViewerType.Web, file.Name);
            }
            else if (extension == ".pdf")
            {
                await LoadPdfAsync(file);
                return new FileLoadResult(FileViewerType.Web, file.Name);
            }
            else if (IsTextFile(extension))
            {
                await LoadTextAsync(file);
                return new FileLoadResult(FileViewerType.Text, file.Name);
            }
            else
            {
                await LoadTextAsync(file);
                return new FileLoadResult(FileViewerType.Text, file.Name);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error loading file: {ex.Message}", ex);
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

    private async Task LoadImageAsync(StorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        _imageViewer.Source = bitmap;
    }

    private async Task LoadHtmlAsync(StorageFile file)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }
        _webViewer.Source = new Uri(file.Path);
    }

    private async Task LoadPdfAsync(StorageFile file)
    {
        if (_webViewer.CoreWebView2 == null)
        {
            await _webViewer.EnsureCoreWebView2Async();
        }
        _webViewer.Source = new Uri(file.Path);
    }

    private async Task LoadTextAsync(StorageFile file)
    {
        string content = await FileIO.ReadTextAsync(file);
        _textViewer.Text = content;
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
public record FileLoadResult(FileViewerType ViewerType, string FileName);

/// <summary>
/// Type of file viewer being used.
/// </summary>
public enum FileViewerType
{
    Image,
    Web,
    Text
}