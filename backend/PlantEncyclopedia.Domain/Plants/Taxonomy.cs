using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// Biological classification. Not every rank is populated for every plant.
/// </summary>
public class Taxonomy : IHasTimestamps
{
    public Guid Id { get; set; }
    public string? Kingdom { get; set; }
    public string? Phylum { get; set; }
    public string? Class { get; set; }
    public string? TaxonOrder { get; set; }
    public string? Family { get; set; }
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Subspecies { get; set; }
    public string? Variety { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<Plant> Plants { get; set; } = [];
}
