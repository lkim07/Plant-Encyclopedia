using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Search;

namespace PlantEncyclopedia.Api.Search;

public static class SearchEndpoints
{
    /// <summary>Search results stop at this page, bounding the cost of deep paging.</summary>
    public const int MaxPage = 50;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", SearchAsync)
            .RequireRateLimiting(SearchProtection.RateLimitPolicy)
            .WithRequestTimeout(SearchProtection.TimeoutPolicy);
        return app;
    }

    // GET /api/search?q=&page=&pageSize= (API_SPEC §24–25). The search text is never logged.
    private static async Task<IResult> SearchAsync(
        string? q,
        string? page,
        string? pageSize,
        ISearchQueries search,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>();

        var (text, textError) = SearchText.Validate(q);
        if (textError is not null)
        {
            details["q"] = textError;
        }

        var request = Pagination.Validate(page, pageSize, MaxPage, details);

        if (details.Count > 0)
        {
            return ApiErrors.Validation(details);
        }

        var results = await search.SearchAsync(text!, request.Page, request.PageSize, cancellationToken);

        return Results.Ok(new
        {
            data = results.Results.Select(SearchResultResponse.From),
            pagination = new PaginationResponse(request.Page, request.PageSize, results.HasNext)
        });
    }
}
