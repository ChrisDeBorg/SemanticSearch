using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alexandria.Crawler.Data.Entities;

[Table("CrawledPages")]
[Index(nameof(Url), IsUnique = true)]
[Index(nameof(Domain))]
[Index(nameof(CrawledAt))]
public class CrawledPage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Domain { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Title { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(64)")]
    [MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty; // SHA256 für Duplikat-Erkennung

    public int StatusCode { get; set; }

    public bool IsSuccess { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastModified { get; set; }

    public int CrawlDepth { get; set; }

    public Guid? CrawlSessionId { get; set; }

    [ForeignKey(nameof(CrawlSessionId))]
    public CrawlSession? CrawlSession { get; set; }

    // Navigation Properties
    public ICollection<ExtractedLink> ExtractedLinks { get; set; } = new List<ExtractedLink>();
    public ICollection<ExtractedImage> ExtractedImages { get; set; } = new List<ExtractedImage>();
    public ICollection<PageMetaData> MetaData { get; set; } = new List<PageMetaData>();
}
