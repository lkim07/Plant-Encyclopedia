namespace PlantEncyclopedia.Domain.Retail;

/// <summary>The kinds of shop Where to Buy lists (D-032; online retailers are not included yet).</summary>
public enum RetailerKind
{
    Nursery,
    GardenCentre,
    FlowerShop
}

/// <summary>
/// A shop that may sell plants (DATABASE_DESIGN §38–39), copied from an outside source with its source and date.
/// One row per shop location. It says nothing about which plants the shop has (D-033): availability is unknown.
/// </summary>
public class Retailer
{
    public Guid Id { get; set; }

    /// <summary>Where the record came from, e.g. "openstreetmap".</summary>
    public required string Source { get; set; }

    /// <summary>The record's ID at the source, e.g. "node/123" for OpenStreetMap; unique per source.</summary>
    public required string SourceId { get; set; }

    public required string Name { get; set; }
    public RetailerKind Kind { get; set; }

    /// <summary>House number and street, when the source has them.</summary>
    public string? Street { get; set; }

    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Postcode { get; set; }
    public string? Website { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>When this copy was taken from the source.</summary>
    public DateTimeOffset RetrievedAt { get; set; }
}
