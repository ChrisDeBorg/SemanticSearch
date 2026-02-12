using System;
using System.Collections.Generic;
using System.Text;


namespace Minerva.Persistence;

/// <summary>
/// Einfache PostalAddress-Klasse (falls nicht schon vorhanden).
/// </summary>
public class PostalAddress
{
    public int Id { get; set; }
    public string? StreetAddress { get; set; }
    public string? AddressLocality { get; set; }  // Stadt
    public string? AddressRegion { get; set; }     // Bundesland/Region
    public string? PostalCode { get; set; }
    public string? AddressCountry { get; set; }
}

