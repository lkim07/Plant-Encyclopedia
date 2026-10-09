namespace PlantEncyclopedia.Domain.Common;

/// <summary>
/// An entity whose creation time is recorded when it is first persisted.
/// </summary>
public interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}
