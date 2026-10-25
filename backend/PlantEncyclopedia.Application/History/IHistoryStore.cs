namespace PlantEncyclopedia.Application.History;

/// <summary>A recent search as the user sees it.</summary>
public sealed record SearchHistoryItem(Guid SearchId, string Query, DateTimeOffset SearchedAt);

/// <summary>
/// One signed-in user's search and view history (implemented in Infrastructure), kept as recommendation signals
/// (PRD §16–17). Every operation is scoped to <c>userId</c>; another user's entry behaves as if it did not exist.
/// Only the newest entries are kept, so history cannot grow without bound.
/// </summary>
public interface IHistoryStore
{
    /// <summary>Searches kept per user; older ones are deleted.</summary>
    public const int MaxSearches = 50;

    /// <summary>Recent searches returned to the user (the Search page shows up to 10).</summary>
    public const int RecentSearchLimit = 10;

    /// <summary>Views kept per user; older ones are deleted.</summary>
    public const int MaxViews = 200;

    /// <summary>Opening the same plant again within this many minutes is not a new view (reloads, Back and forth).</summary>
    public const int ViewRepeatMinutes = 30;

    /// <summary>The user's most recent searches, newest first.</summary>
    Task<IReadOnlyList<SearchHistoryItem>> ListSearchesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records a search; the same text (ignoring case) moves to the top instead of repeating.</summary>
    Task<SearchHistoryItem> AddSearchAsync(Guid userId, string query, CancellationToken cancellationToken);

    /// <summary>Removes one of the user's searches. False when the user has no such entry.</summary>
    Task<bool> RemoveSearchAsync(Guid userId, Guid searchId, CancellationToken cancellationToken);

    /// <summary>Records that the user opened a Published plant. False when the plant does not exist or is not Published.</summary>
    Task<bool> AddViewAsync(Guid userId, Guid plantId, CancellationToken cancellationToken);
}
