using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.Identification;
using PlantEncyclopedia.Infrastructure.Identification;
using PlantEncyclopedia.Tests.Api;
using PlantEncyclopedia.Tests.ExternalData;
using SkiaSharp;

namespace PlantEncyclopedia.Tests.Identification;

/// <summary>
/// Identification without a database or network: the Pl@ntNet adapter against an in-memory handler, photo checks
/// and re-encoding, the cache and budget, and the candidate rules of the service.
/// </summary>
public class IdentificationUnitTests
{
    private const string ApiKey = "test-key-not-real";

    // Constructed from the field names in Pl@ntNet's documentation (my.plantnet.org/doc/api/identify); not a
    // recorded live response, since no live request has been approved yet.
    private const string DocumentedShapeResponse = """
        {
          "query": { "project": "all", "organs": ["auto"] },
          "bestMatch": "Rosa canina L.",
          "results": [
            { "score": 0.82, "species": { "scientificNameWithoutAuthor": "Rosa canina", "scientificNameAuthorship": "L.", "commonNames": ["Dog rose"] }, "gbif": { "id": "1" } },
            { "score": 0.09, "species": { "scientificNameWithoutAuthor": "Rosa rubiginosa", "scientificNameAuthorship": "L.", "commonNames": [] } },
            { "score": 0.01, "species": { "scientificNameWithoutAuthor": "Rosa arvensis" } },
            { "score": 0.5, "species": { } },
            { "species": { "scientificNameWithoutAuthor": "No score" } }
          ],
          "version": "test",
          "remainingIdentificationRequests": 499
        }
        """;

    private static PlantNetIdentificationProvider Provider(HttpMessageHandler handler, CapturingLoggerProvider? logs = null)
    {
        var options = new PlantNetOptions { Enabled = true, ApiKey = ApiKey };
        var client = new HttpClient(handler);
        PlantNetIdentificationProvider.ConfigureClient(client);
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs ?? new CapturingLoggerProvider()));
        return new PlantNetIdentificationProvider(client, options, loggerFactory.CreateLogger<PlantNetIdentificationProvider>());
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Protects against: a request Pl@ntNet would not accept (documented endpoint, field names and organ value).
    [Fact]
    public async Task PlantNet_SendsOneJpegWithAutoOrgan_AndReadsTheResults()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Json(DocumentedShapeResponse);
        });

        var answer = await Provider(handler).IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://my-api.plantnet.org/v2/identify/all?api-key=test-key-not-real&nb-results=10", request.RequestUri!.ToString());
        Assert.Contains("name=images; filename=photo.jpg", body);
        Assert.Contains("Content-Type: image/jpeg", body);
        Assert.Contains("name=organs", body);
        Assert.Contains("auto", body);

        Assert.Equal(ProviderIdentificationStatus.Answered, answer.Status);
        Assert.Equal([("Rosa canina", 0.82), ("Rosa rubiginosa", 0.09), ("Rosa arvensis", 0.01)],
            answer.Candidates.Select(c => (c.ScientificName, c.Score)));
    }

    // Protects against: an error answer read as "not a plant", or the API key written to the logs.
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task PlantNet_AnyErrorStatus_IsUnavailable_AndTheKeyIsNeverLogged(HttpStatusCode status)
    {
        var logs = new CapturingLoggerProvider();
        var handler = StubHttpMessageHandler.Returning(() => Json("""{"message":"x"}""", status));

        var answer = await Provider(handler, logs).IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        Assert.Equal(ProviderIdentificationStatus.Unavailable, answer.Status);
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, e => e.Contains(ApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlantNet_InvalidOrNonJsonAnswers_AreUnavailable()
    {
        var html = StubHttpMessageHandler.Returning(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>", Encoding.UTF8, "text/html")
        });
        var broken = StubHttpMessageHandler.Returning(() => Json("{ not json"));

        Assert.Equal(ProviderIdentificationStatus.Unavailable, (await Provider(html).IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None)).Status);
        Assert.Equal(ProviderIdentificationStatus.Unavailable, (await Provider(broken).IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None)).Status);
    }

    [Fact]
    public void Options_RequireAKeyWhenEnabled()
    {
        new PlantNetOptions().Validate(); // disabled: nothing required
        Assert.Throws<InvalidOperationException>(() => new PlantNetOptions { Enabled = true }.Validate());
        Assert.Throws<InvalidOperationException>(() => new PlantNetOptions { Enabled = true, ApiKey = "k", Project = "../all" }.Validate());
        new PlantNetOptions { Enabled = true, ApiKey = "k" }.Validate();
    }

    // Protects against: large photos sent as they are, or other formats passed to the provider.
    [Fact]
    public void Processor_ShrinksPhotosToAJpegOfAtMost1280Pixels()
    {
        var processor = new IdentificationImageProcessor();

        var large = processor.Prepare(TestPhotos.Jpeg(4000, 3000))!;
        var png = processor.Prepare(TestPhotos.Png(800, 600))!;

        Assert.Equal((1280, 960), TestPhotos.SizeOf(large));
        Assert.Equal(SKEncodedImageFormat.Jpeg, TestPhotos.FormatOf(large));
        Assert.Equal((800, 600), TestPhotos.SizeOf(png));
        Assert.Equal(SKEncodedImageFormat.Jpeg, TestPhotos.FormatOf(png));
    }

    // Protects against: location or other metadata leaving the server, and sideways phone photos (SECURITY §11).
    [Fact]
    public void Processor_DropsMetadata_AndTurnsThePhotoUpright()
    {
        var photo = TestPhotos.JpegWithExif(400, 200, orientation: 6, marker: "GPS-49.28N-123.12W");
        Assert.Contains("GPS-49.28N-123.12W", Encoding.ASCII.GetString(photo));

        var prepared = new IdentificationImageProcessor().Prepare(photo)!;

        Assert.DoesNotContain("GPS-49.28N-123.12W", Encoding.ASCII.GetString(prepared));
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(prepared));
        Assert.Equal((200, 400), TestPhotos.SizeOf(prepared));
    }

    // Protects against: arbitrary files reaching the provider (SECURITY §10).
    [Fact]
    public void Processor_RejectsAnythingButAReasonablePhoto()
    {
        var processor = new IdentificationImageProcessor();
        var gif = "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray();

        Assert.Null(processor.Prepare("not an image"u8.ToArray()));
        Assert.Null(processor.Prepare(gif));
        Assert.Null(processor.Prepare(TestPhotos.Jpeg(40, 40))); // too small to identify
        Assert.Null(processor.Prepare(TestPhotos.Jpeg()[..200])); // truncated
    }

    // Protects against: paying twice for the same photo, or remembering a failure as an answer.
    [Fact]
    public async Task Guard_AnswersARepeatedPhotoFromTheCache_ButNotAFailure()
    {
        var answers = new Queue<HttpResponseMessage>([Json("{}", HttpStatusCode.InternalServerError), Json(DocumentedShapeResponse)]);
        var handler = StubHttpMessageHandler.Returning(() => answers.Dequeue());
        using var cache = new IdentificationCache(TimeProvider.System);
        var guard = new GuardedIdentificationProvider(Provider(handler), cache, new IdentificationCallBudget(TimeProvider.System));
        var photo = TestPhotos.Jpeg();

        Assert.Equal(ProviderIdentificationStatus.Unavailable, (await guard.IdentifyAsync(photo, CancellationToken.None)).Status);
        Assert.Equal(ProviderIdentificationStatus.Answered, (await guard.IdentifyAsync(photo, CancellationToken.None)).Status);
        Assert.Equal(ProviderIdentificationStatus.Answered, (await guard.IdentifyAsync(photo, CancellationToken.None)).Status);

        Assert.Equal(2, handler.Requests.Count);
    }

    // Protects against: identification spending more than the app-wide budget (cost control, API_SPEC §40).
    [Fact]
    public async Task Guard_StopsCallingWhenTheBudgetIsUsedUp()
    {
        var handler = StubHttpMessageHandler.Returning(() => Json(DocumentedShapeResponse));
        using var cache = new IdentificationCache(TimeProvider.System);
        var budget = new IdentificationCallBudget(TimeProvider.System);
        while (budget.TryAcquire()) { }
        var guard = new GuardedIdentificationProvider(Provider(handler), cache, budget);

        var answer = await guard.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        Assert.Equal(ProviderIdentificationStatus.Unavailable, answer.Status);
        Assert.Empty(handler.Requests);
    }

    // Protects against: unlikely guesses shown as results, more than three candidates, or one plant twice (D-012).
    [Fact]
    public async Task Service_KeepsLikelySuggestionsInOrder_AndReturnsAtMostThreePlants()
    {
        var provider = new FakeIdentificationProvider(ProviderIdentification.Answered(
        [
            new("Rosa  rubiginosa", 0.3),
            new("Rosa canina", 0.6),
            new("Rosa arvensis", 0.01),
            new("Rosa canina", 0.2),
            new("Rosa gallica", 0.1)
        ]));
        var queries = new FakeIdentificationQueries();
        var importer = new FakeImporter(queries);
        var service = new PlantIdentificationService(new IdentificationImageProcessor(), provider, queries, importer);

        var result = await service.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        Assert.Equal(["Rosa canina", "Rosa rubiginosa", "Rosa gallica"], queries.LookedUp);
        Assert.Empty(importer.Calls); // every likely suggestion is already in the encyclopedia
        Assert.Equal(IdentificationStatus.Identified, result.Status);
        Assert.Equal(["Dog rose", "Dog rose 'Cultivar'", "Sweet briar"], result.Candidates.Select(p => p.Name));
    }

    [Fact]
    public async Task Service_ReportsInvalidPhotos_UnavailableProviders_AndUnmatchedSuggestions()
    {
        var queries = new FakeIdentificationQueries();
        var unavailable = new PlantIdentificationService(new IdentificationImageProcessor(), new FakeIdentificationProvider(ProviderIdentification.Unavailable), queries, new FakeImporter(queries));
        var unknown = new PlantIdentificationService(new IdentificationImageProcessor(),
            new FakeIdentificationProvider(ProviderIdentification.Answered([new("Quercus robur", 0.9)])), queries, new FakeImporter(queries));
        var unlikely = new PlantIdentificationService(new IdentificationImageProcessor(),
            new FakeIdentificationProvider(ProviderIdentification.Answered([new("Rosa canina", 0.01)])), queries, new FakeImporter(queries));

        Assert.Equal(IdentificationStatus.InvalidImage, (await unavailable.IdentifyAsync("text"u8.ToArray(), CancellationToken.None)).Status);
        Assert.Equal(IdentificationStatus.Unavailable, (await unavailable.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None)).Status);
        Assert.Equal(IdentificationStatus.NotIdentified, (await unknown.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None)).Status);
        Assert.Equal(IdentificationStatus.NotIdentified, (await unlikely.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None)).Status);
    }

    // Protects against: a likely species missing from results just because it was not in the encyclopedia yet (D-072),
    // or an imported plant placed out of the provider's order.
    [Fact]
    public async Task Service_ImportsAMissingSuggestion_AndShowsItAtItsRank()
    {
        var provider = new FakeIdentificationProvider(ProviderIdentification.Answered([new("Quercus robur", 0.6), new("Rosa canina", 0.3)]));
        var queries = new FakeIdentificationQueries();
        var importer = new FakeImporter(queries, importable: ["Quercus robur"]);
        var service = new PlantIdentificationService(new IdentificationImageProcessor(), provider, queries, importer);

        var result = await service.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        Assert.Equal(["Quercus robur"], importer.Calls);
        Assert.Equal(["Imported Quercus robur", "Dog rose", "Dog rose 'Cultivar'"], result.Candidates.Select(p => p.Name));
    }

    // Protects against: one photo starting many slow imports (each uses the GBIF budget), or a failed import failing
    // the identification.
    [Fact]
    public async Task Service_TriesAtMostThreeImports_AndSkipsOnesThatFail()
    {
        var provider = new FakeIdentificationProvider(ProviderIdentification.Answered(
            Enumerable.Range(1, 6).Select(i => new ProviderCandidate($"Unknownus species{i}", 0.5 - i * 0.01)).ToList()));
        var queries = new FakeIdentificationQueries();
        var importer = new FakeImporter(queries);
        var service = new PlantIdentificationService(new IdentificationImageProcessor(), provider, queries, importer);

        var result = await service.IdentifyAsync(TestPhotos.Jpeg(), CancellationToken.None);

        Assert.Equal(IdentificationStatus.NotIdentified, result.Status);
        Assert.Equal(PlantIdentificationService.MaxImports, importer.Calls.Count);
    }

    private sealed class FakeIdentificationProvider(ProviderIdentification answer) : IPlantIdentificationProvider
    {
        public Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken) => Task.FromResult(answer);
    }

    /// <summary>Knows Rosa canina (a species and one cultivar) and Rosa rubiginosa and Rosa gallica.</summary>
    private sealed class FakeIdentificationQueries : IIdentificationQueries
    {
        private static readonly Dictionary<string, string[]> Plants = new()
        {
            ["Rosa canina"] = ["Dog rose", "Dog rose 'Cultivar'"],
            ["Rosa rubiginosa"] = ["Sweet briar"],
            ["Rosa gallica"] = ["French rose"]
        };

        public List<string> LookedUp { get; } = [];

        /// <summary>Plants added by <see cref="FakeImporter"/>, found by ID afterwards.</summary>
        public Dictionary<Guid, MatchedPlant> Imported { get; } = [];

        public Task<MatchedPlant?> FindPublishedByIdAsync(Guid plantId, CancellationToken cancellationToken) =>
            Task.FromResult(Imported.GetValueOrDefault(plantId));

        public Task<PlantEncyclopedia.Application.Explore.ExplorePage> GetRelatedAsync(
            IReadOnlyList<Guid> candidateIds, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the identification service.");

        public Task<IReadOnlyList<MatchedPlant>> FindPublishedAsync(IReadOnlyList<string> scientificNames, CancellationToken cancellationToken)
        {
            LookedUp.AddRange(scientificNames);
            IReadOnlyList<MatchedPlant> matches = scientificNames
                .SelectMany((name, index) => Plants.GetValueOrDefault(name, [])
                    .Select(plant => new MatchedPlant(index, Guid.NewGuid(), plant, "Roses", "roses", name, null)))
                .ToList();
            return Task.FromResult(matches);
        }
    }

    /// <summary>Imports only the names in <paramref name="importable"/>, as Published plants; records every attempt.</summary>
    private sealed class FakeImporter(FakeIdentificationQueries queries, string[]? importable = null) : ISuggestionImporter
    {
        public List<string> Calls { get; } = [];

        public Task<Guid?> ImportAsync(string scientificName, CancellationToken cancellationToken)
        {
            Calls.Add(scientificName);
            if (importable?.Contains(scientificName) != true) return Task.FromResult<Guid?>(null);

            var plant = new MatchedPlant(0, Guid.NewGuid(), $"Imported {scientificName}", null, null, scientificName, null);
            queries.Imported[plant.Id] = plant;
            return Task.FromResult<Guid?>(plant.Id);
        }
    }
}
