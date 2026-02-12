using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alexandria.Crawler.Data.Entities;

[Table("ExtractedImages")]
[Index(nameof(PageId))]
public class ExtractedImage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PageId { get; set; }

    [ForeignKey(nameof(PageId))]
    public CrawledPage? Page { get; set; }

    [Required]
    [MaxLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AltText { get; set; }

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
