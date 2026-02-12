using Alexandria.Crawler.Data;
using Alexandria.Crawler.Data.Entities;
using Alexandria.Crawler.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Alexandria.Crawler.Repositories;

public class CrawlerRepository : ICrawlerRepository
{
    private readonly CrawlerDbContext _context;

    public CrawlerRepository(CrawlerDbContext context)
    {
        _context = context;
    }

    public async Task<CrawlSession> CreateSessionAsync(string startUrl, int maxDepth, int maxPages)
    {
        var session = new CrawlSession
        {
            StartUrl = startUrl,
            MaxDepth = maxDepth,
            MaxPages = maxPages,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        _context.CrawlSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<CrawlSession?> GetSessionAsync(Guid sessionId)
    {
        return await _context.CrawlSessions
            .Include(s => s.CrawledPages)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public async Task UpdateSessionAsync(CrawlSession session)
    {
        _context.CrawlSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CrawlSession>> GetRecentSessionsAsync(int count = 10)
    {
        return await _context.CrawlSessions
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<CrawledPage> SavePageAsync(CrawlResult crawlResult, Guid sessionId, int depth)
    {
        var uri = new Uri(crawlResult.Url);
        var contentHash = ComputeHash(crawlResult.Content);

        // Check ob URL bereits existiert
        var existingPage = await _context.CrawledPages
            .FirstOrDefaultAsync(p => p.Url == crawlResult.Url);

        if (existingPage != null)
        {
            // Update existing page
            existingPage.Title = crawlResult.Title;
            existingPage.Content = crawlResult.Content;
            existingPage.ContentHash = contentHash;
            existingPage.StatusCode = crawlResult.StatusCode;
            existingPage.IsSuccess = crawlResult.Success;
            existingPage.ErrorMessage = crawlResult.ErrorMessage;
            existingPage.CrawledAt = DateTime.UtcNow;
            existingPage.CrawlDepth = depth;

            _context.CrawledPages.Update(existingPage);
            await _context.SaveChangesAsync();

            return existingPage;
        }

        // Create new page
        var page = new CrawledPage
        {
            Url = crawlResult.Url,
            Domain = uri.Host,
            Title = crawlResult.Title,
            Content = crawlResult.Content,
            ContentHash = contentHash,
            StatusCode = crawlResult.StatusCode,
            IsSuccess = crawlResult.Success,
            ErrorMessage = crawlResult.ErrorMessage,
            CrawledAt = DateTime.UtcNow,
            CrawlDepth = depth,
            CrawlSessionId = sessionId
        };

        _context.CrawledPages.Add(page);
        await _context.SaveChangesAsync();

        // Save Links
        foreach (var link in crawlResult.Links)
        {
            var extractedLink = new ExtractedLink
            {
                SourcePageId = page.Id,
                TargetUrl = link,
                IsInternal = IsSameDomain(crawlResult.Url, link)
            };
            _context.ExtractedLinks.Add(extractedLink);
        }

        // Save Images
        foreach (var image in crawlResult.Images)
        {
            var extractedImage = new ExtractedImage
            {
                PageId = page.Id,
                ImageUrl = image
            };
            _context.ExtractedImages.Add(extractedImage);
        }

        // Save MetaData
        foreach (var meta in crawlResult.MetaData)
        {
            var metaData = new PageMetaData
            {
                PageId = page.Id,
                MetaKey = meta.Key,
                MetaValue = meta.Value
            };
            _context.PageMetaData.Add(metaData);
        }

        await _context.SaveChangesAsync();

        return page;
    }

    public async Task<CrawledPage?> GetPageByUrlAsync(string url)
    {
        return await _context.CrawledPages
            .Include(p => p.ExtractedLinks)
            .Include(p => p.ExtractedImages)
            .Include(p => p.MetaData)
            .FirstOrDefaultAsync(p => p.Url == url);
    }

    public async Task<bool> IsUrlCrawledAsync(string url)
    {
        return await _context.CrawledPages.AnyAsync(p => p.Url == url);
    }

    public async Task<List<CrawledPage>> GetPagesByDomainAsync(string domain, int skip = 0, int take = 50)
    {
        return await _context.CrawledPages
            .Where(p => p.Domain == domain)
            .OrderByDescending(p => p.CrawledAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<CrawledPage>> GetPagesBySessionAsync(Guid sessionId)
    {
        return await _context.CrawledPages
            .Where(p => p.CrawlSessionId == sessionId)
            .OrderBy(p => p.CrawlDepth)
            .ThenBy(p => p.CrawledAt)
            .ToListAsync();
    }

    public async Task<int> GetTotalPagesCountAsync()
    {
        return await _context.CrawledPages.CountAsync();
    }

    public async Task<List<CrawledPage>> SearchPagesAsync(string searchTerm, int skip = 0, int take = 20)
    {
        return await _context.CrawledPages
            .Where(p => p.Title.Contains(searchTerm) || p.Content.Contains(searchTerm))
            .OrderByDescending(p => p.CrawledAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetDomainStatisticsAsync()
    {
        return await _context.CrawledPages
            .GroupBy(p => p.Domain)
            .Select(g => new { Domain = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToDictionaryAsync(x => x.Domain, x => x.Count);
    }

    public async Task<List<CrawledPage>> GetRecentPagesAsync(int count = 20)
    {
        return await _context.CrawledPages
            .OrderByDescending(p => p.CrawledAt)
            .Take(count)
            .ToListAsync();
    }

    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        //return Convert.ToBase64String(hash);
        // Hex (64 chars) statt Base64 -> konsistente Länge für DB
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
