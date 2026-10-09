using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

public class Category : IHasTimestamps
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<PlantGroup> PlantGroups { get; set; } = [];
}
