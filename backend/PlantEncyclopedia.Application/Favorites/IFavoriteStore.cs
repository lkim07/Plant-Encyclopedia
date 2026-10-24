namespace PlantEncyclopedia.Application.Favorites;

/// <summary>A favorite as the user sees it: their order and the plant's card data.</summary>
/// <param name="ImageUrl">The card image: a photo if there is one, otherwise an illustration.</param>
public sealed record FavoriteItem(Guid FavoriteId, int Order, Guid PlantId, string PlantName, string? PlantGroupName, string? ImageUrl);

public enum AddFavoriteStatus
{
    Added,

    /// <summary>The plant was already saved; the existing favorite is returned (saving twice is harmless).</summary>
    AlreadySaved,

    /// <summary>The plant does not exist or is not Published.</summary>
    PlantNotFound,

    /// <summary>The user already has <see cref="IFavoriteStore.MaxFavorites"/> favorites.</summary>
    LimitReached
}

public sealed record AddFavoriteResult(AddFavoriteStatus Status, FavoriteItem? Favorite = null);

public enum ReorderFavoritesStatus
{
    Reordered,

    /// <summary>The list is not exactly the user's favorites, each once, with orders 1…n.</summary>
    Invalid
}

/// <summary>
/// One user's favorites (implemented in Infrastructure). Every operation is scoped to <c>userId</c>, so one user can
/// never read or change another user's favorites; a favorite ID of another user behaves as if it did not exist.
/// Changes for one user are serialized, so positions stay 1…n without gaps or duplicates.
/// </summary>
public interface IFavoriteStore
{
    public const int MaxFavorites = 500;

    /// <summary>The user's favorites of Published plants, in their order.</summary>
    Task<IReadOnlyList<FavoriteItem>> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a Published plant at the end, or at <paramref name="order"/> (1-based, clamped) to put it back where it was.
    /// </summary>
    Task<AddFavoriteResult> AddAsync(Guid userId, Guid plantId, int? order, CancellationToken cancellationToken);

    /// <summary>Removes the user's favorite and closes the gap. False when the user has no such favorite.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken);

    /// <summary>Sets the order of all the user's favorites at once.</summary>
    Task<ReorderFavoritesStatus> ReorderAsync(Guid userId, IReadOnlyList<(Guid FavoriteId, int Order)> items, CancellationToken cancellationToken);
}
