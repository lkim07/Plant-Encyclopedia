using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// The application's user-facing plant identity. A cultivar is a distinct Plant.
/// A plant must belong to a PlantGroup before it can be Published.
/// </summary>
public class Plant : IHasTimestamps
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string ScientificName { get; set; }
    public string? CultivarName { get; set; }
    public string? Description { get; set; }
    public Guid? PlantGroupId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public PlantStatus Status { get; set; } = PlantStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PlantGroup? PlantGroup { get; set; }
    public Taxonomy? Taxonomy { get; set; }
    public PlantCare? Care { get; set; }
    public List<PlantName> Names { get; set; } = [];
    public List<PlantImage> Images { get; set; } = [];
    public List<PlantSource> Sources { get; set; } = [];
}
