using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for fetching and summarizing web page content.
/// </summary>
public class WebSummaryService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // Content types that should not be parsed as text
    private static readonly HashSet<string> _binaryContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml", "image/tiff",
        "application/pdf",
        "application/zip", "application/x-zip-compressed", "application/x-rar-compressed", "application/x-7z-compressed",
        "application/octet-stream",
        "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4",
        "video/mp4", "video/webm", "video/ogg", "video/quicktime", "video/x-msvideo",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    // File extensions that indicate binary content
    private static readonly HashSet<string> _binaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".tiff", ".ico",
        ".pdf",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".mp3", ".wav", ".ogg", ".flac", ".aac",
        ".mp4", ".webm", ".avi", ".mov", ".mkv", ".wmv",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".exe", ".msi", ".dll"
    };

    static WebSummaryService()
    {
        // Set a user agent to avoid being blocked by some websites
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", 
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    }

    /// <summary>
    /// Fetches and summarizes a web page from the given URL.
    /// </summary>
    public async Task<WebPageSummary> SummarizeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var summary = new WebPageSummary { Url = url };

        try
        {
            // First, check if URL looks like a binary file by extension
            if (IsBinaryFileByExtension(url))
            {
                return CreateBinaryFileSummary(url, summary);
            }

            // Perform HEAD request first to check content type without downloading full content
            var contentType = await GetContentTypeAsync(url, cancellationToken);
            
            if (contentType != null && IsBinaryContentType(contentType))
            {
                return CreateBinaryContentSummary(url, contentType, summary);
            }

            // Fetch the HTML content with cancellation support
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Check content type from response
            var responseContentType = response.Content.Headers.ContentType?.MediaType;
            if (responseContentType != null && IsBinaryContentType(responseContentType))
            {
                return CreateBinaryContentSummary(url, responseContentType, summary);
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for cancellation before processing
            cancellationToken.ThrowIfCancellationRequested();

            // Extract title
            summary.Title = ExtractTitle(html);

            // Extract meta description
            summary.Description = ExtractMetaDescription(html);

            // Extract keywords
            summary.Keywords = ExtractKeywords(html);

            // Extract main content summary
            summary.ContentSummary = ExtractContentSummary(html);

            // Extract additional metadata
            summary.Author = ExtractAuthor(html);
            summary.PublishedDate = ExtractPublishedDate(html);
            summary.ImageUrl = ExtractOgImage(html);
            summary.SiteName = ExtractSiteName(html);
            summary.Locale = ExtractLocale(html);
            summary.ContentType = ExtractContentType(html);

            summary.Success = true;
            summary.StatusCode = (int)response.StatusCode;
            summary.MediaType = "text/html";
        }
        catch (OperationCanceledException)
        {
            summary.Success = false;
            summary.ErrorMessage = "Request was cancelled";
            summary.WasCancelled = true;
        }
        catch (HttpRequestException ex)
        {
            summary.Success = false;
            summary.ErrorMessage = $"HTTP Error: {ex.Message}";
            summary.StatusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode : 0;
        }
        catch (Exception ex)
        {
            summary.Success = false;
            summary.ErrorMessage = $"Error: {ex.Message}";
        }

        return summary;
    }

    /// <summary>
    /// Checks if a URL points to a binary file based on its extension.
    /// </summary>
    private bool IsBinaryFileByExtension(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = System.IO.Path.GetExtension(path);
            return !string.IsNullOrEmpty(extension) && _binaryExtensions.Contains(extension);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a content type is binary.
    /// </summary>
    private bool IsBinaryContentType(string contentType)
    {
        // Extract just the media type without parameters
        var mediaType = contentType.Split(';')[0].Trim();
        return _binaryContentTypes.Contains(mediaType) ||
               mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the content type of a URL using a HEAD request.
    /// </summary>
    private async Task<string?> GetContentTypeAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.Content.Headers.ContentType?.MediaType;
        }
        catch
        {
            return null; // If HEAD fails, we'll try GET anyway
        }
    }

    /// <summary>
    /// Creates a summary for a binary file detected by extension.
    /// </summary>
    private WebPageSummary CreateBinaryFileSummary(string url, WebPageSummary summary)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = System.IO.Path.GetFileName(uri.AbsolutePath);
            var extension = System.IO.Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

            summary.Success = true;
            summary.Title = fileName;
            summary.IsBinaryContent = true;
            summary.MediaType = GetMediaTypeFromExtension(extension);
            summary.Description = GetBinaryFileDescription(extension, fileName);
            summary.ContentSummary = $"This URL points to a {GetFileTypeDescription(extension)} file and cannot be summarized as text.";

            return summary;
        }
        catch
        {
            summary.Success = false;
            summary.ErrorMessage = "Could not parse binary file URL";
            return summary;
        }
    }

    /// <summary>
    /// Creates a summary for binary content detected by content type.
    /// </summary>
    private WebPageSummary CreateBinaryContentSummary(string url, string contentType, WebPageSummary summary)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = System.IO.Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "Unknown file";
            }

            summary.Success = true;
            summary.Title = fileName;
            summary.IsBinaryContent = true;
            summary.MediaType = contentType;
            summary.Description = GetBinaryContentDescription(contentType, fileName);
            summary.ContentSummary = $"This URL returns {GetContentTypeDescription(contentType)} content and cannot be summarized as text.";

            return summary;
        }
        catch
        {
            summary.Success = false;
            summary.ErrorMessage = "Could not parse binary content URL";
            return summary;
        }
    }

    /// <summary>
    /// Gets a description for a binary file based on its extension.
    /// </summary>
    private string GetBinaryFileDescription(string extension, string fileName)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" or ".tiff" or ".ico"
                => $"Image file: {fileName}",
            ".pdf" => $"PDF document: {fileName}",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".aac"
                => $"Audio file: {fileName}",
            ".mp4" or ".webm" or ".avi" or ".mov" or ".mkv" or ".wmv"
                => $"Video file: {fileName}",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz"
                => $"Archive file: {fileName}",
            ".doc" or ".docx" => $"Word document: {fileName}",
            ".xls" or ".xlsx" => $"Excel spreadsheet: {fileName}",
            ".ppt" or ".pptx" => $"PowerPoint presentation: {fileName}",
            _ => $"Binary file: {fileName}"
        };
    }

    /// <summary>
    /// Gets a description for binary content based on its content type.
    /// </summary>
    private string GetBinaryContentDescription(string contentType, string fileName)
    {
        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        
        if (mediaType.StartsWith("image/"))
            return $"Image: {fileName}";
        if (mediaType.StartsWith("audio/"))
            return $"Audio: {fileName}";
        if (mediaType.StartsWith("video/"))
            return $"Video: {fileName}";
        if (mediaType == "application/pdf")
            return $"PDF document: {fileName}";
        if (mediaType.Contains("zip") || mediaType.Contains("compressed"))
            return $"Archive: {fileName}";
        
        return $"Binary content: {fileName}";
    }

    /// <summary>
    /// Gets a human-readable file type description.
    /// </summary>
    private string GetFileTypeDescription(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "JPEG image",
            ".png" => "PNG image",
            ".gif" => "GIF image",
            ".webp" => "WebP image",
            ".bmp" => "Bitmap image",
            ".svg" => "SVG image",
            ".pdf" => "PDF document",
            ".mp3" => "MP3 audio",
            ".wav" => "WAV audio",
            ".mp4" => "MP4 video",
            ".webm" => "WebM video",
            ".zip" => "ZIP archive",
            _ => extension.TrimStart('.').ToUpperInvariant()
        };
    }

    /// <summary>
    /// Gets a human-readable content type description.
    /// </summary>
    private string GetContentTypeDescription(string contentType)
    {
        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        
        return mediaType switch
        {
            "image/jpeg" => "JPEG image",
            "image/png" => "PNG image",
            "image/gif" => "GIF image",
            "image/webp" => "WebP image",
            "application/pdf" => "PDF document",
            "audio/mpeg" => "MP3 audio",
            "video/mp4" => "MP4 video",
            _ when mediaType.StartsWith("image/") => "image",
            _ when mediaType.StartsWith("audio/") => "audio",
            _ when mediaType.StartsWith("video/") => "video",
            _ => "binary"
        };
    }

    /// <summary>
    /// Gets media type from file extension.
    /// </summary>
    private string GetMediaTypeFromExtension(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Extracts the page title from HTML.
    /// </summary>
    private string ExtractTitle(string html)
    {
        // Try to extract from <title> tag
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (titleMatch.Success)
        {
            return DecodeHtml(titleMatch.Groups[1].Value.Trim());
        }

        // Try to extract from og:title meta tag
        var ogTitleMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:title[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogTitleMatch.Success)
        {
            return DecodeHtml(ogTitleMatch.Groups[1].Value.Trim());
        }

        // Try alternate og:title format
        var ogTitleAltMatch = Regex.Match(html, 
            @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:title[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogTitleAltMatch.Success)
        {
            return DecodeHtml(ogTitleAltMatch.Groups[1].Value.Trim());
        }

        return "No title found";
    }

    /// <summary>
    /// Extracts the meta description from HTML.
    /// </summary>
    private string ExtractMetaDescription(string html)
    {
        // Try to extract from meta description tag
        var descMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']description[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (descMatch.Success)
        {
            return DecodeHtml(descMatch.Groups[1].Value.Trim());
        }

        // Try alternate format
        var descAltMatch = Regex.Match(html, 
            @"<meta[^>]+content=[""']([^""']+)[""'][^>]+name=[""']description[""']", 
            RegexOptions.IgnoreCase);
        
        if (descAltMatch.Success)
        {
            return DecodeHtml(descAltMatch.Groups[1].Value.Trim());
        }

        // Try to extract from og:description meta tag
        var ogDescMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogDescMatch.Success)
        {
            return DecodeHtml(ogDescMatch.Groups[1].Value.Trim());
        }

        // Try alternate og:description format
        var ogDescAltMatch = Regex.Match(html, 
            @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:description[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogDescAltMatch.Success)
        {
            return DecodeHtml(ogDescAltMatch.Groups[1].Value.Trim());
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts author information from HTML.
    /// </summary>
    private string? ExtractAuthor(string html)
    {
        // Try meta author tag
        var authorMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']author[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (authorMatch.Success)
        {
            return DecodeHtml(authorMatch.Groups[1].Value.Trim());
        }

        // Try article:author
        var articleAuthorMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']article:author[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (articleAuthorMatch.Success)
        {
            return DecodeHtml(articleAuthorMatch.Groups[1].Value.Trim());
        }

        // Try DC.creator
        var dcCreatorMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']DC\.creator[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (dcCreatorMatch.Success)
        {
            return DecodeHtml(dcCreatorMatch.Groups[1].Value.Trim());
        }

        return null;
    }

    /// <summary>
    /// Extracts published date from HTML.
    /// </summary>
    private string? ExtractPublishedDate(string html)
    {
        // Try article:published_time
        var publishedMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']article:published_time[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (publishedMatch.Success)
        {
            return FormatDate(publishedMatch.Groups[1].Value.Trim());
        }

        // Try datePublished in JSON-LD
        var jsonLdMatch = Regex.Match(html, 
            @"""datePublished""\s*:\s*""([^""]+)""", 
            RegexOptions.IgnoreCase);
        
        if (jsonLdMatch.Success)
        {
            return FormatDate(jsonLdMatch.Groups[1].Value.Trim());
        }

        // Try DC.date
        var dcDateMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']DC\.date[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (dcDateMatch.Success)
        {
            return FormatDate(dcDateMatch.Groups[1].Value.Trim());
        }

        return null;
    }

    /// <summary>
    /// Extracts og:image URL from HTML.
    /// </summary>
    private string? ExtractOgImage(string html)
    {
        var ogImageMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:image[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogImageMatch.Success)
        {
            return ogImageMatch.Groups[1].Value.Trim();
        }

        // Try alternate format
        var ogImageAltMatch = Regex.Match(html, 
            @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:image[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogImageAltMatch.Success)
        {
            return ogImageAltMatch.Groups[1].Value.Trim();
        }

        // Try twitter:image
        var twitterImageMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']twitter:image[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (twitterImageMatch.Success)
        {
            return twitterImageMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts site name from HTML.
    /// </summary>
    private string? ExtractSiteName(string html)
    {
        var siteNameMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:site_name[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (siteNameMatch.Success)
        {
            return DecodeHtml(siteNameMatch.Groups[1].Value.Trim());
        }

        // Try alternate format
        var siteNameAltMatch = Regex.Match(html, 
            @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:site_name[""']", 
            RegexOptions.IgnoreCase);
        
        if (siteNameAltMatch.Success)
        {
            return DecodeHtml(siteNameAltMatch.Groups[1].Value.Trim());
        }

        return null;
    }

    /// <summary>
    /// Extracts locale from HTML.
    /// </summary>
    private string? ExtractLocale(string html)
    {
        // Try og:locale
        var localeMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:locale[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (localeMatch.Success)
        {
            return localeMatch.Groups[1].Value.Trim();
        }

        // Try html lang attribute
        var langMatch = Regex.Match(html, 
            @"<html[^>]+lang=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (langMatch.Success)
        {
            return langMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts content type (article, website, etc.) from HTML.
    /// </summary>
    private string? ExtractContentType(string html)
    {
        var typeMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:type[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (typeMatch.Success)
        {
            return typeMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Formats a date string to a readable format.
    /// </summary>
    private string FormatDate(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var date))
        {
            return date.ToString("yyyy-MM-dd");
        }
        return dateStr;
    }

    /// <summary>
    /// Extracts keywords from HTML meta tags and content.
    /// </summary>
    private List<string> ExtractKeywords(string html)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract from meta keywords tag
        var keywordsMatch = Regex.Match(html, 
            @"<meta[^>]+name=[""']keywords[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (keywordsMatch.Success)
        {
            var keywordText = keywordsMatch.Groups[1].Value;
            var keywordArray = keywordText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var keyword in keywordArray)
            {
                var trimmed = keyword.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length > 2)
                {
                    keywords.Add(trimmed);
                }
            }
        }

        // Extract from article:tag (Open Graph)
        var tagMatches = Regex.Matches(html, 
            @"<meta[^>]+property=[""']article:tag[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        foreach (Match match in tagMatches)
        {
            var tag = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(tag) && tag.Length > 2)
            {
                keywords.Add(tag);
            }
        }

        // Extract from heading tags (h1, h2, h3)
        var headingMatches = Regex.Matches(html, @"<h[1-3][^>]*>(.*?)</h[1-3]>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        foreach (Match match in headingMatches)
        {
            var headingText = StripHtmlTags(match.Groups[1].Value).Trim();
            if (!string.IsNullOrWhiteSpace(headingText))
            {
                // Extract significant words from headings (3+ characters)
                var words = Regex.Split(headingText, @"\s+")
                    .Where(w => w.Length >= 3 && !IsCommonWord(w))
                    .Take(5); // Limit words per heading
                
                foreach (var word in words)
                {
                    keywords.Add(word);
                }
            }

            if (keywords.Count >= 20) break; // Limit total keywords
        }

        return keywords.Take(15).ToList(); // Return top 15 keywords
    }

    /// <summary>
    /// Extracts a content summary from the page body.
    /// </summary>
    private string ExtractContentSummary(string html)
    {
        // Remove script and style tags
        var cleanHtml = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<style[^>]*>.*?</style>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<nav[^>]*>.*?</nav>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<footer[^>]*>.*?</footer>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<header[^>]*>.*?</header>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanHtml = Regex.Replace(cleanHtml, @"<!--.*?-->", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Try to extract main content from common container tags
        var contentPatterns = new[]
        {
            @"<main[^>]*>(.*?)</main>",
            @"<article[^>]*>(.*?)</article>",
            @"<div[^>]*class=[""'][^""']*content[^""']*[""'][^>]*>(.*?)</div>",
            @"<div[^>]*id=[""']content[""'][^>]*>(.*?)</div>",
            @"<body[^>]*>(.*?)</body>"
        };

        string mainContent = null;
        foreach (var pattern in contentPatterns)
        {
            var match = Regex.Match(cleanHtml, pattern, 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (match.Success)
            {
                mainContent = match.Groups[1].Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(mainContent))
        {
            mainContent = cleanHtml;
        }

        // Extract paragraph text
        var paragraphs = new List<string>();
        var paraMatches = Regex.Matches(mainContent, @"<p[^>]*>(.*?)</p>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in paraMatches)
        {
            var paraText = StripHtmlTags(match.Groups[1].Value).Trim();
            if (!string.IsNullOrWhiteSpace(paraText) && paraText.Length > 50)
            {
                paragraphs.Add(paraText);
            }

            if (paragraphs.Count >= 5) break; // Limit to first 5 substantial paragraphs
        }

        if (paragraphs.Count > 0)
        {
            var summary = string.Join("\n\n", paragraphs);
            
            // Truncate to reasonable length
            if (summary.Length > 1000)
            {
                summary = summary.Substring(0, 997) + "...";
            }

            return summary;
        }

        // Fallback: extract any text content
        var allText = StripHtmlTags(mainContent);
        allText = Regex.Replace(allText, @"\s+", " ").Trim();
        
        if (allText.Length > 500)
        {
            allText = allText.Substring(0, 497) + "...";
        }

        return allText;
    }

    /// <summary>
    /// Strips HTML tags from text.
    /// </summary>
    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Remove HTML tags
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        
        // Decode HTML entities
        text = DecodeHtml(text);
        
        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }

    /// <summary>
    /// Decodes HTML entities.
    /// </summary>
    private string DecodeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&amp;", "&")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&apos;", "'")
                   .Replace("&nbsp;", " ")
                   .Replace("&#160;", " ")
                   .Replace("&ndash;", "-")
                   .Replace("&mdash;", "--")
                   .Replace("&lsquo;", "'")
                   .Replace("&rsquo;", "'")
                   .Replace("&ldquo;", "\"")
                   .Replace("&rdquo;", "\"")
                   .Replace("&hellip;", "...")
                   .Replace("&copy;", "(c)")
                   .Replace("&reg;", "(R)")
                   .Replace("&trade;", "(TM)");

        // Decode numeric entities
        text = Regex.Replace(text, @"&#(\d+);", m =>
        {
            if (int.TryParse(m.Groups[1].Value, out var code))
            {
                return ((char)code).ToString();
            }
            return m.Value;
        });

        // Decode hex entities
        text = Regex.Replace(text, @"&#x([0-9a-fA-F]+);", m =>
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var code))
            {
                return ((char)code).ToString();
            }
            return m.Value;
        });

        return text;
    }

    /// <summary>
    /// Checks if a word is a common word that should be filtered from keywords.
    /// </summary>
    private bool IsCommonWord(string word)
    {
        var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "are", "but", "not", "you", "all", "can", "her", "was", "one", "our",
            "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see",
            "two", "way", "who", "boy", "did", "got", "let", "put", "say", "she", "too", "use", "this",
            "that", "with", "have", "from", "they", "will", "your", "what", "been", "more", "when",
            "some", "time", "very", "than", "them", "into", "could", "other", "these", "there", "their",
            "also", "just", "like", "would", "should", "about", "after", "before", "being", "between"
        };

        return commonWords.Contains(word);
    }
}

/// <summary>
/// Result from web page summarization.
/// </summary>
public class WebPageSummary
{
    public string Url { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string ContentSummary { get; set; } = string.Empty;
    public bool WasCancelled { get; set; }
    
    // New properties for enhanced metadata
    public bool IsBinaryContent { get; set; }
    public string? MediaType { get; set; }
    public string? Author { get; set; }
    public string? PublishedDate { get; set; }
    public string? ImageUrl { get; set; }
    public string? SiteName { get; set; }
    public string? Locale { get; set; }
    public string? ContentType { get; set; }
}
