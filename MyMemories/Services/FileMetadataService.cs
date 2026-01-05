using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace MyMemories.Services;

/// <summary>
/// Service for extracting metadata from different file types.
/// </summary>
public static class FileMetadataService
{
    /// <summary>
    /// Generates a descriptive string based on file type and metadata.
    /// </summary>
    public static async Task<string> GenerateFileDescriptionAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var properties = await file.GetBasicPropertiesAsync();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Image files - get dimensions
            if (IsImageFile(extension))
            {
                return await GetImageDescriptionAsync(file, properties);
            }
            
            // PDF files - get page count
            if (extension == ".pdf")
            {
                return await GetPdfDescriptionAsync(file, properties);
            }
            
            // Video files - get duration if possible
            if (IsVideoFile(extension))
            {
                return await GetVideoDescriptionAsync(file, properties);
            }
            
            // Audio files - get duration
            if (IsAudioFile(extension))
            {
                return await GetAudioDescriptionAsync(file, properties);
            }
            
            // Default - just show file size
            return $"Size: {FormatFileSize(properties.Size)}";
        }
        catch
        {
            // If metadata extraction fails, return empty string
            return string.Empty;
        }
    }

    private static async Task<string> GetImageDescriptionAsync(StorageFile file, BasicProperties properties)
    {
        try
        {
            var imageProps = await file.Properties.GetImagePropertiesAsync();
            if (imageProps.Width > 0 && imageProps.Height > 0)
            {
                return $"{imageProps.Width}x{imageProps.Height} • {FormatFileSize(properties.Size)}";
            }
        }
        catch { }
        
        return $"Image • {FormatFileSize(properties.Size)}";
    }

    private static async Task<string> GetPdfDescriptionAsync(StorageFile file, BasicProperties properties)
    {
        try
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);
            var pageCount = pdfDocument.PageCount;
            return $"{pageCount} page{(pageCount != 1 ? "s" : "")} • {FormatFileSize(properties.Size)}";
        }
        catch
        {
            return $"PDF • {FormatFileSize(properties.Size)}";
        }
    }

    private static async Task<string> GetVideoDescriptionAsync(StorageFile file, BasicProperties properties)
    {
        try
        {
            var videoProps = await file.Properties.GetVideoPropertiesAsync();
            if (videoProps.Duration.TotalSeconds > 0)
            {
                var duration = FormatDuration(videoProps.Duration);
                if (videoProps.Width > 0 && videoProps.Height > 0)
                {
                    return $"{videoProps.Width}x{videoProps.Height} • {duration} • {FormatFileSize(properties.Size)}";
                }
                return $"{duration} • {FormatFileSize(properties.Size)}";
            }
        }
        catch { }
        
        return $"Video • {FormatFileSize(properties.Size)}";
    }

    private static async Task<string> GetAudioDescriptionAsync(StorageFile file, BasicProperties properties)
    {
        try
        {
            var musicProps = await file.Properties.GetMusicPropertiesAsync();
            if (musicProps.Duration.TotalSeconds > 0)
            {
                var duration = FormatDuration(musicProps.Duration);
                return $"{duration} • {FormatFileSize(properties.Size)}";
            }
        }
        catch { }
        
        return $"Audio • {FormatFileSize(properties.Size)}";
    }

    private static bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico";
    }

    private static bool IsVideoFile(string extension)
    {
        return extension is ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".m4v" or ".flv" or ".webm";
    }

    private static bool IsAudioFile(string extension)
    {
        return extension is ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{(int)duration.TotalSeconds}s";
    }

    public static string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
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