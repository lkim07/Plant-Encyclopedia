using PlantEncyclopedia.Application.Plants;

namespace PlantEncyclopedia.Api.Plants;

/// <summary>JSON contract for GET /api/plants/{plantId}. Changing it changes the public API.</summary>
public sealed record PlantDetailResponse(
    Guid Id,
    string Name,
    string? PlantGroup,
    string ScientificName,
    string? Description,
    HeroImageResponse? HeroImage,
    QuickCareResponse? QuickCare)
{
    public static PlantDetailResponse From(PlantDetail plant) => new(
        plant.Id,
        plant.Name,
        plant.PlantGroupName,
        plant.ScientificName,
        plant.Description,
        HeroImage: null, // Hero image selection is not decided yet.
        QuickCareResponse.From(plant.Care));
}

public sealed record HeroImageResponse(string Url, string? AltText);

/// <summary>Quick Care cards. A card is null when it has no value.</summary>
public sealed record QuickCareResponse(
    CareCardResponse? Light,
    CareCardResponse? Water,
    TemperatureResponse? Temperature,
    CareCardResponse? Soil)
{
    public static QuickCareResponse? From(PlantCareDetail? care) => care is null
        ? null
        : new QuickCareResponse(
            CareCardResponse.From(care.LightSummary, care.LightDetails),
            CareCardResponse.From(care.WaterSummary, care.WaterDetails),
            care.TemperatureMinC is null && care.TemperatureMaxC is null
                ? null
                : new TemperatureResponse(care.TemperatureMinC, care.TemperatureMaxC, care.TemperatureDetails),
            CareCardResponse.From(care.SoilSummary, care.SoilDetails));
}

public sealed record CareCardResponse(string Value, string? Description)
{
    public static CareCardResponse? From(string? value, string? description) =>
        value is null ? null : new CareCardResponse(value, description);
}

/// <summary>Temperature in °C as numbers; the client formats the display text.</summary>
public sealed record TemperatureResponse(decimal? MinC, decimal? MaxC, string? Description);
