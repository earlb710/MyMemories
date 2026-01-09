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

    static WebSummaryService()
    {
        // Set a user agent to avoid being blocked by some websites
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Fetches and summarizes a web page from the given URL.
    /// </summary>
    public async Task<WebPageSummary> SummarizeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var summary = new WebPageSummary { Url = url };

        try
        {
            // Fetch the HTML content with cancellation support
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

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

            summary.Success = true;
            summary.StatusCode = (int)response.StatusCode;
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

        // Try to extract from og:description meta tag
        var ogDescMatch = Regex.Match(html, 
            @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""']([^""']+)[""']", 
            RegexOptions.IgnoreCase);
        
        if (ogDescMatch.Success)
        {
            return DecodeHtml(ogDescMatch.Groups[1].Value.Trim());
        }

        return string.Empty;
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

        // Try to extract main content from common container tags
        var contentPatterns = new[]
        {
            @"<main[^>]*>(.*?)</main>",
            @"<article[^>]*>(.*?)</article>",
            @"<div[^>]*class=[""'][^""']*content[^""']*[""'][^>]*>(.*?)</div>",
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
                   .Replace("&#160;", " ");

        // Decode numeric entities
        text = Regex.Replace(text, @"&#(\d+);", m =>
        {
            if (int.TryParse(m.Groups[1].Value, out var code))
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
            "some", "time", "very", "than", "them", "into", "could", "other", "these", "there", "their"
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
}
