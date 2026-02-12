using Alexandria.Crawler.Data.Entities;
using Alexandria.Crawler.Services.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Alexandria.Crawler.Repositories;

public interface ICrawlerRepository
{
    // CrawlSession Operations
    Task<CrawlSession> CreateSessionAsync(string startUrl, int maxDepth, int maxPages);
    Task<CrawlSession?> GetSessionAsync(Guid sessionId);
    Task UpdateSessionAsync(CrawlSession session);
    Task<List<CrawlSession>> GetRecentSessionsAsync(int count = 10);

    // CrawledPage Operations
    Task<CrawledPage> SavePageAsync(CrawlResult crawlResult, Guid sessionId, int depth);
    Task<CrawledPage?> GetPageByUrlAsync(string url);
    Task<bool> IsUrlCrawledAsync(string url);
    Task<List<CrawledPage>> GetPagesByDomainAsync(string domain, int skip = 0, int take = 50);
    Task<List<CrawledPage>> GetPagesBySessionAsync(Guid sessionId);
    Task<int> GetTotalPagesCountAsync();

    // Search & Analytics
    Task<List<CrawledPage>> SearchPagesAsync(string searchTerm, int skip = 0, int take = 20);
    Task<Dictionary<string, int>> GetDomainStatisticsAsync();
    Task<List<CrawledPage>> GetRecentPagesAsync(int count = 20);
}
