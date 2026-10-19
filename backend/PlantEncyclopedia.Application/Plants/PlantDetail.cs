using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.Plants;

/// <summary>
/// Read model for the Plant Detail page. Not a database entity and not the JSON contract.
/// </summary>
/// <param name="Sources">Where the plant's information came from, for attribution (empty for curated plants).</param>
/// <param name="Images">Photos first, then illustrations, oldest first within a kind.</param>
public sealed record PlantDetail(
    Guid Id,
    string Name,
    string? PlantGroupName,
    string? PlantGroupSlug,
    string ScientificName,
    string? Description,
    PlantCareDetail? Care,
    IReadOnlyList<PlantSourceInfo> Sources,
    IReadOnlyList<PlantImageInfo> Images);

/// <summary>One provenance record: the provider (plant_sources.provider) and what it supplied.</summary>
public sealed record PlantSourceInfo(string Provider, PlantSourceType SourceType);

/// <summary>A stored image with its provenance: <paramref name="License"/> is a licence code, <paramref name="Attribution"/> the author.</summary>
public sealed record PlantImageInfo(
    PlantImageKind Kind,
    string Url,
    string? ThumbnailUrl,
    string? MediumUrl,
    string? LargeUrl,
    string? AltText,
    int? Width,
    int? Height,
    string Source,
    string? SourceUrl,
    string? License,
    string? Attribution);

/// <summary>A plant as a search result, with its status so callers can decide whether to show it.</summary>
/// <param name="ImageUrl">The card image: a photo if there is one, otherwise an illustration.</param>
public sealed record PlantSummary(
    Guid Id,
    string Name,
    string? PlantGroupName,
    string ScientificName,
    PlantStatus Status,
    string? ImageUrl);

/// <summary>Verified Quick Care values. Every value is optional.</summary>
public sealed record PlantCareDetail(
    string? LightSummary,
    string? LightDetails,
    string? WaterSummary,
    string? WaterDetails,
    decimal? TemperatureMinC,
    decimal? TemperatureMaxC,
    string? TemperatureDetails,
    string? SoilSummary,
    string? SoilDetails);
