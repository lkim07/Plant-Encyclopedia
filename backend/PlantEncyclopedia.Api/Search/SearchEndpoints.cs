using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Search;

namespace PlantEncyclopedia.Api.Search;

public static class SearchEndpoints
{
    /// <summary>Search results stop at this page, bounding the cost of deep paging.</summary>
    public const int MaxPage = 50;

    /// <summary>Most suggestions returned per request.</summary>
    public const int MaxSuggestions = 8;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", SearchAsync)
            .RequireRateLimiting(SearchProtection.RateLimitPolicy)
            .WithRequestTimeout(SearchProtection.TimeoutPolicy);

        app.MapGet("/api/search/suggestions", SuggestAsync)
            .RequireRateLimiting(SearchProtection.SuggestionsRateLimitPolicy)
            .WithRequestTimeout(SearchProtection.SuggestionsTimeoutPolicy);

        return app;
    }

    // GET /api/search/suggestions?q= (API_SPEC §26). Same text validation as search; the text is never logged.
    private static async Task<IResult> SuggestAsync(
        string? q,
        ISearchQueries search,
        CancellationToken cancellationToken)
    {
        var (text, error) = SearchText.Validate(q);
        if (error is not null)
        {
            return ApiErrors.Validation("q", error);
        }

        var suggestions = await search.SuggestAsync(text!, MaxSuggestions, cancellationToken);
        return Results.Ok(new { data = suggestions.Select(SearchSuggestionResponse.From) });
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
