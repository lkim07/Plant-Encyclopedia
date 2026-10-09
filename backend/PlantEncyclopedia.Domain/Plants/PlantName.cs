using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// An alternative searchable name for a plant, stored exactly as written.
/// Common names require a language tag (for example "en", "ko", "zh-Hant");
/// synonyms may omit it.
/// </summary>
public class PlantName : IHasCreatedAt
{
    public Guid Id { get; set; }
    public Guid PlantId { get; set; }
    public required string Name { get; set; }
    public required PlantNameType NameType { get; set; }
    public string? LanguageCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Plant? Plant { get; set; }
}
