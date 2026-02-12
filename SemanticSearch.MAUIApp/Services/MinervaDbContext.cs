using Microsoft.EntityFrameworkCore;
using Minerva.Persistence;
using Minerva.Persistence.Entities;
//using Minerva.Persistence.Entities.AdministrativeAreas;
using Minerva.Persistence.Relations;
using System.Text.Json;

namespace SemanticSearch.MAUIApp.Services;

/// <summary>
/// Entity Framework DbContext für den Minerva Knowledge Graph.
/// Verwendet SQLite für lokale Speicherung.
/// </summary>
public class MinervaDbContext : DbContext
{
    public MinervaDbContext(DbContextOptions<MinervaDbContext> options)
        : base(options)
    {
    }

    // Entities
    public DbSet<BaseEntity> BaseEntities { get; set; }
    public DbSet<Person> Persons { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Bank> Banks { get; set; }
    public DbSet<NGO> NGOs { get; set; }
    public DbSet<Agency> Agencies { get; set; }
    public DbSet<GovernmentAgency> GovernmentAgencies { get; set; }
    public DbSet<InternationalAgency> InternationalAgencies { get; set; }
    public DbSet<SecretService> SecretServices { get; set; }
    public DbSet<NewsMediaOrganization> NewsMediaOrganizations { get; set; }
    public DbSet<BroadcastService> BroadcastServices { get; set; }
    public DbSet<MovieStudio> MovieStudios { get; set; }
    public DbSet<Cooperative> Cooperatives { get; set; }
    public DbSet<Consortium> Consortiums { get; set; }
    public DbSet<Corporation> Corporations { get; set; }
    public DbSet<NonProfit> NonProfits { get; set; }
    public DbSet<Event> Events { get; set; }

    // Relationen
    public DbSet<Relation> Relations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }

    // Adressen
    public DbSet<PostalAddress> PostalAddresses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ──────────────────────────────────────────────────────────
        // BaseEntity Hierarchie (Table-Per-Hierarchy)
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<BaseEntity>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<BaseEntity>("Base")
            .HasValue<Person>("Person")
            .HasValue<Organization>("Organization")
            .HasValue<Company>("Organization:Company")
            .HasValue<Bank>("Organization:Bank")
            .HasValue<NGO>("Organization:NGO")
            .HasValue<Agency>("Organization:Agency")
            .HasValue<GovernmentAgency>("Organization:Agency:GovernmentAgency")
            .HasValue<InternationalAgency>("Organization:Agency:InternationalAgency")
            .HasValue<SecretService>("Organization:SecretService")
            .HasValue<NewsMediaOrganization>("Organization:NewsMediaOrganization")
            .HasValue<BroadcastService>("Organization:BroadcastService")
            .HasValue<MovieStudio>("Organization:MovieStudio")
            .HasValue<Cooperative>("Organization:Cooperative")
            .HasValue<Consortium>("Organization:Consortium")
            .HasValue<Corporation>("Organization:Corporation")
            .HasValue<NonProfit>("Organization:NonProfit")
            .HasValue<Event>("Event");

        modelBuilder.Entity<BaseEntity>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<BaseEntity>()
            .HasIndex(e => e.Name);

        modelBuilder.Entity<BaseEntity>()
            .HasIndex(e => e.EntityType);

        modelBuilder.Entity<BaseEntity>()
            .Property(e => e.Discriminator)
            .HasMaxLength(100);

        // ──────────────────────────────────────────────────────────
        // Person
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<Person>()
            .Property(p => p.AlternateNames)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        // ──────────────────────────────────────────────────────────
        // Organization
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<Organization>()
            .HasOne(o => o.Address)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Organization>()
            .HasOne(o => o.ParentOrganization)
            .WithMany(o => o.SubOrganizations)
            .HasForeignKey(o => o.ParentOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // ──────────────────────────────────────────────────────────
        // Event
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<Event>()
            .Property(e => e.Stakeholders)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

        // ──────────────────────────────────────────────────────────
        // Relationen
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<Relation>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<Relation>()
            .HasOne(r => r.SourceEntity)
            .WithMany(e => e.OutgoingRelations)
            .HasForeignKey(r => r.SourceEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Relation>()
            .HasOne(r => r.TargetEntity)
            .WithMany(e => e.IncomingRelations)
            .HasForeignKey(r => r.TargetEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Relation>()
            .HasIndex(r => new { r.SourceEntityId, r.TargetEntityId, r.RelationType });

        modelBuilder.Entity<Relation>()
            .HasQueryFilter(r => !r.IsDeleted);

        // ──────────────────────────────────────────────────────────
        // OrganizationMember (spezialisierte Relation)
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<OrganizationMember>()
            .HasOne(om => om.Person)
            .WithMany(p => p.Memberships)
            .HasForeignKey(om => om.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrganizationMember>()
            .HasOne(om => om.Organization)
            .WithMany(o => o.Members)
            .HasForeignKey(om => om.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ──────────────────────────────────────────────────────────
        // PostalAddress
        // ──────────────────────────────────────────────────────────

        modelBuilder.Entity<PostalAddress>()
            .HasKey(a => a.Id);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatische Zeitstempel, falls gewünscht
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            if (entry.Entity is BaseEntity entity)
            {
                // Hier könnten Sie CreatedAt/UpdatedAt setzen falls Sie die Properties hinzufügen
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
