using Minerva.Persistence.Relations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Minerva.Persistence.Entities
{
    /// <summary>
    /// Represents a person entity (alive, dead, undead, or fictional) in the Minerva persistence layer.
    /// </summary>
    public class Person : BaseEntity
    {
        public Person() 
        {
            EntityType = "Person";
        }
        public DateOnly? BirthDate { get; set; }
        public DateOnly? DeathDate { get; set; }          // Optional, for deceased persons

        public string? GivenName { get; set; }             // Vorname / Vornamen
        public string? FamilyName { get; set; }            // Nachname

        /// <summary>
        /// Calling name or preferred name, e.g. "Larry Fink" in "Laurence Fink".
        /// </summary>
        public string? CallingName { get; set; }

        /// <summary>
        /// Secondary name or middle name, e.g. "Douglas" in "Laurence Douglas Fink".
        /// </summary>
        public string? AdditionalName { get; set; }        

        // Mehrere alternative Namen: Initialen, Rufname, Spitzname
        public List<string> AlternateNames { get; set; } = [];

        public bool? Gender { get; set; }

        public string? Ethnicity { get; set; }

        /// <summary>
        /// Real persons vs. fictional or symbolic persons.
        /// </summary>
        public bool IsReal { get; set; } = true; // False for fictional or symbolic persons

        public ICollection<OrganizationMember> Memberships { get; set; } = new List<OrganizationMember>();

        public string? NationalityId { get; set; }

                //// --------- Abgeleitete Eigenschaften ---------

        //// Komforteigenschaft (nicht direkt von EF verwaltet)
        //[NotMapped]
        //public IEnumerable<Person> Children =>
        //    OutgoingRelations
        //        .Where(r => r.RelationType == "Family:Child")
        //        .Select(r => r.TargetPerson!);

        //[NotMapped]
        //public IEnumerable<Person> Colleagues =>
        //    OutgoingRelations
        //        .Where(r => r.RelationType == "Work:Colleague")
        //        .Select(r => r.TargetPerson!);

        //[NotMapped]
        //public IEnumerable<Person> Friends =>
        //    OutgoingRelations
        //        .Where(r => r.RelationType == "Social:Friend")
        //        .Select(r => r.TargetPerson!);

        //[NotMapped]
        //public Country? Nationality { get; set; }

        /*
         
additionalName	Zweitname / Mittelname (z. B. Douglas)
alternateName	Gängiger Kurzname, Spitzname, Künstlername (z. B. Larry Fink)
name	Vollständiger Display-Name (z. B. Laurence Douglas Fink)
description	Kurzbiografie / Kontext (z. B. "CEO von BlackRock")
sameAs	URL zu Wikipedia, Wikidata, o. Ä. für semantische Verknüpfung
         */
    }
}
