namespace PlantEncyclopedia.Domain.History;

/// <summary>
/// A search a signed-in user ran (PRD §16, DATABASE_DESIGN §28–29): one row per user and text, ignoring case, moved
/// to the top when searched again. <see cref="UserId"/> refers to the user account kept by Infrastructure.
/// Removing an entry never touches plants, favorites or other users' data.
/// </summary>
public class SearchHistoryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The normalized search text as the user typed it (at most 100 characters).</summary>
    public required string Query { get; set; }

    /// <summary>The text in lower case, so "Rose" and "rose" are one entry.</summary>
    public required string QueryKey { get; set; }

    public DateTimeOffset SearchedAt { get; set; }
}
