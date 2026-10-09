namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// Provenance for one kind of plant information. A source is identified by the
/// provider's external ID, or by its URL when the provider has no ID (curated sources).
/// An external taxon ID may be shared by several cultivars, so it is not a plant identity.
/// </summary>
public class PlantSource
{
    public Guid Id { get; set; }
    public Guid PlantId { get; set; }
    public required string Provider { get; set; }
    public string? ExternalId { get; set; }
    public required PlantSourceType SourceType { get; set; }
    public string? SourceUrl { get; set; }
    public required DateTimeOffset RetrievedAt { get; set; }
    public DateTimeOffset? LastVerifiedAt { get; set; }

    public Plant? Plant { get; set; }
}
