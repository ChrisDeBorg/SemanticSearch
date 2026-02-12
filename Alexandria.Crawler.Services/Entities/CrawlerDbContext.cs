using Alexandria.Crawler.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Net.Http.Json;
using System.Text.Json.Serialization; // <-- Hinzugefügt für PropertyBuilder-Erweiterungen


namespace Alexandria.Crawler.Data;

public class CrawlerDbContext : DbContext
{
    public CrawlerDbContext(DbContextOptions<CrawlerDbContext> options)
        : base(options)
    {
    }

    public DbSet<CrawledPage> CrawledPages { get; set; }
    public DbSet<CrawlSession> CrawlSessions { get; set; }
    public DbSet<ExtractedLink> ExtractedLinks { get; set; }
    public DbSet<ExtractedImage> ExtractedImages { get; set; }
    public DbSet<PageMetaData> PageMetaData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CrawledPage Konfiguration
        modelBuilder.Entity<CrawledPage>(entity =>
        {
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.CrawledAt);
            entity.HasIndex(e => e.ContentHash);

            entity.Property(e => e.Content);

            entity.HasOne(e => e.CrawlSession)
                .WithMany(s => s.CrawledPages)
                .HasForeignKey(e => e.CrawlSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CrawlSession Konfiguration
        modelBuilder.Entity<CrawlSession>(entity =>
        {
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);
        });

        // ExtractedLink Konfiguration
        modelBuilder.Entity<ExtractedLink>(entity =>
        {
            entity.HasIndex(e => new { e.SourcePageId, e.TargetUrl });

            entity.HasOne(e => e.SourcePage)
                .WithMany(p => p.ExtractedLinks)
                .HasForeignKey(e => e.SourcePageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ExtractedImage Konfiguration
        modelBuilder.Entity<ExtractedImage>(entity =>
        {
            entity.HasOne(e => e.Page)
                .WithMany(p => p.ExtractedImages)
                .HasForeignKey(e => e.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PageMetaData Konfiguration
        modelBuilder.Entity<PageMetaData>(entity =>
        {
            entity.HasOne(e => e.Page)
                .WithMany(p => p.MetaData)
                .HasForeignKey(e => e.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}