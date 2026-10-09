namespace PlantEncyclopedia.Domain.Common;

/// <summary>
/// An entity whose creation and last-modification times are recorded.
/// </summary>
public interface IHasTimestamps : IHasCreatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
