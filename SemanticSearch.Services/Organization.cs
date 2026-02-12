using Minerva.Persistence.Relations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Minerva.Persistence.Entities
{
    /// <summary>
    /// Represents an organisation entity in the Minerva persistence layer.
    /// </summary>
    public class Organization : BaseEntity
    {
        public Organization()
        {
            EntityType = "Organisation";
        }

        public int? AddressId { get; set; }
        public PostalAddress? Address { get; set; }

        // Juristische Kennungen
        public string? VatID { get; set; }
        public string? TaxID { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? LeiCode { get; set; }
        public string? DunsNumber { get; set; }

        // Klassifikationen
        public string? NaicsCode { get; set; }
        public string? NaceCode { get; set; }
        public string? Industry { get; set; }
        public string? AreaServed { get; set; } // optional
        public string? ServiceType { get; set; } // optional
        public string? IdentifierScheme { get; set; } // z.B. ISIC, NAICS
        public string? IdentifierValue { get; set; }  // Codewert

        /// <summary>
        /// The date that this organization was dissolved.
        /// </summary>
        [JsonPropertyName("@dissolutionDate")]
        public DateOnly? DissolutionDate { get; set; }

        /// <summary>
        /// The date that this organization was founded. 
        /// </summary>
        [JsonPropertyName("@foundingDate")]
        public DateOnly? FoundingDate { get; set; }
        //    /// <summary>
        //    /// The date that this organization was founded. 
        //    /// </summary>
        //    [JsonPropertyName("@foundingDate")]
        //    public DateOnly? FoundingDate { get; set; }

        //    [NotMapped]
        public string Founders { get; set; }

        //    [NotMapped]
        //    public List<Person> Grounders { get; set; }

        //    [NotMapped]
        public string Headquarters { get; set; }
        //    [NotMapped]
        public string Website { get; set; }

        public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();

        public long? ParentOrganizationId;
        public Organization? ParentOrganization { get; set; }
        public ICollection<Organization> SubOrganizations { get; set; } = [];
    }



    public class NGO : Organization
    {
        public NGO()
        {
            EntityType = "Organization:NGO";
        }
        public string? Mission { get; set; }
        public string? FocusArea { get; set; }
        public string? Website { get; set; }
    }

    public class Company : Organization
    {
        public Company()
        {
            EntityType = "Organization:Company";
        }
        public string? CEO { get; set; }
        public int? EmployeeCount { get; set; }
        public decimal? Revenue { get; set; }
        public string? StockSymbol { get; set; }
    }

    public class SecretService : Organization
    {
        public SecretService()
        {
            EntityType = "Organization:SecretService";

        }

        public string? Country { get; set; }
        public string? Jurisdiction { get; set; }
        public string? Headquarters { get; set; }
        public string? Director { get; set; }
    }


    public class Bank : Organization
    {
        public Bank()
        {
            EntityType = "Organization:Bank";
        }

        public BankType? BankType { get; set; } // z.B. "Zentralbank", "Geschäftsbank"
        public string? SWIFTCode { get; set; }
        public string? IBAN { get; set; }
        public string? Branch { get; set; }
        public string? CEO { get; set; }
        public string? HeadquartersLocation { get; set; }
    }
    public enum BankType
    {
        CentralBank,
        CommercialBank,
        InvestmentBank,
        RetailBank,
        CreditUnion,
        SavingsBank,
        OnlineBank,
        CooperativeBank,
        DevelopmentBank,
        PrivateBank,
        PaymentServiceProvider,
        DigitalBank
    }

    public class InternationalAgency : Agency
    {
        public InternationalAgency()
        {
            EntityType = "Organization:Agency:InternationalAgency";
        }
        public string? MemberCountries { get; set; }
        public string? DirectorGeneral { get; set; }
    }

    public class Agency : Organization
    {
        public Agency()
        {
            EntityType = "Organization:Agency";
        }
        public string? Headquarters { get; set; }

        // Optional: weitere spezifische Eigenschaften
        public string? Jurisdiction { get; set; } // z.B. "weltweit"
        public string? AgencyType { get; set; }   // z.B. "Polizeiorganisation"
    }

    public class GovernmentAgency : Agency
    {
        public GovernmentAgency()
        {
            EntityType = "Organization:Agency:Political:GovernmentAgency";
        }
        public string? Country { get; set; }
        public string? Minister { get; set; }
    }

    public class Cooperative : Organization
    {
        public Cooperative()
        {
            EntityType = "Organization:Cooperative";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? CooperativeType { get; set; } // z.B. "Landwirtschaftliche Genossenschaft", "Wohnungsbaugenossenschaft"
        public int? MemberCount { get; set; } // Anzahl der Mitglieder
        public string? Purpose { get; set; } // Zweck der Genossenschaft
    }
    public class Consortium : Organization
    {
        public Consortium()
        {
            EntityType = "Organization:Consortium";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? Purpose { get; set; } // Zweck des Konsortiums
        public DateOnly? FormationDate { get; set; } // Gründungsdatum
        public DateOnly? DissolutionDate { get; set; } // Auflösungsdatum
        public string? Members { get; set; } // Mitglieder des Konsortiums (kann auch als separate Entität modelliert werden)
    }

    public class Corporation : Organization
    {
        public Corporation()
        {
            EntityType = "Organization:Corporation";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? StockSymbol { get; set; } // Börsensymbol, z.B. "AAPL" für Apple Inc.
        public string? Exchange { get; set; } // Börse, z.B. "NASDAQ"
        public int? EmployeeCount { get; set; } // Anzahl der Mitarbeiter
        public decimal? AnnualRevenue { get; set; } // Jährlicher Umsatz
    }

    public class NonProfit : Organization
    {
        public NonProfit()
        {
            EntityType = "Organization:NonProfit";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? Mission { get; set; } // Mission der Organisation
        public string? TaxExemptStatus { get; set; } // Steuerbefreiungsstatus, z.B. "501(c)(3)" in den USA
        public int? VolunteerCount { get; set; } // Anzahl der Freiwilligen
    }


    public class NewsMediaOrganization : Organization
    {
        public NewsMediaOrganization()
        {
            EntityType = "Organization:NewsMediaOrganization";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? MediaType { get; set; } // z.B. "Zeitung", "Fernsehen", "Online"
        public string? Audience { get; set; } // Zielpublikum, z.B. "Allgemein", "Fachpublikum"
    }


    public class BroadcastService : Organization
    {
        public BroadcastService()
        {
            EntityType = "Organization:BroadcastService";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? BroadcastType { get; set; } // z.B. "Radio", "Fernsehen", "Online-Streaming"
        public string? CoverageArea { get; set; } // z.B. "Lokal", "National", "International"
        public string? Language { get; set; } // z.B. "Deutsch", "Englisch"
        public string? Location { get; set; } // Standort des Senders

    }

    public class MovieStudio : Organization
    {
        public MovieStudio()
        {
            EntityType = "Organization:MovieStudio";
        }
        // Optional: weitere spezifische Eigenschaften
        public string? HeadquartersLocation { get; set; } // z.B. "Hollywood, CA"
        public int? FoundedYear { get; set; } // z.B. 1912
        public string? NotableProductions { get; set; } // z.B. "Star Wars, Marvel Cinematic Universe"
        public string? ProductionType { get; set; } // z.B. "Film", "Fernsehen", "Animation"

    }

}
