using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using PlantEncyclopedia.Api.Accounts;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Favorites;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Api.Favorites;

public sealed record AddFavoriteRequest(Guid? PlantId, int? Order);

public sealed record ReorderFavoritesRequest(IReadOnlyList<ReorderItem>? Items);

public sealed record ReorderItem(Guid? FavoriteId, int? Order);

/// <summary>JSON contract for one favorite (API_SPEC §31). Changing it changes the public API.</summary>
public sealed record FavoriteResponse(Guid FavoriteId, int Order, FavoritePlantResponse Plant)
{
    public static FavoriteResponse From(FavoriteItem item) =>
        new(item.FavoriteId, item.Order, new FavoritePlantResponse(item.PlantId, item.PlantName, item.PlantGroupName, item.ImageUrl));
}

public sealed record FavoritePlantResponse(Guid Id, string Name, string? PlantGroup, string? ImageUrl);

/// <summary>
/// The signed-in user's Favorites (API_SPEC §31–34, D-040–D-044). Everything requires sign-in and works only on the
/// user's own favorites; changes also require the CSRF token and are rate-limited per user. Saving needs a confirmed
/// email (D-041). Saving a plant twice returns the existing favorite, so a bookmark toggle is safe to repeat.
/// </summary>
public static class FavoriteEndpoints
{
    public const string RateLimitPolicy = "favorites";
    public const int RequestsPerMinute = 60;

    public static IServiceCollection AddFavorites(this IServiceCollection services)
    {
        services.Configure<RateLimiterOptions>(limiter => limiter.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }

    public static IEndpointRouteBuilder MapFavoriteEndpoints(this IEndpointRouteBuilder app)
    {
        var favorites = app.MapGroup("/api/favorites").RequireAuthorization().RequireRateLimiting(RateLimitPolicy);
        favorites.MapGet("", ListAsync);
        favorites.MapPost("", AddAsync).RequireCsrfToken();
        favorites.MapDelete("/{favoriteId:guid}", RemoveAsync).RequireCsrfToken();
        favorites.MapPatch("/order", ReorderAsync).RequireCsrfToken();
        return app;
    }

    // GET /api/favorites
    private static async Task<IResult> ListAsync(ClaimsPrincipal user, IFavoriteStore store, CancellationToken cancellationToken) =>
        Results.Ok(new { data = (await store.ListAsync(UserId(user), cancellationToken)).Select(FavoriteResponse.From).ToList() });

    // POST /api/favorites {plantId, order?}: 201 when saved, 200 when it already was.
    private static async Task<IResult> AddAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IFavoriteStore store,
        CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<AddFavoriteRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;
        if (body!.PlantId is not { } plantId || plantId == Guid.Empty) return ApiErrors.Validation("plantId", "Choose a plant.");
        if (body.Order is < 1) return ApiErrors.Validation("order", "Use a position of 1 or more.");

        var account = await users.GetUserAsync(context.User);
        if (account is null) return Results.Unauthorized();
        if (!account.EmailConfirmed)
        {
            return Results.Json(
                new { error = new { code = "EMAIL_NOT_CONFIRMED", message = "Confirm your email address to save plants to your Favorites." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await store.AddAsync(account.Id, plantId, body.Order, cancellationToken);
        return result.Status switch
        {
            AddFavoriteStatus.Added => Results.Created($"/api/favorites/{result.Favorite!.FavoriteId}", new { data = FavoriteResponse.From(result.Favorite) }),
            AddFavoriteStatus.AlreadySaved when result.Favorite is not null => Results.Ok(new { data = FavoriteResponse.From(result.Favorite) }),
            AddFavoriteStatus.AlreadySaved => Results.Ok(),
            AddFavoriteStatus.LimitReached => Results.Conflict(new
            {
                error = new { code = "FAVORITES_LIMIT", message = $"You can save up to {IFavoriteStore.MaxFavorites} plants. Remove some to save more." }
            }),
            _ => ApiErrors.NotFound("PLANT_NOT_FOUND", "The requested plant could not be found.")
        };
    }

    // DELETE /api/favorites/{favoriteId}: another user's favorite answers like a missing one.
    private static async Task<IResult> RemoveAsync(Guid favoriteId, ClaimsPrincipal user, IFavoriteStore store, CancellationToken cancellationToken) =>
        await store.RemoveAsync(UserId(user), favoriteId, cancellationToken)
            ? Results.NoContent()
            : ApiErrors.NotFound("FAVORITE_NOT_FOUND", "The favorite could not be found.");

    // PATCH /api/favorites/order {items: [{favoriteId, order}]}: all of the user's favorites, orders 1…n.
    private static async Task<IResult> ReorderAsync(HttpContext context, IFavoriteStore store, CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<ReorderFavoritesRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;
        if (body!.Items is not { } items || items.Any(i => i?.FavoriteId is null || i.Order is null)
            || items.Count > IFavoriteStore.MaxFavorites)
        {
            return ApiErrors.Validation("items", "List each favorite with its new order.");
        }

        var status = await store.ReorderAsync(
            UserId(context.User), items.Select(i => (i.FavoriteId!.Value, i.Order!.Value)).ToList(), cancellationToken);
        return status == ReorderFavoritesStatus.Reordered
            ? Results.NoContent()
            : ApiErrors.Validation("items", "List each of your favorites exactly once, with orders 1 to n.");
    }

    // Cookie authentication always carries the user ID; the endpoints require authorization.
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
