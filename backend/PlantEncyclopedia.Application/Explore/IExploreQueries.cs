namespace PlantEncyclopedia.Application.Explore;

/// <summary>Read-only queries for the Explore discovery feed (API_SPEC §20–23).</summary>
public interface IExploreQueries
{
    /// <summary>
    /// One page of the feed for a visitor without behavioral data: Published plants, each once, with
    /// categories and plant groups taking turns. The order is stable, so pages never repeat or skip a plant.
    /// </summary>
    Task<ExplorePage> GetFeedAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record ExplorePage(IReadOnlyList<ExploreCard> Cards, bool HasNext);

/// <param name="ImageUrl">The card image: a photo if there is one, otherwise an illustration.</param>
public sealed record ExploreCard(Guid Id, string Name, string? PlantGroupName, string? ImageUrl);
