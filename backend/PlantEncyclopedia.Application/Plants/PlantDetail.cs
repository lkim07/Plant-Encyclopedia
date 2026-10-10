namespace PlantEncyclopedia.Application.Plants;

/// <summary>
/// Read model for the Plant Detail page. Not a database entity and not the JSON contract.
/// </summary>
public sealed record PlantDetail(
    Guid Id,
    string Name,
    string? PlantGroupName,
    string ScientificName,
    string? Description,
    PlantCareDetail? Care);

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
