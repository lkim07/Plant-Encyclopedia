using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PlantEncyclopedia.Api.Accounts;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Api.Search;
using PlantEncyclopedia.Application.History;

namespace PlantEncyclopedia.Api.History;

public sealed record AddSearchRequest(string? Query);

public sealed record AddViewRequest(Guid? PlantId);

/// <summary>JSON contract for one recent search (API_SPEC §28). Changing it changes the public API.</summary>
public sealed record SearchHistoryResponse(Guid SearchId, string Query, DateTimeOffset SearchedAt)
{
    public static SearchHistoryResponse From(SearchHistoryItem item) => new(item.SearchId, item.Query, item.SearchedAt);
}

/// <summary>
/// The signed-in user's search and view history (API_SPEC §28–29, PRD §16–17), kept as recommendation signals.
/// Visitors who are not signed in have no server-side history: their recent searches stay in their browser.
/// Everything requires sign-in and works only on the user's own history; changes also require the CSRF token and
/// are rate-limited per user. Search text is never logged.
/// </summary>
public static class HistoryEndpoints
{
    public const string RateLimitPolicy = "history";
    public const int RequestsPerMinute = 120;

    public static IServiceCollection AddHistory(this IServiceCollection services)
    {
        services.Configure<RateLimiterOptions>(limiter => limiter.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }

    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var history = app.MapGroup("/api/history").RequireAuthorization().RequireRateLimiting(RateLimitPolicy);
        history.MapGet("/searches", ListSearchesAsync);
        history.MapPost("/searches", AddSearchAsync).RequireCsrfToken();
        history.MapDelete("/searches/{searchId:guid}", RemoveSearchAsync).RequireCsrfToken();
        history.MapPost("/views", AddViewAsync).RequireCsrfToken();
        return app;
    }

    // GET /api/history/searches: the most recent searches, newest first.
    private static async Task<IResult> ListSearchesAsync(ClaimsPrincipal user, IHistoryStore store, CancellationToken cancellationToken) =>
        Results.Ok(new { data = (await store.ListSearchesAsync(UserId(user), cancellationToken)).Select(SearchHistoryResponse.From).ToList() });

    // POST /api/history/searches {query}: records a search the user ran; the same text moves to the top.
    private static async Task<IResult> AddSearchAsync(HttpContext context, IHistoryStore store, CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<AddSearchRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;
        var (query, error) = SearchText.Validate(body!.Query);
        if (error is not null) return ApiErrors.Validation("query", error);

        var item = await store.AddSearchAsync(UserId(context.User), query!, cancellationToken);
        return Results.Ok(new { data = SearchHistoryResponse.From(item) });
    }

    // DELETE /api/history/searches/{searchId}: another user's entry answers like a missing one.
    private static async Task<IResult> RemoveSearchAsync(Guid searchId, ClaimsPrincipal user, IHistoryStore store, CancellationToken cancellationToken) =>
        await store.RemoveSearchAsync(UserId(user), searchId, cancellationToken)
            ? Results.NoContent()
            : ApiErrors.NotFound("SEARCH_NOT_FOUND", "The search could not be found.");

    // POST /api/history/views {plantId}: the user opened a plant's detail page.
    private static async Task<IResult> AddViewAsync(HttpContext context, IHistoryStore store, CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<AddViewRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;
        if (body!.PlantId is not { } plantId || plantId == Guid.Empty) return ApiErrors.Validation("plantId", "Choose a plant.");

        return await store.AddViewAsync(UserId(context.User), plantId, cancellationToken)
            ? Results.NoContent()
            : ApiErrors.NotFound("PLANT_NOT_FOUND", "The requested plant could not be found.");
    }

    // Cookie authentication always carries the user ID; the endpoints require authorization.
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
