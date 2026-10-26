using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Retail;
using PlantEncyclopedia.Infrastructure.Retail;
using PlantEncyclopedia.Tests.Api;
using PlantEncyclopedia.Tests.ExternalData;

namespace PlantEncyclopedia.Tests.Retail;

/// <summary>Where to Buy without a database or network: reading OpenStreetMap records, cleaning them, and relevance.</summary>
public class RetailerUnitTests
{
    // Constructed from the documented OSM JSON shape (elements with type, id, lat/lon, tags; ways with a center from
    // "out center"); not a recorded Overpass response, since no live request has been approved yet.
    private const string ConstructedResponse = """
        {
          "version": 0.6,
          "generator": "Overpass API",
          "osm3s": { "timestamp_osm_base": "2026-10-20T08:00:00Z", "copyright": "The data included in this document is from www.openstreetmap.org. The data is made available under ODbL." },
          "elements": [
            { "type": "node", "id": 101, "lat": 49.2634, "lon": -123.1386,
              "tags": { "shop": "florist", "name": "Test Florist", "addr:housenumber": "12", "addr:street": "Main Street", "addr:city": "Vancouver", "website": "https://florist.example" } },
            { "type": "way", "id": 202, "center": { "lat": 49.2, "lon": -123.0 },
              "tags": { "shop": "garden_centre", "landuse": "plant_nursery", "name": "Test Garden Centre" } },
            { "type": "way", "id": 303, "center": { "lat": 49.1, "lon": -122.9 }, "tags": { "landuse": "plant_nursery", "name": "Test Nursery" } },
            { "type": "node", "id": 404, "lat": 49.0, "lon": -123.0, "tags": { "shop": "florist" } },
            { "type": "node", "id": 505, "lat": 49.0, "lon": -123.0, "tags": { "shop": "bakery", "name": "Not a plant shop" } },
            { "type": "way", "id": 606, "tags": { "shop": "florist", "name": "No position" } }
          ]
        }
        """;

    private static OverpassRetailerSource Source(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        OverpassRetailerSource.ConfigureClient(client, GbifFixtures.Options());
        return new OverpassRetailerSource(client, LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider())).CreateLogger<OverpassRetailerSource>());
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Protects against: missing shops (ways mapped as areas), wrong kinds, or records without a name or position.
    [Fact]
    public async Task Overpass_ReadsShopsFromNodesAndAreas_AndSkipsUnusableOnes()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Json(ConstructedResponse);
        });

        var snapshot = (await Source(handler).FetchAsync(CancellationToken.None))!;

        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, "https://overpass-api.de/api/interpreter"), (request.Method, request.RequestUri!.ToString()));
        Assert.StartsWith("data=", body);
        Assert.Contains(Uri.EscapeDataString("\"ISO3166-2\"=\"CA-BC\"").Replace("%20", "+"), body!.Replace("%20", "+"));
        Assert.Equal("openstreetmap", snapshot.Source);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), snapshot.RetrievedAt);
        Assert.Equal(
            [("node/101", RetailerKind.FlowerShop), ("way/202", RetailerKind.GardenCentre), ("way/303", RetailerKind.Nursery)],
            snapshot.Records.Select(r => (r.SourceId, r.Kind)));
        var florist = snapshot.Records[0];
        Assert.Equal(("12 Main Street", "Vancouver", "BC", "https://florist.example"), (florist.Street, florist.City, florist.Region, florist.Website));
        Assert.Equal((49.2, -123.0), (snapshot.Records[1].Latitude, snapshot.Records[1].Longitude));
    }

    // Protects against: an error page or overload answer (429, 504) replacing the stored shops.
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Overpass_Failures_ReturnNothing(HttpStatusCode status)
    {
        var failed = StubHttpMessageHandler.Returning(() => Json("{}", status));
        var html = StubHttpMessageHandler.Returning(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>", Encoding.UTF8, "text/html")
        });

        Assert.Null(await Source(failed).FetchAsync(CancellationToken.None));
        Assert.Null(await Source(html).FetchAsync(CancellationToken.None));
    }

    // Protects against: unsafe links (javascript:, credentials) or broken text from the source reaching the page.
    [Fact]
    public void Clean_KeepsOnlySafeLinksAndUsableRecords()
    {
        var record = new RetailerRecord("node/1", "  Green  Thumb  ", RetailerKind.Nursery, null, "Vancouver", "BC", null,
            "https://shop.example/plants", 49.2, -123.1);

        Assert.Equal(("Green Thumb", "https://shop.example/plants"), (RetailerImportService.Clean(record)!.Name, RetailerImportService.Clean(record)!.Website));
        Assert.Null(RetailerImportService.Clean(record with { Website = "javascript:alert(1)" })!.Website);
        Assert.Null(RetailerImportService.Clean(record with { Website = "https://user:pass@shop.example" })!.Website);
        Assert.Null(RetailerImportService.Clean(record with { Name = "" }));
        Assert.Null(RetailerImportService.Clean(record with { Name = new string('a', 201) }));
        Assert.Null(RetailerImportService.Clean(record with { Latitude = double.NaN }));
        Assert.Null(RetailerImportService.Clean(record with { Longitude = 200 }));
        Assert.Null(RetailerImportService.Clean(record with { City = "Van\u0007couver" })!.City);
    }

    // Protects against: a partial or failed answer wiping the stored shops.
    [Fact]
    public async Task Import_RefusesASnapshotMuchSmallerThanTheStoredOne()
    {
        var one = new RetailerRecord("node/1", "Only one", RetailerKind.Nursery, null, null, null, null, null, 49, -123);
        var store = new FakeStore(stored: 10);
        var small = new RetailerImportService(new FakeSource(new RetailerSnapshot("openstreetmap", [one], DateTimeOffset.UtcNow)), store);
        var unavailable = new RetailerImportService(new FakeSource(null), store);

        Assert.Equal(RetailerImportStatus.Suspicious, (await small.ImportAsync(CancellationToken.None)).Status);
        Assert.Equal(RetailerImportStatus.Unavailable, (await unavailable.ImportAsync(CancellationToken.None)).Status);
        Assert.Equal(0, store.Replacements);
    }

    // Protects against: florists offered for trees and vegetables, or flowers not leading with florists.
    [Fact]
    public void Relevance_MatchesShopKindsToTheCategory()
    {
        Assert.Equal(RetailerKind.FlowerShop, RetailerRelevance.KindsFor("flowers")[0]);
        Assert.DoesNotContain(RetailerKind.FlowerShop, RetailerRelevance.KindsFor("trees"));
        Assert.DoesNotContain(RetailerKind.FlowerShop, RetailerRelevance.KindsFor("vegetables"));
        Assert.Equal(3, RetailerRelevance.KindsFor(null).Count); // a plant without a group: every kind
    }

    private sealed class FakeSource(RetailerSnapshot? snapshot) : IRetailerSource
    {
        public Task<RetailerSnapshot?> FetchAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class FakeStore(int stored) : IRetailerStore
    {
        public int Replacements { get; private set; }

        public Task<int> CountAsync(string source, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task<RetailerImportSummary> ReplaceAsync(RetailerSnapshot snapshot, CancellationToken cancellationToken)
        {
            Replacements++;
            return Task.FromResult(new RetailerImportSummary(snapshot.Records.Count, 0, 0));
        }
    }
}
