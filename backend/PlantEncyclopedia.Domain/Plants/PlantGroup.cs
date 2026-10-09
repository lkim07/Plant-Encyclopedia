using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

public class PlantGroup : IHasTimestamps
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }
    public List<Plant> Plants { get; set; } = [];
}
