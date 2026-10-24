using PlantEncyclopedia.Domain.Common;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Domain.Favorites;

/// <summary>
/// A plant a user saved (D-040–D-044, DATABASE_DESIGN §26–27): one row per user and plant, in the user's own order.
/// <see cref="UserId"/> refers to the user account kept by Infrastructure; the Domain does not know its type.
/// </summary>
public class Favorite : IHasCreatedAt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlantId { get; set; }

    /// <summary>The logical order, 1…n without gaps within one user's favorites (never pixel coordinates).</summary>
    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Plant? Plant { get; set; }
}
