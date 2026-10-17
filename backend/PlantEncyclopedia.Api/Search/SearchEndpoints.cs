using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Application.Search;

namespace PlantEncyclopedia.Api.Search;

public static class SearchEndpoints
{
    /// <summary>Search results stop at this page, bounding the cost of deep paging.</summary>
    public const int MaxPage = 50;

    /// <summary>Most suggestions returned per request.</summary>
    public const int MaxSuggestions = 8;

    /// <summary>Largest accepted body for an external lookup; {"q": ...} with 100 characters needs far less.</summary>
    public const int MaxExternalLookupBodyBytes = 1024;

    public const string ExternalLookupUnavailableCode = "EXTERNAL_LOOKUP_UNAVAILABLE";
    public const string ExternalLookupUnavailableMessage = "External plant lookup is unavailable right now. Please try again later.";

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", SearchAsync)
            .RequireRateLimiting(SearchProtection.RateLimitPolicy)
            .WithRequestTimeout(SearchProtection.TimeoutPolicy);

        app.MapGet("/api/search/suggestions", SuggestAsync)
            .RequireRateLimiting(SearchProtection.SuggestionsRateLimitPolicy)
            .WithRequestTimeout(SearchProtection.SuggestionsTimeoutPolicy);

        app.MapPost("/api/search/external", LookUpExternallyAsync)
            .RequireRateLimiting(SearchProtection.ExternalLookupRateLimitPolicy)
            .WithRequestTimeout(SearchProtection.ExternalLookupTimeoutPolicy);

        return app;
    }

    // POST /api/search/external {"q": "..."} (API_SPEC §27): the user asked to look a plant up in Catalogue
    // of Life after the local search found nothing. The body is read explicitly so that malformed or
    // oversized bodies get the documented error format. The text is never logged.
    private static async Task<IResult> LookUpExternallyAsync(
        HttpRequest request,
        ExternalPlantLookupService lookup,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxExternalLookupBodyBytes)
        {
            return ApiErrors.PayloadTooLarge();
        }

        // Also caps bodies sent without a Content-Length (chunked).
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = MaxExternalLookupBodyBytes;
        }

        if (!request.HasJsonContentType())
        {
            return ApiErrors.Validation("body", "Request body must be JSON.");
        }

        ExternalLookupRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<ExternalLookupRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return ApiErrors.Validation("body", "Request body must be valid JSON.");
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return ApiErrors.PayloadTooLarge();
        }

        var (text, error) = SearchText.Validate(body?.Q);
        if (error is not null)
        {
            return ApiErrors.Validation("q", error);
        }

        var result = await lookup.LookUpAsync(text!, cancellationToken);
        return result.Status switch
        {
            ExternalLookupStatus.Found => ExternalLookupOk("found", SearchResultResponse.From(result.Plant!)),
            ExternalLookupStatus.PendingReview => ExternalLookupOk("pendingReview"),
            ExternalLookupStatus.NotFound => ExternalLookupOk("notFound"),
            _ => ApiErrors.ServiceUnavailable(ExternalLookupUnavailableCode, ExternalLookupUnavailableMessage)
        };
    }

    private static IResult ExternalLookupOk(string status, SearchResultResponse? plant = null) =>
        Results.Ok(new { data = new ExternalLookupResponse(status, plant) });

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
