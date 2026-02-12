using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alexandria.Crawler.Data.Entities;

[Table("CrawlSessions")]
[Index(nameof(StartedAt))]
public class CrawlSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(2048)]
    public string StartUrl { get; set; } = string.Empty;

    public int MaxDepth { get; set; }

    public int MaxPages { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int TotalPagesCrawled { get; set; }

    public int SuccessfulPages { get; set; }

    public int FailedPages { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled

    // Navigation Properties
    public ICollection<CrawledPage> CrawledPages { get; set; } = new List<CrawledPage>();
}