using System.Globalization;
using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Retail;

namespace PlantEncyclopedia.Api.Retail;

/// <summary>JSON contract for one nearby shop (API_SPEC §47). Changing it changes the public API.</summary>
public sealed record RetailerResponse(
    Guid Id,
    string Name,
    string Type,
    RetailerLocationResponse Location,
    double? Rating,
    string? Website,
    RetailerAvailabilityResponse Availability);

public sealed record RetailerLocationResponse(string? Address, string? City, string? Region, double DistanceKm);

/// <summary>Always "unknown": no source says what a shop has in stock (D-033, API_SPEC §48).</summary>
public sealed record RetailerAvailabilityResponse(string Status);

public sealed record RetailerAttributionResponse(string Name, string Notice, string License, string Url, DateTimeOffset? RetrievedAt);

/// <summary>
/// Where to Buy (API_SPEC §45–51, D-031–D-035): shops near a location that may sell a plant. The location comes from
/// the request only and is never stored or logged (§50). Shops come from an OpenStreetMap snapshot, credited in every
/// response; there are no ratings, prices or stock claims.
/// </summary>
public static class RetailerEndpoints
{
    public const double DefaultRadiusKm = 25;
    public const double MaxRadiusKm = 100;
    public const int MaxPlaces = 50;

    private static readonly RetailerAttributionResponse OpenStreetMap = new(
        "OpenStreetMap", "© OpenStreetMap contributors", "ODbL 1.0", "https://www.openstreetmap.org/copyright", null);

    public static IEndpointRouteBuilder MapRetailerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plants/{plantId}/retailers", GetRetailersAsync);
        app.MapGet("/api/retailers/places", GetPlacesAsync);
        return app;
    }

    // GET /api/plants/{plantId}/retailers?latitude=&longitude=&radius=&sort=distance|relevance&page=&pageSize=
    private static async Task<IResult> GetRetailersAsync(
        string plantId,
        string? latitude,
        string? longitude,
        string? radius,
        string? sort,
        string? page,
        string? pageSize,
        IRetailerQueries queries,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(plantId, out var id)) return ApiErrors.Validation("plantId", "Plant ID must be a valid UUID.");
        var (request, pagingError) = Pagination.Parse(page, pageSize);
        if (pagingError is not null) return pagingError;

        var errors = new Dictionary<string, string>();
        var lat = Number(latitude, -90, 90, "latitude", "Latitude must be a number from -90 to 90.", errors);
        var lon = Number(longitude, -180, 180, "longitude", "Longitude must be a number from -180 to 180.", errors);
        var radiusKm = radius is null ? DefaultRadiusKm : Number(radius, 1, MaxRadiusKm, "radius", "Radius must be 1 to 100 km.", errors);
        RetailerSort? order = sort switch
        {
            null or "distance" => RetailerSort.Distance,
            "relevance" => RetailerSort.Relevance,
            _ => null
        };
        if (order is null) errors["sort"] = "Sort must be distance or relevance.";
        if (errors.Count > 0) return ApiErrors.Validation(errors);

        var (found, category) = await queries.FindPlantCategoryAsync(id, cancellationToken);
        if (!found) return ApiErrors.NotFound("PLANT_NOT_FOUND", "The requested plant could not be found.");

        var nearby = await queries.FindNearbyAsync(
            lat, lon, radiusKm, RetailerRelevance.KindsFor(category), order!.Value, request.Page, request.PageSize, cancellationToken);
        return Results.Ok(new
        {
            data = nearby.Retailers.Select(ToResponse).ToList(),
            pagination = new PaginationResponse(request.Page, request.PageSize, nearby.HasNext),
            attribution = OpenStreetMap with { RetrievedAt = nearby.Retailers.Count == 0 ? null : nearby.Retailers.Max(r => r.RetrievedAt) }
        });
    }

    // GET /api/retailers/places: towns in the shop data, for choosing a location by hand.
    private static async Task<IResult> GetPlacesAsync(IRetailerQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(new
        {
            data = (await queries.ListPlacesAsync(MaxPlaces, cancellationToken))
                .Select(p => new { name = p.Name, region = p.Region, latitude = Math.Round(p.Latitude, 4), longitude = Math.Round(p.Longitude, 4) })
                .ToList()
        });

    private static RetailerResponse ToResponse(NearbyRetailer r) => new(
        r.Id,
        r.Name,
        r.Kind switch
        {
            RetailerKind.GardenCentre => "gardenCentre",
            RetailerKind.FlowerShop => "flowerShop",
            _ => "nursery"
        },
        new RetailerLocationResponse(r.Street, r.City, r.Region, Math.Round(r.DistanceKm, 1)),
        Rating: null,
        r.Website,
        new RetailerAvailabilityResponse("unknown"));

    private static double Number(string? value, double min, double max, string field, string message, Dictionary<string, string> errors)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number) && number >= min && number <= max)
        {
            return number;
        }

        errors[field] = message;
        return 0;
    }
}
