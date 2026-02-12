using Minerva.Persistence.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Minerva.Persistence.Relations
{
    public class Relation
    {
        [JsonPropertyName("@Id")]
        public long Id { get; set; }

        //[JsonPropertyName("@discriminator")]
        //[Required]
        //[MaxLength(100)]
        //public string Discriminator { get; set; } = "Base";

        public long SourceEntityId { get; set; }
        public BaseEntity SourceEntity { get; set; }
        public long TargetEntityId { get; set; }
        public BaseEntity TargetEntity { get; set; }

        public string RelationType { get; set; } = string.Empty;
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }

        public string? Context { get; set; }

        public bool IsDirected { get; set; } = true;

        // Not mapped: deserialized object for in-app use

        public bool IsDeleted { get; set; } = false;

        //[NotMapped]
        //public RelationMeta? Meta
        //{
        //    get => string.IsNullOrEmpty(MetaDataJson)
        //        ? null
        //        : JsonSerializer.Deserialize<RelationMeta>(MetaDataJson!);

        //    set => MetaDataJson = value == null ? null : JsonSerializer.Serialize(value);
        //}
    }

    public class OrganizationMember : Relation
    {
        public long OrganizationId { get; set; }
        public Organization Organization { get; set; }

        public long PersonId { get; set; }
        public Person Person { get; set; }

        //public DateOnly? FromDate { get; set; }
        //public DateOnly? ToDate { get; set; }
        public string Role { get; set; }
    }

    public class RelationGeneric<TSource, TTarget> : Relation
        where TSource : BaseEntity
        where TTarget : BaseEntity
    {
        public RelationGeneric()
        {
            RelationType = $"{typeof(TSource).Name}To{typeof(TTarget).Name}Relation";
        }

        public new TSource? SourceEntity { get; set; }
        public new TTarget? TargetEntity { get; set; }
    }
}
