using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Domain.Retail;
using PlantEncyclopedia.Infrastructure.Persistence;

namespace PlantEncyclopedia.Infrastructure.Retail;

/// <summary>
/// Where to Buy queries. Distances are great-circle (haversine, Earth radius 6,371 km), computed in one parameterized
/// statement after a latitude/longitude box narrows the rows; no PostGIS is needed for a province's shops.
/// The requested location is only used for the query: never stored or logged.
/// </summary>
internal sealed class RetailerQueries(PlantEncyclopediaDbContext db) : IRetailerQueries
{
    private const double KmPerDegreeLatitude = 111.32;

    public async Task<(bool Found, string? CategorySlug)> FindPlantCategoryAsync(Guid plantId, CancellationToken cancellationToken)
    {
        var plant = await db.Plants
            .AsNoTracking()
            .Where(p => p.Id == plantId && p.Status == PlantStatus.Published)
            .Select(p => new { Category = p.PlantGroup != null ? p.PlantGroup.Category!.Slug : null })
            .SingleOrDefaultAsync(cancellationToken);
        return plant is null ? (false, null) : (true, plant.Category);
    }

    public async Task<NearbyRetailersPage> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyList<RetailerKind> kinds,
        RetailerSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var latDelta = radiusKm / KmPerDegreeLatitude;
        var lonDelta = radiusKm / (KmPerDegreeLatitude * Math.Max(0.01, Math.Cos(latitude * Math.PI / 180)));
        var (minLat, maxLat, minLon, maxLon) = (latitude - latDelta, latitude + latDelta, longitude - lonDelta, longitude + lonDelta);
        var kindNames = kinds.Select(k => k.ToString()).ToArray();
        var byRelevance = sort == RetailerSort.Relevance;
        var offset = (page - 1) * pageSize;
        var limit = pageSize + 1; // one extra row tells us whether another page exists

        var rows = await db.Database.SqlQuery<NearbyRow>($"""
            WITH boxed AS (
                SELECT r.*,
                       2 * 6371 * asin(least(1, sqrt(
                           power(sin(radians(r.latitude - {latitude}) / 2), 2)
                           + cos(radians({latitude})) * cos(radians(r.latitude)) * power(sin(radians(r.longitude - {longitude}) / 2), 2)
                       ))) AS distance_km,
                       array_position({kindNames}, r.kind) AS kind_rank
                FROM retailers r
                WHERE r.latitude BETWEEN {minLat} AND {maxLat}
                  AND r.longitude BETWEEN {minLon} AND {maxLon}
                  AND r.kind = ANY({kindNames})
            )
            SELECT id, name, kind, street, city, region, website, distance_km, source, retrieved_at
            FROM boxed
            WHERE distance_km <= {radiusKm}
            ORDER BY CASE WHEN {byRelevance} THEN kind_rank ELSE 0 END, distance_km, name, id
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return new NearbyRetailersPage(
            rows.Take(pageSize)
                .Select(r => new NearbyRetailer(r.Id, r.Name, Enum.Parse<RetailerKind>(r.Kind), r.Street, r.City, r.Region, r.Website, r.DistanceKm, r.Source, r.RetrievedAt))
                .ToList(),
            rows.Count > pageSize);
    }

    public async Task<IReadOnlyList<RetailerPlace>> ListPlacesAsync(int limit, CancellationToken cancellationToken)
    {
        var places = await db.Retailers
            .AsNoTracking()
            .Where(r => r.City != null)
            .GroupBy(r => new { r.City, r.Region })
            .Select(g => new { g.Key.City, g.Key.Region, Latitude = g.Average(r => r.Latitude), Longitude = g.Average(r => r.Longitude), Shops = g.Count() })
            .OrderByDescending(p => p.Shops)
            .ThenBy(p => p.City)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return places.Select(p => new RetailerPlace(p.City!, p.Region, p.Latitude, p.Longitude, p.Shops)).ToList();
    }

    private sealed class NearbyRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Kind { get; init; }
        public string? Street { get; init; }
        public string? City { get; init; }
        public string? Region { get; init; }
        public string? Website { get; init; }
        public double DistanceKm { get; init; }
        public required string Source { get; init; }
        public DateTimeOffset RetrievedAt { get; init; }
    }
}
