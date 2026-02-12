using Alexandria.Crawler.Services.Models;

namespace Alexandria.Crawler.Services;

public interface ICrawlerService
{
    Task<CrawlResult> CrawlAsync(string url, CancellationToken cancellationToken = default);
    Task<List<CrawlResult>> CrawlDeepAsync(string startUrl, int maxDepth, int maxPages, CancellationToken cancellationToken = default);
    Task<bool> IsUrlAllowedAsync(string url);
}
