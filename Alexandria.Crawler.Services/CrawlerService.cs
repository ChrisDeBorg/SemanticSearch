using Alexandria.Crawler.Services.Models;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Alexandria.Crawler.Services;

public class CrawlerService : ICrawlerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CrawlerService> _logger;
    private readonly IBrowsingContext _browsingContext;
    private readonly ConcurrentDictionary<string, bool> _visitedUrls;
    private readonly SemaphoreSlim _rateLimiter;

    public CrawlerService(
        IHttpClientFactory httpClientFactory,
        ILogger<CrawlerService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CrawlerClient");
        _logger = logger;
        _browsingContext = BrowsingContext.New(Configuration.Default);
        _visitedUrls = new ConcurrentDictionary<string, bool>();
        _rateLimiter = new SemaphoreSlim(1, 1); // 1 Request zur Zeit
    }

    public async Task<CrawlResult> CrawlAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = new CrawlResult { Url = url };

        try
        {
            // Rate Limiting: 1 Sekunde zwischen Requests
            await _rateLimiter.WaitAsync(cancellationToken);
            await Task.Delay(1000, cancellationToken);
            _rateLimiter.Release();

            // Robots.txt Check
            if (!await IsUrlAllowedAsync(url))
            {
                result.Success = false;
                result.ErrorMessage = "Blocked by robots.txt";
                _logger.LogWarning("URL blocked by robots.txt: {Url}", url);
                return result;
            }

            // HTTP Request mit Polly Retry
            var response = await _httpClient.GetAsync(url, cancellationToken);
            result.StatusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.ErrorMessage = $"HTTP {result.StatusCode}";
                return result;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var rawDocument = await _browsingContext.OpenAsync(req => req.Content(html), cancellationToken);

            if (rawDocument == null || rawDocument is not IHtmlDocument)
            {
                result.Success = false;
                result.ErrorMessage = "Not a valid HTML document";
                return result;
            }

            var document = (IHtmlDocument)rawDocument;
            // Extrahiere Daten
            result.Title = document.Title ?? string.Empty;
            result.Content = ExtractTextContent(document);
            result.Links = ExtractLinks(document, url);
            result.Images = ExtractImages(document, url);
            result.MetaData = ExtractMetaData(document);
            result.Success = true;

            _logger.LogInformation("Successfully crawled: {Url} - Found {LinkCount} links", url, result.Links.Count);
        }
        catch (TaskCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Request timeout";
            _logger.LogWarning("Timeout crawling: {Url}", url);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error crawling: {Url}", url);
        }

        return result;
    }

    public async Task<List<CrawlResult>> CrawlDeepAsync(
        string startUrl,
        int maxDepth,
        int maxPages,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CrawlResult>();
        var queue = new Queue<CrawlJob>();
        queue.Enqueue(new CrawlJob { Url = startUrl, Depth = 0 });

        _visitedUrls.Clear();

        while (queue.Count > 0 && results.Count < maxPages && !cancellationToken.IsCancellationRequested)
        {
            var job = queue.Dequeue();

            // Skip bereits besuchte URLs
            if (!_visitedUrls.TryAdd(job.Url, true))
                continue;

            _logger.LogInformation("Crawling [{Depth}/{MaxDepth}]: {Url}", job.Depth, maxDepth, job.Url);

            var result = await CrawlAsync(job.Url, cancellationToken);
            results.Add(result);

            // Nur weiter crawlen wenn erfolgreich und max Depth nicht erreicht
            if (result.Success && job.Depth < maxDepth)
            {
                foreach (var link in result.Links.Take(10)) // Max 10 Links pro Seite folgen
                {
                    if (!_visitedUrls.ContainsKey(link) && IsSameDomain(startUrl, link))
                    {
                        queue.Enqueue(new CrawlJob { Url = link, Depth = job.Depth + 1 });
                    }
                }
            }
        }

        _logger.LogInformation("Deep crawl completed. Crawled {Count} pages", results.Count);
        return results;
    }

    public async Task<bool> IsUrlAllowedAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            var robotsUrl = $"{uri.Scheme}://{uri.Host}/robots.txt";

            var response = await _httpClient.GetAsync(robotsUrl);
            if (!response.IsSuccessStatusCode)
                return true; // Kein robots.txt = erlaubt

            var robotsTxt = await response.Content.ReadAsStringAsync();

            // Einfache robots.txt Parsing (User-agent: * + Disallow)
            var lines = robotsTxt.Split('\n');
            var isUserAgentAll = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains("*"))
                {
                    isUserAgentAll = true;
                }
                else if (isUserAgentAll &&
                         trimmed.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var disallowPath = trimmed.Substring(9).Trim();
                    if (!string.IsNullOrEmpty(disallowPath) &&
                        uri.AbsolutePath.StartsWith(disallowPath))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return true; // Bei Fehler erlauben
        }
    }

    private string ExtractTextContent(IHtmlDocument document)
    {
        // Entferne Script, Style, Nav, Footer
        var elementsToRemove = document.QuerySelectorAll("script, style, nav, footer, header");
        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }

        var body = document.Body?.TextContent ?? string.Empty;

        // Normalisiere Whitespace
        return System.Text.RegularExpressions.Regex.Replace(body, @"\s+", " ").Trim();
    }

    private List<string> ExtractLinks(IHtmlDocument document, string baseUrl)
    {
        var links = new List<string>();
        var anchorElements = document.QuerySelectorAll("a[href]");

        foreach (var anchor in anchorElements)
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href)) continue;

            try
            {
                var absoluteUrl = new Uri(new Uri(baseUrl), href).ToString();

                // Nur HTTP(S) Links
                if (absoluteUrl.StartsWith("http://") || absoluteUrl.StartsWith("https://"))
                {
                    links.Add(absoluteUrl);
                }
            }
            catch { /* Ignoriere ungültige URLs */ }
        }

        return links.Distinct().ToList();
    }

    private List<string> ExtractImages(IHtmlDocument document, string baseUrl)
    {
        var images = new List<string>();
        var imgElements = document.QuerySelectorAll("img[src]");

        foreach (var img in imgElements)
        {
            var src = img.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(src)) continue;

            try
            {
                var absoluteUrl = new Uri(new Uri(baseUrl), src).ToString();
                images.Add(absoluteUrl);
            }
            catch { /* Ignoriere ungültige URLs */ }
        }

        return images.Distinct().ToList();
    }

    private Dictionary<string, string> ExtractMetaData(IHtmlDocument document)
    {
        var metaData = new Dictionary<string, string>();
        var metaTags = document.QuerySelectorAll("meta");

        foreach (var meta in metaTags)
        {
            var name = meta.GetAttribute("name") ?? meta.GetAttribute("property");
            var content = meta.GetAttribute("content");

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(content))
            {
                metaData[name] = content;
            }
        }

        return metaData;
    }

    private bool IsSameDomain(string url1, string url2)
    {
        try
        {
            var uri1 = new Uri(url1);
            var uri2 = new Uri(url2);
            return uri1.Host == uri2.Host;
        }
        catch
        {
            return false;
        }
    }
}
