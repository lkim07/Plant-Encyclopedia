using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Application.Explore;

namespace PlantEncyclopedia.Api.Explore;

public static class ExploreEndpoints
{
    public static IEndpointRouteBuilder MapExploreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/explore", GetFeedAsync);
        return app;
    }

    // GET /api/explore?page=&pageSize= — the discovery feed (API_SPEC §20–23). Same paging limits as plant groups.
    private static async Task<IResult> GetFeedAsync(
        string? page,
        string? pageSize,
        IExploreQueries explore,
        CancellationToken cancellationToken)
    {
        var (request, error) = Pagination.Parse(page, pageSize);
        if (error is not null)
        {
            return error;
        }

        var feed = await explore.GetFeedAsync(request.Page, request.PageSize, cancellationToken);
        return Results.Ok(new
        {
            data = feed.Cards.Select(ExploreCardResponse.From).ToList(),
            pagination = new PaginationResponse(request.Page, request.PageSize, feed.HasNext)
        });
    }
}

/// <summary>JSON contract for one Explore card (API_SPEC §20). Changing it changes the public API.</summary>
public sealed record ExploreCardResponse(Guid Id, string Name, string? PlantGroup, string? ImageUrl)
{
    public static ExploreCardResponse From(ExploreCard card) => new(card.Id, card.Name, card.PlantGroupName, card.ImageUrl);
}
