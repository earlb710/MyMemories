using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isChecking;

    public UrlStateCheckerService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10) // 10 second timeout per URL
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
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
                    var status = await CheckUrlAsync(link.Url, _cancellationTokenSource.Token);
                    link.UrlStatus = status;
                    link.UrlLastChecked = DateTime.Now;
                    
                    // Get detailed message for non-accessible URLs
                    if (status != UrlStatus.Accessible)
                    {
                        var (_, message) = await CheckSingleUrlAsync(link.Url);
                        link.UrlStatusMessage = message;
                    }
                    else
                    {
                        link.UrlStatusMessage = "URL is accessible";
                    }

                    switch (status)
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

                    Debug.WriteLine($"[UrlStateChecker] {i + 1}/{stats.TotalUrls}: {link.Title} = {status}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UrlStateChecker] Exception checking {link.Url}: {ex.Message}");
                    link.UrlStatus = UrlStatus.Error;
                    link.UrlLastChecked = DateTime.Now;
                    link.UrlStatusMessage = $"Exception: {ex.Message}";
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

            if (response.IsSuccessStatusCode)
            {
                return (UrlStatus.Accessible, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            else if ((int)response.StatusCode == 404 || (int)response.StatusCode == 410)
            {
                // 404 Not Found or 410 Gone
                return (UrlStatus.NotFound, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            else
            {
                // Other HTTP errors (403, 500, etc.)
                return (UrlStatus.Error, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            // DNS failure, connection refused, etc.
            return (UrlStatus.NotFound, $"Connection failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            // Timeout
            return (UrlStatus.Error, "Request timed out");
        }
        catch (Exception ex)
        {
            // Other errors (invalid URL, etc.)
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
        }
    }
}

/// <summary>
/// Statistics from a URL check operation.
/// </summary>
public class UrlCheckStatistics
{
    public int TotalUrls { get; set; }
    public int AccessibleCount { get; set; }
    public int ErrorCount { get; set; }
    public int NotFoundCount { get; set; }
    public int CheckedCount => AccessibleCount + ErrorCount + NotFoundCount;
}
