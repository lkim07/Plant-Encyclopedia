using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.Plants;

/// <summary>
/// Read model for the Plant Detail page. Not a database entity and not the JSON contract.
/// </summary>
/// <param name="Sources">Where the plant's information came from, for attribution (empty for curated plants).</param>
public sealed record PlantDetail(
    Guid Id,
    string Name,
    string? PlantGroupName,
    string? PlantGroupSlug,
    string ScientificName,
    string? Description,
    PlantCareDetail? Care,
    IReadOnlyList<PlantSourceInfo> Sources);

/// <summary>One provenance record: the provider (plant_sources.provider) and what it supplied.</summary>
public sealed record PlantSourceInfo(string Provider, PlantSourceType SourceType);

/// <summary>A plant as a search result, with its status so callers can decide whether to show it.</summary>
public sealed record PlantSummary(
    Guid Id,
    string Name,
    string? PlantGroupName,
    string ScientificName,
    PlantStatus Status);

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
