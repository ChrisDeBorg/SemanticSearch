using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alexandria.Crawler.Data.Entities;

[Table("ExtractedLinks")]
[Index(nameof(SourcePageId))]
[Index(nameof(TargetUrl))]
public class ExtractedLink
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourcePageId { get; set; }

    [ForeignKey(nameof(SourcePageId))]
    public CrawledPage? SourcePage { get; set; }

    [Required]
    [MaxLength(2048)]
    public string TargetUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AnchorText { get; set; }

    public bool IsInternal { get; set; }

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
