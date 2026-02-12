namespace Alexandria.Crawler.Services.Models;

public class CrawlJob
{
    public string Url { get; set; } = string.Empty;
    public int Depth { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
