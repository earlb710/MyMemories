using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MyMemories.Services;

/// <summary>
/// Service for checking URL accessibility status in bookmark categories.
/// </summary>
public class UrlStateCheckerService
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _noRedirectHttpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isChecking;

    public UrlStateCheckerService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10) // 10 second timeout per URL
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        // Create a separate HttpClient that doesn't follow redirects
        var noRedirectHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _noRedirectHttpClient = new HttpClient(noRedirectHandler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _noRedirectHttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Gets whether a check is currently in progress.
    /// </summary>
    public bool IsChecking => _isChecking;

    /// <summary>
    /// Checks all URLs in a category recursively and updates their status.
    /// </summary>
    /// <param name="categoryNode">The category node to check</param>
    /// <param name="progressCallback">Optional callback for progress updates (current, total, url, linkNode)</param>
    /// <returns>Statistics about the check</returns>
    public async Task<UrlCheckStatistics> CheckCategoryUrlsAsync(
        TreeViewNode categoryNode, 
        Action<int, int, string, TreeViewNode?>? progressCallback = null)
    {
        if (_isChecking)
        {
            throw new InvalidOperationException("A URL check is already in progress");
        }

        _isChecking = true;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var stats = new UrlCheckStatistics();
            var urlLinkPairs = new List<(LinkItem link, TreeViewNode node)>();

            // Collect all URL links from the category with their nodes
            CollectUrlLinksWithNodes(categoryNode, urlLinkPairs);
            stats.TotalUrls = urlLinkPairs.Count;

            Debug.WriteLine($"[UrlStateChecker] Found {stats.TotalUrls} URL links to check");

            // Check each URL
            for (int i = 0; i < urlLinkPairs.Count; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Debug.WriteLine("[UrlStateChecker] Check cancelled by user");
                    break;
                }

                var (link, node) = urlLinkPairs[i];
                
                // Validate node exists in tree before checking
                if (node?.Content == null || node.Content != link)
                {
                    Debug.WriteLine($"[UrlStateChecker] Node not found for link: {link.Title}");
                    throw new InvalidOperationException($"Tree node not found for link '{link.Title}'. Check cancelled.");
                }
                
                progressCallback?.Invoke(i + 1, stats.TotalUrls, link.Url, node);

                try
                {
                    // Use the new redirect-aware check
                    var result = await CheckUrlWithRedirectAsync(link.Url, _cancellationTokenSource.Token);
                    
                    link.UrlStatus = result.Status;
                    link.UrlStatusMessage = result.Message;
                    link.UrlLastChecked = DateTime.Now;
                    
                    // Update redirect information
                    if (result.RedirectDetected && !string.IsNullOrEmpty(result.RedirectUrl))
                    {
                        link.RedirectUrl = result.RedirectUrl;
                        stats.RedirectCount++;
                        Debug.WriteLine($"[UrlStateChecker] Redirect detected: {link.Url} -> {result.RedirectUrl}");
                    }
                    else
                    {
                        link.RedirectUrl = null;
                    }

                    switch (result.Status)
                    {
                        case UrlStatus.Accessible:
                            stats.AccessibleCount++;
                            break;
                        case UrlStatus.Error:
                            stats.ErrorCount++;
                            break;
                        case UrlStatus.NotFound:
                            stats.NotFoundCount++;
                            break;
                    }

                    Debug.WriteLine($"[UrlStateChecker] {i + 1}/{stats.TotalUrls}: {link.Title} = {result.Status}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UrlStateChecker] Exception checking {link.Url}: {ex.Message}");
                    link.UrlStatus = UrlStatus.Error;
                    link.UrlLastChecked = DateTime.Now;
                    link.UrlStatusMessage = $"Exception: {ex.Message}";
                    link.RedirectUrl = null;
                    stats.ErrorCount++;
                }
            }

            return stats;
        }
        finally
        {
            _isChecking = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Cancels the current URL check operation.
    /// </summary>
    public void CancelCheck()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Checks a single URL and returns its status with a detailed message.
    /// </summary>
    /// <returns>Tuple of (UrlStatus, DetailedMessage)</returns>
    public async Task<(UrlStatus status, string message)> CheckSingleUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (UrlStatus.NotFound, "URL is empty");
        }

        // Only check HTTP/HTTPS URLs
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return (UrlStatus.Unknown, "Not an HTTP/HTTPS URL");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, CancellationToken.None);

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return (UrlStatus.Accessible, $"HTTP {statusCode} {response.ReasonPhrase}");
            }
            else if (statusCode == 404 || statusCode == 410)
            {
                return (UrlStatus.NotFound, $"HTTP {statusCode} {response.ReasonPhrase}");
            }
            else
            {
                return (UrlStatus.Error, $"HTTP {statusCode} {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            return (UrlStatus.NotFound, $"Connection failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (UrlStatus.Error, "Request timed out");
        }
        catch (Exception ex)
        {
            return (UrlStatus.Error, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks a single URL and returns its status.
    /// </summary>
    private async Task<UrlStatus> CheckUrlAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return UrlStatus.NotFound;
        }

        // Only check HTTP/HTTPS URLs
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return UrlStatus.Unknown;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return UrlStatus.Accessible;
            }
            else if ((int)response.StatusCode == 404 || (int)response.StatusCode == 410)
            {
                // 404 Not Found or 410 Gone
                return UrlStatus.NotFound;
            }
            else
            {
                // Other HTTP errors (403, 500, etc.)
                return UrlStatus.Error;
            }
        }
        catch (HttpRequestException)
        {
            // DNS failure, connection refused, etc.
            return UrlStatus.NotFound;
        }
        catch (TaskCanceledException)
        {
            // Timeout or cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                throw; // Re-throw if user cancelled
            }
            return UrlStatus.Error; // Timeout
        }
        catch (Exception)
        {
            // Other errors (invalid URL, etc.)
            return UrlStatus.Error;
        }
    }

    /// <summary>
    /// Recursively collects all URL links from a category node WITH their tree nodes.
    /// </summary>
    private void CollectUrlLinksWithNodes(TreeViewNode node, List<(LinkItem link, TreeViewNode node)> urlLinkPairs)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Only include web URLs (not directories or files)
                if (!link.IsDirectory && 
                    !string.IsNullOrEmpty(link.Url) &&
                    (link.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     link.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    urlLinkPairs.Add((link, child));
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively check subcategories
                CollectUrlLinksWithNodes(child, urlLinkPairs);
            }
        }
    }

    /// <summary>
    /// Recursively collects all URL links from a category node.
    /// </summary>
    private void CollectUrlLinks(TreeViewNode node, List<LinkItem> urlLinks)
    {
        foreach (var child in node.Children)
        {
            if (child.Content is LinkItem link)
            {
                // Only include web URLs (not directories or files)
                if (!link.IsDirectory && 
                    !string.IsNullOrEmpty(link.Url) &&
                    (link.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     link.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    urlLinks.Add(link);
                }
            }
            else if (child.Content is CategoryItem)
            {
                // Recursively check subcategories
                CollectUrlLinks(child, urlLinks);
            }
        }
    }

    /// <summary>
    /// Resets URL status for all links in a category.
    /// </summary>
    public void ResetCategoryUrlStatus(TreeViewNode categoryNode)
    {
        var urlLinks = new List<LinkItem>();
        CollectUrlLinks(categoryNode, urlLinks);

        foreach (var link in urlLinks)
        {
            link.UrlStatus = UrlStatus.Unknown;
            link.UrlLastChecked = null;
            link.UrlStatusMessage = string.Empty;
            link.RedirectUrl = null;
        }
    }

    /// <summary>
    /// Checks a URL for redirects and returns detailed information including the final URL.
    /// Uses a HashSet to detect redirect loops.
    /// </summary>
    /// <param name="url">The URL to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="maxRedirects">Maximum number of redirects to follow (default: 10)</param>
    /// <returns>A UrlCheckResult with status, message, and redirect information</returns>
    public async Task<UrlCheckResult> CheckUrlWithRedirectAsync(string url, CancellationToken cancellationToken = default, int maxRedirects = 10)
    {
        var result = new UrlCheckResult();

        if (string.IsNullOrWhiteSpace(url))
        {
            result.Status = UrlStatus.NotFound;
            result.Message = "URL is empty";
            return result;
        }

        // Only check HTTP/HTTPS URLs
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            result.Status = UrlStatus.Unknown;
            result.Message = "Not an HTTP/HTTPS URL";
            return result;
        }

        try
        {
            var currentUrl = url;
            var redirectCount = 0;
            string? finalRedirectUrl = null;

            // HashSet to detect redirect loops
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visitedUrls.Add(NormalizeUrlForComparison(url));

            Debug.WriteLine($"[UrlStateChecker] Checking: {url} (max redirects: {maxRedirects})");

            while (redirectCount < maxRedirects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response;
                try
                {
                    using var headRequest = new HttpRequestMessage(HttpMethod.Head, currentUrl);
                    response = await _noRedirectHttpClient.SendAsync(headRequest, cancellationToken);

                    // Some servers return 405 Method Not Allowed for HEAD requests
                    if ((int)response.StatusCode == 405)
                    {
                        response.Dispose();
                        Debug.WriteLine($"[UrlStateChecker] HEAD not allowed, trying GET for: {currentUrl}");
                        using var getRequest = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                        response = await _noRedirectHttpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"[UrlStateChecker] HEAD failed with exception, trying GET: {ex.Message}");
                    using var getRequest = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    response = await _noRedirectHttpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                using (response) // Ensure response is always disposed
                {
                    var statusCode = (int)response.StatusCode;
                    Debug.WriteLine($"[UrlStateChecker] Response: {statusCode} {response.ReasonPhrase} for {currentUrl}");

                    // Check for redirect status codes (301, 302, 303, 307, 308)
                    if (statusCode == 301 || statusCode == 302 || statusCode == 303 || 
                        statusCode == 307 || statusCode == 308)
                    {
                        redirectCount++;
                        var location = response.Headers.Location;

                        Debug.WriteLine($"[UrlStateChecker] Redirect {redirectCount}/{maxRedirects}: Status={statusCode}, Location={location?.ToString() ?? "(null)"}");

                        if (location == null)
                        {
                            result.Status = UrlStatus.Error;
                            result.Message = $"HTTP {statusCode} redirect without Location header";
                            return result;
                        }

                        // Handle relative URLs
                        if (!location.IsAbsoluteUri)
                        {
                            var baseUri = new Uri(currentUrl);
                            location = new Uri(baseUri, location);
                        }

                        var nextUrl = location.ToString();
                        var normalizedNextUrl = NormalizeUrlForComparison(nextUrl);
                        
                        // Check for redirect loop using HashSet
                        if (visitedUrls.Contains(normalizedNextUrl))
                        {
                            Debug.WriteLine($"[UrlStateChecker] REDIRECT LOOP DETECTED! URL already visited: {nextUrl}");
                            Debug.WriteLine($"[UrlStateChecker] Visited URLs: {string.Join(", ", visitedUrls)}");
                            result.Status = UrlStatus.Error;
                            result.Message = $"Redirect loop detected after {redirectCount} redirect(s)";
                            return result;
                        }
                        
                        visitedUrls.Add(normalizedNextUrl);
                        currentUrl = nextUrl;
                        finalRedirectUrl = currentUrl;

                        Debug.WriteLine($"[UrlStateChecker] Following redirect {redirectCount}: {url} -> {currentUrl}");
                        continue;
                    }

                    // We've reached the final destination
                    if (response.IsSuccessStatusCode)
                    {
                        result.Status = UrlStatus.Accessible;
                        result.Message = $"HTTP {statusCode} {response.ReasonPhrase}";
                    }
                    else if (statusCode == 404 || statusCode == 410)
                    {
                        result.Status = UrlStatus.NotFound;
                        result.Message = $"HTTP {statusCode} {response.ReasonPhrase}";
                    }
                    else
                    {
                        result.Status = UrlStatus.Error;
                        result.Message = $"HTTP {statusCode} {response.ReasonPhrase}";
                    }

                    break;
                }
            }

            if (redirectCount >= maxRedirects)
            {
                Debug.WriteLine($"[UrlStateChecker] Max redirects ({maxRedirects}) exceeded");
                result.Status = UrlStatus.Error;
                result.Message = $"Too many redirects (max {maxRedirects})";
                return result;
            }

            // Set redirect information if we followed any redirects
            if (redirectCount > 0 && finalRedirectUrl != null)
            {
                var normalizedOriginal = NormalizeUrlForComparison(url);
                var normalizedFinal = NormalizeUrlForComparison(finalRedirectUrl);
                
                Debug.WriteLine($"[UrlStateChecker] Comparing URLs for redirect detection:");
                Debug.WriteLine($"[UrlStateChecker]   Original: {url}");
                Debug.WriteLine($"[UrlStateChecker]   Normalized Original: {normalizedOriginal}");
                Debug.WriteLine($"[UrlStateChecker]   Final: {finalRedirectUrl}");
                Debug.WriteLine($"[UrlStateChecker]   Normalized Final: {normalizedFinal}");
                
                if (!string.Equals(normalizedOriginal, normalizedFinal, StringComparison.OrdinalIgnoreCase))
                {
                    result.RedirectDetected = true;
                    result.RedirectUrl = finalRedirectUrl;
                    result.RedirectCount = redirectCount;
                    result.Message += $" (redirected {redirectCount}x)";
                    Debug.WriteLine($"[UrlStateChecker] REDIRECT CONFIRMED: {url} -> {finalRedirectUrl}");
                }
                else
                {
                    Debug.WriteLine($"[UrlStateChecker] Redirect NOT detected - URLs normalize to same value");
                }
            }
            else if (redirectCount > 0)
            {
                Debug.WriteLine($"[UrlStateChecker] Had {redirectCount} redirect(s) but finalRedirectUrl is null");
            }

            Debug.WriteLine($"[UrlStateChecker] Check complete: {result.Status} - {result.Message}");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User cancelled - rethrow
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout (TaskCanceledException is a subclass of OperationCanceledException)
            Debug.WriteLine($"[UrlStateChecker] Request timed out for: {url}");
            result.Status = UrlStatus.Error;
            result.Message = "Request timed out";
            return result;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[UrlStateChecker] HttpRequestException: {ex.Message}");
            result.Status = UrlStatus.NotFound;
            result.Message = $"Connection failed: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UrlStateChecker] Exception: {ex.GetType().Name}: {ex.Message}");
            result.Status = UrlStatus.Error;
            result.Message = $"Error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Normalizes a URL for comparison purposes.
    /// Keeps path differences significant (e.g., /page vs /page2) but ignores:
    /// - Case differences
    /// - Trailing slashes on paths
    /// - Default ports
    /// </summary>
    private static string NormalizeUrlForComparison(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            var uri = new Uri(url);
            // Normalize: scheme + host + port (if non-default) + path (without trailing slash) + query
            var normalized = $"{uri.Scheme}://{uri.Host}";
            
            // Add port only if non-default
            if (!uri.IsDefaultPort)
                normalized += $":{uri.Port}";
            
            // Add path (without trailing slash)
            normalized += uri.AbsolutePath.TrimEnd('/');
            
            // Add query if present
            if (!string.IsNullOrEmpty(uri.Query))
                normalized += uri.Query;
                
            return normalized.ToLowerInvariant();
        }
        catch
        {
            return url.TrimEnd('/').ToLowerInvariant();
        }
    }
}

public class UrlCheckStatistics
{
    public int TotalUrls { get; set; }
    public int AccessibleCount { get; set; }
    public int ErrorCount { get; set; }
    public int NotFoundCount { get; set; }
    public int RedirectCount { get; set; }
    public int CheckedCount => AccessibleCount + ErrorCount + NotFoundCount;
}

public class UrlCheckResult
{
    public UrlStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RedirectDetected { get; set; }
    public string? RedirectUrl { get; set; }
    public int RedirectCount { get; set; }
}
