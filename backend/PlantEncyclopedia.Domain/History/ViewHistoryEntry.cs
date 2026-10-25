using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Domain.History;

/// <summary>
/// A plant a signed-in user opened (PRD §17, DATABASE_DESIGN §30). Repeat views are kept as separate rows, since
/// they say more about interest, except within a short window. Used only as a recommendation signal; not shown.
/// <see cref="UserId"/> refers to the user account kept by Infrastructure.
/// </summary>
public class ViewHistoryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlantId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }

    public Plant? Plant { get; set; }
}
