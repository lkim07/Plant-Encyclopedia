using System.Text.Json;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Application.Plants;

namespace PlantEncyclopedia.Api.Plants;

/// <summary>JSON contract for GET /api/plants/{plantId}. Changing it changes the public API.</summary>
public sealed record PlantDetailResponse(
    Guid Id,
    string Name,
    string? PlantGroup,
    string? PlantGroupId,
    string ScientificName,
    string? Description,
    HeroImageResponse? HeroImage,
    QuickCareResponse? QuickCare,
    IReadOnlyList<AttributionResponse> Attributions)
{
    public static PlantDetailResponse From(PlantDetail plant) => new(
        plant.Id,
        plant.Name,
        plant.PlantGroupName,
        plant.PlantGroupSlug,
        plant.ScientificName,
        plant.Description,
        HeroImage: null, // Hero image selection is not decided yet.
        QuickCareResponse.From(plant.Care),
        AttributionResponse.From(plant.Sources));
}

/// <summary>A credit line for an external source, e.g. type "taxonomy", text "Catalogue of Life, via GBIF".</summary>
public sealed record AttributionResponse(string Type, string Text)
{
    // Display names of providers. Sources from other providers are not listed: a raw provider key means
    // nothing to users, and every provider needs its attribution text reviewed before it is shown.
    private static readonly Dictionary<string, string> ProviderNames = new(StringComparer.Ordinal)
    {
        [KnownProviders.GbifCatalogueOfLife] = "Catalogue of Life, via GBIF"
    };

    public static IReadOnlyList<AttributionResponse> From(IEnumerable<PlantSourceInfo> sources) => sources
        .Where(s => ProviderNames.ContainsKey(s.Provider))
        .Select(s => new AttributionResponse(
            JsonNamingPolicy.CamelCase.ConvertName(s.SourceType.ToString()),
            ProviderNames[s.Provider]))
        .Distinct()
        .ToList();
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
