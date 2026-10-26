using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Retail;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Infrastructure.Retail;

/// <summary>
/// British Columbia's garden centres, florists and named plant nurseries from OpenStreetMap, in one Overpass API query
/// (overpass-api.de, used within its fair-use guidance: one query per refresh). Data © OpenStreetMap contributors,
/// ODbL 1.0; the API credits it with every result. HTTPS to overpass-api.de only, no redirects, no retries, a
/// 3-minute timeout and a 20 MB limit. Any failure returns null, so nothing stored changes.
/// </summary>
internal sealed partial class OverpassRetailerSource(HttpClient http, ILogger<OverpassRetailerSource> logger) : IRetailerSource
{
    public const string SourceName = "openstreetmap";

    public static readonly Uri BaseAddress = new("https://overpass-api.de/");
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    public const int MaxResponseBytes = 20 * 1024 * 1024;

    /// <summary>BC by its ISO 3166-2 code; ways and relations come back as their centre point.</summary>
    public const string Query = """
        [out:json][timeout:120];
        area["ISO3166-2"="CA-BC"]->.bc;
        (
          nwr["shop"="garden_centre"](area.bc);
          nwr["shop"="florist"](area.bc);
          nwr["landuse"="plant_nursery"]["name"](area.bc);
        );
        out center tags;
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void ConfigureClient(HttpClient client, GbifOptions contact)
    {
        client.BaseAddress = BaseAddress;
        client.Timeout = Timeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        var version = typeof(OverpassRetailerSource).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{contact.Contact})"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<RetailerSnapshot?> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var body = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", Query)]);
            using var response = await http.PostAsync("api/interpreter", body, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                LogUnexpectedContentType(logger);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OverpassResponse>(JsonOptions, cancellationToken);
            if (result?.Elements is null)
            {
                LogInvalidJson(logger);
                return null;
            }

            var dataTime = DateTimeOffset.TryParse(result.Osm3s?.TimestampOsmBase, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at.ToUniversalTime()
                : DateTimeOffset.UtcNow;
            return new RetailerSnapshot(SourceName, result.Elements.Select(ToRecord).OfType<RetailerRecord>().ToList(), dataTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, Timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException e)
        {
            LogRequestFailed(logger, e.HttpRequestError);
            return null;
        }
        catch (JsonException)
        {
            LogInvalidJson(logger);
            return null;
        }
    }

    /// <summary>One shop, or null without a name, a known kind or a position. A shop tag wins over the nursery tag.</summary>
    internal static RetailerRecord? ToRecord(OverpassElement? element)
    {
        if (element?.Tags is not { } tags || element.Type is not ("node" or "way" or "relation")) return null;

        var kind = tags.GetValueOrDefault("shop") switch
        {
            "garden_centre" => RetailerKind.GardenCentre,
            "florist" => RetailerKind.FlowerShop,
            _ when tags.GetValueOrDefault("landuse") == "plant_nursery" => RetailerKind.Nursery,
            _ => (RetailerKind?)null
        };
        var (latitude, longitude) = element.Lat is { } lat && element.Lon is { } lon
            ? (lat, lon)
            : (element.Center?.Lat, element.Center?.Lon) is ({ } clat, { } clon) ? (clat, clon) : (double.NaN, double.NaN);
        if (kind is null || !tags.TryGetValue("name", out var name) || double.IsNaN(latitude)) return null;

        var street = string.Join(' ', new[] { tags.GetValueOrDefault("addr:housenumber"), tags.GetValueOrDefault("addr:street") }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return new RetailerRecord(
            SourceId: $"{element.Type}/{element.Id}",
            Name: name,
            Kind: kind.Value,
            Street: street.Length > 0 ? street : null,
            City: tags.GetValueOrDefault("addr:city"),
            Region: tags.GetValueOrDefault("addr:province") ?? tags.GetValueOrDefault("addr:state") ?? "BC",
            Postcode: tags.GetValueOrDefault("addr:postcode"),
            Website: tags.GetValueOrDefault("website") ?? tags.GetValueOrDefault("contact:website"),
            Latitude: latitude,
            Longitude: longitude);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overpass returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overpass returned a response that is not JSON.")]
    private static partial void LogUnexpectedContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overpass timed out after {Seconds} seconds.")]
    private static partial void LogTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overpass request failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overpass returned invalid JSON.")]
    private static partial void LogInvalidJson(ILogger logger);
}

/// <summary>The fields used from Overpass JSON output (OSM JSON: version, generator, osm3s, elements).</summary>
internal sealed record OverpassResponse(OverpassMeta? Osm3s, IReadOnlyList<OverpassElement?>? Elements);

internal sealed record OverpassMeta([property: JsonPropertyName("timestamp_osm_base")] string? TimestampOsmBase);

/// <summary>A node has lat/lon; a way or relation output with "out center" has a center point.</summary>
internal sealed record OverpassElement(
    string? Type,
    long Id,
    double? Lat,
    double? Lon,
    OverpassCenter? Center,
    IReadOnlyDictionary<string, string>? Tags);

internal sealed record OverpassCenter(double? Lat, double? Lon);
