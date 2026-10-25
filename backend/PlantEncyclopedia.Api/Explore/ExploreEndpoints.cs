using System.Globalization;
using System.Security.Claims;
using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Explore;

namespace PlantEncyclopedia.Api.Explore;

public static class ExploreEndpoints
{
    public static IEndpointRouteBuilder MapExploreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/explore", GetFeedAsync);
        return app;
    }

    // GET /api/explore?page=&pageSize=&feedAt= — the discovery feed (API_SPEC §20–23). Same paging limits as plant
    // groups. Signed in, the feed is personalized from the user's activity up to feedAt: the first page returns it,
    // and later pages send it back so their order matches the first page's.
    private static async Task<IResult> GetFeedAsync(
        string? page,
        string? pageSize,
        string? feedAt,
        ClaimsPrincipal user,
        IExploreQueries explore,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var (request, error) = Pagination.Parse(page, pageSize);
        if (error is not null)
        {
            return error;
        }

        var now = time.GetUtcNow();
        var asOf = now;
        if (feedAt is not null)
        {
            if (!DateTimeOffset.TryParse(feedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return ApiErrors.Validation("feedAt", "feedAt must be a date and time returned by this endpoint.");
            }

            asOf = parsed.ToUniversalTime() < now ? parsed.ToUniversalTime() : now;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var feed = userId is null
            ? await explore.GetFeedAsync(request.Page, request.PageSize, cancellationToken)
            : await explore.GetFeedForUserAsync(Guid.Parse(userId), asOf, request.Page, request.PageSize, cancellationToken);
        return Results.Ok(new
        {
            data = feed.Cards.Select(ExploreCardResponse.From).ToList(),
            pagination = new PaginationResponse(request.Page, request.PageSize, feed.HasNext),
            feedAt = asOf.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        });
    }
}

/// <summary>JSON contract for one Explore card (API_SPEC §20). Changing it changes the public API.</summary>
public sealed record ExploreCardResponse(Guid Id, string Name, string? PlantGroup, string? ImageUrl)
{
    public static ExploreCardResponse From(ExploreCard card) => new(card.Id, card.Name, card.PlantGroupName, card.ImageUrl);
}
