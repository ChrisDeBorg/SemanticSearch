using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alexandria.Crawler.Data.Entities;

[Table("PageMetaData")]
[Index(nameof(PageId))]
[Index(nameof(MetaKey))]
public class PageMetaData
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PageId { get; set; }

    [ForeignKey(nameof(PageId))]
    public CrawledPage? Page { get; set; }

    [Required]
    [MaxLength(200)]
    public string MetaKey { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? MetaValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}