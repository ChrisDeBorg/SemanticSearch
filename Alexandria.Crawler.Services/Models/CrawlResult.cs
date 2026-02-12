using System;
using System.Collections.Generic;
using System.Text;

namespace Alexandria.Crawler.Services.Models;

public class CrawlResult
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Links { get; set; } = new();
    public List<string> Images { get; set; } = new();
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> MetaData { get; set; } = new();
}
