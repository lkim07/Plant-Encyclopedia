using PlantEncyclopedia.Domain.Retail;

namespace PlantEncyclopedia.Application.Retail;

/// <summary>A shop as read from the outside source, before it is checked.</summary>
public sealed record RetailerRecord(
    string SourceId,
    string Name,
    RetailerKind Kind,
    string? Street,
    string? City,
    string? Region,
    string? Postcode,
    string? Website,
    double Latitude,
    double Longitude);

/// <summary>A full copy of the shops at a source, taken at one moment.</summary>
public sealed record RetailerSnapshot(string Source, IReadOnlyList<RetailerRecord> Records, DateTimeOffset RetrievedAt);

/// <summary>Reads the whole snapshot from the source (implemented in Infrastructure); null when it cannot.</summary>
public interface IRetailerSource
{
    Task<RetailerSnapshot?> FetchAsync(CancellationToken cancellationToken);
}

public sealed record RetailerImportSummary(int Added, int Updated, int Removed);

/// <summary>Stores a snapshot: adds new shops, updates known ones, removes those the source no longer has.</summary>
public interface IRetailerStore
{
    Task<int> CountAsync(string source, CancellationToken cancellationToken);

    Task<RetailerImportSummary> ReplaceAsync(RetailerSnapshot snapshot, CancellationToken cancellationToken);
}

public enum RetailerSort
{
    Distance,

    /// <summary>The shop kinds that suit the plant first (<see cref="RetailerRelevance"/>), then distance.</summary>
    Relevance
}

/// <param name="DistanceKm">Straight-line distance from the requested location.</param>
public sealed record NearbyRetailer(
    Guid Id,
    string Name,
    RetailerKind Kind,
    string? Street,
    string? City,
    string? Region,
    string? Website,
    double DistanceKm,
    string Source,
    DateTimeOffset RetrievedAt);

public sealed record NearbyRetailersPage(IReadOnlyList<NearbyRetailer> Retailers, bool HasNext);

/// <summary>A town in the shop data, for choosing a location by hand: the average position of its shops.</summary>
public sealed record RetailerPlace(string Name, string? Region, double Latitude, double Longitude, int Shops);

/// <summary>Read-only queries for Where to Buy (API_SPEC §45–51).</summary>
public interface IRetailerQueries
{
    /// <summary>The plant's category slug, or null when the plant has none; <c>found</c> is false when it is not Published.</summary>
    Task<(bool Found, string? CategorySlug)> FindPlantCategoryAsync(Guid plantId, CancellationToken cancellationToken);

    /// <summary>Shops of the given kinds within <paramref name="radiusKm"/>, each once.</summary>
    Task<NearbyRetailersPage> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyList<RetailerKind> kinds,
        RetailerSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Towns with at least one shop, the ones with the most shops first.</summary>
    Task<IReadOnlyList<RetailerPlace>> ListPlacesAsync(int limit, CancellationToken cancellationToken);
}
