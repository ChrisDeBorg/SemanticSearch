using Minerva.Persistence.Relations;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Minerva.Persistence.Entities
{
    public class BaseEntity
    {
        [JsonPropertyName("@Id")]
        [Key]
        public long Id { get; set; }

        [JsonPropertyName("@discriminator")]
        [Required]
        [MaxLength(100)]
        public string Discriminator { get; set; } = "Base";


        /// <summary>
        /// Type of the entity, e.g. "Event", "Influence", "Person", etc.
        /// </summary>
        [JsonPropertyName("@type")]
        [Required] 
        public string EntityType { get; set; } = "BaseEntity";

        /// <summary>
        /// Name or Title of the entity, e.g. "United Nations", "World Bank", "John Doe".
        /// </summary>
        [JsonPropertyName("@name")]
        [Required] 
        public string Name { get; set; } = string.Empty;
        public string? ShortDescrition { get; set; }
        public string? Description { get; set; }           // Allgemeine Beschreibung
        public string? DisambiguatingDescription { get; set; } // Beschreibung zur Unterscheidung

        /// <summary>
        /// Gets or sets the unique identifier for the entity. e.g. ISSN, ISBN, JFK, WTO, WHO, IMF, etc.
        /// </summary>
        public string? Identifier { get; set; }

        /// <summary>
        /// Gets or sets the raw JSON string containing metadata information.Raw JSON string, stored in DB
        /// </summary>
        public string? MetaDataJson { get; set; }

        /// <summary>
        /// Gets or sets the optional metadata in a compressed or binary serialized format.Optional: compressed or binary serialized
        /// </summary>
        /// <remarks>This property can be used to store metadata in a compact binary format for efficient
        /// storage or transmission. Ensure that the data is properly serialized and deserialized when setting or
        /// retrieving this value.</remarks>
        public byte[]? MetaDataBinary { get; set; }

        /// <summary>
        /// Gets or sets the URL of an image representing the entity, such as a logo or icon.Optional: URL to an image representing the entity (Like a logo or icon)
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// URL of a reference Web page that unambiguously indicates the item's identity. E.g. the URL of the item's Wikipedia page, Wikidata entry, or official website. 
        /// </summary>
        public string? SameAs { get; set; }


        public ICollection<Relation> OutgoingRelations { get; set; } = [];
        public ICollection<Relation> IncomingRelations { get; set; } = [];
    }
}
