using Minerva.Persistence.Entities;
using Minerva.Persistence.Relations;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SemanticSearch.Services;

/// <summary>
/// Service zum Persistieren von extrahierten Entities und Relationen in die SQLite-Datenbank.
/// Mapped ExtractedEntity → BaseEntity (Person, Organization, Event)
/// </summary>
public class EntityPersistenceService
{
    private readonly DbContext _dbContext;
    private readonly Dictionary<string, long> _entityCache = new();

    public EntityPersistenceService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Speichert extrahierte Entities und gibt eine Mapping-Tabelle zurück.
    /// </summary>
    public async Task<Dictionary<ExtractedEntity, BaseEntity>> PersistEntitiesAsync(
        List<ExtractedEntity> extractedEntities,
        string sourceDocumentId)
    {
        var mapping = new Dictionary<ExtractedEntity, BaseEntity>();

        foreach (var extracted in extractedEntities)
        {
            // Prüfe ob Entity bereits existiert (fuzzy matching)
            var existing = await FindExistingEntityAsync(extracted);

            if (existing != null)
            {
                mapping[extracted] = existing;
                continue;
            }

            // Erstelle neue Entity basierend auf Type
            var entity = CreateEntityFromExtracted(extracted, sourceDocumentId);

            _dbContext.Add(entity);
            await _dbContext.SaveChangesAsync();

            mapping[extracted] = entity;
            _entityCache[extracted.Text] = entity.Id;
        }

        return mapping;
    }

    /// <summary>
    /// Speichert extrahierte Relationen zwischen bereits persistierten Entities.
    /// </summary>
    public async Task PersistRelationsAsync(
        List<ExtractedRelation> extractedRelations,
        Dictionary<ExtractedEntity, BaseEntity> entityMapping)
    {
        foreach (var extracted in extractedRelations)
        {
            if (!entityMapping.TryGetValue(extracted.SourceEntity, out var source) ||
                !entityMapping.TryGetValue(extracted.TargetEntity, out var target))
            {
                continue; // Skip if entities not found
            }

            // Prüfe ob Relation bereits existiert
            var existing = await _dbContext.Set<Relation>()
                .FirstOrDefaultAsync(r =>
                    r.SourceEntityId == source.Id &&
                    r.TargetEntityId == target.Id &&
                    r.RelationType == extracted.RelationType);

            if (existing != null)
                continue;

            // Erstelle Relation (ggf. spezialisierte Klasse)
            var relation = CreateRelationFromExtracted(extracted, source, target);

            _dbContext.Add(relation);
        }

        await _dbContext.SaveChangesAsync();
    }

    private BaseEntity CreateEntityFromExtracted(ExtractedEntity extracted, string sourceDocumentId)
    {
        BaseEntity entity = extracted.Type switch
        {
            "Person" => new Person
            {
                Name = extracted.Text,
                GivenName = ExtractGivenName(extracted.Text),
                FamilyName = ExtractFamilyName(extracted.Text),
                IsReal = true
            },

            "Organization" => new Organization
            {
                Name = extracted.Text
            },

            "Organization:Company" => new Company
            {
                Name = extracted.Text
            },

            "Organization:Bank" => new Bank
            {
                Name = extracted.Text
            },

            "Organization:NGO" => new NGO
            {
                Name = extracted.Text
            },

            "Organization:Agency" => new Agency
            {
                Name = extracted.Text
            },

            "Organization:SecretService" => new SecretService
            {
                Name = extracted.Text
            },

            "Organization:NewsMediaOrganization" => new NewsMediaOrganization
            {
                Name = extracted.Text
            },

            "Event" => new Event
            {
                Name = extracted.Text
            },

            _ => new BaseEntity
            {
                Name = extracted.Text,
                EntityType = extracted.Type
            }
        };

        // Gemeinsame Felder
        entity.Description = extracted.AdditionalInfo;
        entity.Discriminator = extracted.Type;

        // Metadaten
        var metadata = new
        {
            ExtractedFrom = sourceDocumentId,
            Confidence = extracted.Confidence,
            ExtractedAt = DateTime.UtcNow
        };
        entity.MetaDataJson = JsonSerializer.Serialize(metadata);

        return entity;
    }

    private Relation CreateRelationFromExtracted(
        ExtractedRelation extracted,
        BaseEntity source,
        BaseEntity target)
    {
        // Spezialisierte Relation für OrganizationMember
        if (extracted.RelationType.StartsWith("Work:") &&
            source is Person person &&
            target is Organization org)
        {
            return new OrganizationMember
            {
                PersonId = person.Id,
                Person = person,
                OrganizationId = org.Id,
                Organization = org,
                Role = extracted.Context ?? extracted.RelationType,
                FromDate = extracted.FromDate,
                ToDate = extracted.ToDate,
                SourceEntityId = source.Id,
                TargetEntityId = target.Id,
                RelationType = extracted.RelationType,
                Context = extracted.Context
            };
        }

        // Allgemeine Relation
        return new Relation
        {
            SourceEntityId = source.Id,
            SourceEntity = source,
            TargetEntityId = target.Id,
            TargetEntity = target,
            RelationType = extracted.RelationType,
            FromDate = extracted.FromDate,
            ToDate = extracted.ToDate,
            Context = extracted.Context,
            IsDirected = true
        };
    }

    private async Task<BaseEntity?> FindExistingEntityAsync(ExtractedEntity extracted)
    {
        // Fuzzy matching by name
        var normalized = NormalizeName(extracted.Text);

        var candidates = await _dbContext.Set<BaseEntity>()
            .Where(e => e.EntityType == extracted.Type)
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            var candidateNormalized = NormalizeName(candidate.Name);
            var similarity = FuzzySharp.Fuzz.Ratio(normalized, candidateNormalized);

            if (similarity > 85) // 85% ähnlich
            {
                return candidate;
            }
        }

        return null;
    }

    private string NormalizeName(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Trim();
    }

    private string? ExtractGivenName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private string? ExtractFamilyName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : null;
    }
}
