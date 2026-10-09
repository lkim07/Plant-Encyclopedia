using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// Image metadata and provenance. Image binaries live in object storage, not the database.
/// </summary>
public class PlantImage : IHasCreatedAt
{
    public Guid Id { get; set; }
    public Guid PlantId { get; set; }
    public required string Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? MediumUrl { get; set; }
    public string? LargeUrl { get; set; }
    public required string Source { get; set; }
    public string? SourceId { get; set; }
    public string? SourceUrl { get; set; }
    public string? AltText { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? License { get; set; }
    public string? Attribution { get; set; }
    public DateTimeOffset? RetrievedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Plant? Plant { get; set; }
}
