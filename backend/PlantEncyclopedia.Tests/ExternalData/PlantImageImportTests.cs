using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using PlantEncyclopedia.Infrastructure.Persistence;
using PlantEncyclopedia.Tests.Api;
using PlantEncyclopedia.Tests.Persistence;
using SkiaSharp;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>Download, checks and re-encoding of images. Offline: images are generated in memory.</summary>
public sealed class ImageFileStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("plant-images-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Protects against: storing full-size originals, or sizes that do not match what the API promises.
    [Fact]
    public void Process_MakesThreeWebpWidths()
    {
        var renditions = ImageFileStore.Process(Jpeg(2000, 1500))!;

        Assert.Equal([("thumb", 400, 300), ("medium", 800, 600), ("large", 1600, 1200)],
            renditions.Select(r => (r.Name, r.Width, r.Height)));
        Assert.All(renditions, r =>
        {
            using var codec = SKCodec.Create(SKData.CreateCopy(r.Webp));
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.Equal((r.Width, r.Height), (codec.Info.Width, codec.Info.Height));
        });
    }

    [Fact]
    public void Process_NeverEnlarges()
    {
        var renditions = ImageFileStore.Process(Png(500, 300))!;

        Assert.Equal([(400, 240), (500, 300), (500, 300)], renditions.Select(r => (r.Width, r.Height)));
    }

    // Protects against: storing files that are not images, other formats, or images large enough to exhaust
    // memory when decoded.
    [Fact]
    public void Process_RejectsUnusableFiles()
    {
        byte[] gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        Assert.Null(ImageFileStore.Process(Encoding.UTF8.GetBytes("<html>not an image</html>")));
        Assert.Null(ImageFileStore.Process(gif));
        Assert.Null(ImageFileStore.Process(Png(ImageFileStore.MaxSide + 1, 1)));
        Assert.Null(ImageFileStore.Process(Jpeg(2000, 1500)[..200])); // truncated
    }

    // Protects against: lost files, files outside the plant's folder, or URLs the API cannot serve.
    [Fact]
    public async Task Save_DownloadsAndStoresTheRenditions()
    {
        var handler = StubHttpMessageHandler.Returning(() => Image(Jpeg(2000, 1500), "image/jpeg"));
        var (plantId, imageId) = (Guid.NewGuid(), Guid.NewGuid());

        var stored = await Store(handler).SaveAsync(plantId, imageId, Accepted(), CancellationToken.None);

        var prefix = $"/media/plants/{plantId:N}/{imageId:N}";
        Assert.Equal(new StoredImageFiles($"{prefix}-thumb.webp", $"{prefix}-medium.webp", $"{prefix}-large.webp", 1600, 1200), stored);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(_root, "plants", plantId.ToString("N"))).Length);
        Assert.Equal("https://inaturalist-open-data.s3.amazonaws.com/photos/1/original.jpg", Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
    }

    // Protects against: fetching from hosts no source vouched for (a malicious redirect target, an internal address).
    [Theory]
    [InlineData("https://example.org/photo.jpg")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("http://upload.wikimedia.org/photo.jpg")]
    public async Task Save_RefusesOtherHosts(string url)
    {
        var handler = StubHttpMessageHandler.Returning(() => Image(Jpeg(2000, 1500), "image/jpeg"));

        Assert.Null(await Store(handler).SaveAsync(Guid.NewGuid(), Guid.NewGuid(), Accepted(url), CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    public static TheoryData<string, Func<HttpResponseMessage>> FailedResponses => new()
    {
        { "not found", () => new HttpResponseMessage(HttpStatusCode.NotFound) },
        { "redirect", () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.org/") } } },
        { "html", () => Image(Encoding.UTF8.GetBytes("<html></html>"), "text/html") },
        { "svg", () => Image(Encoding.UTF8.GetBytes("<svg/>"), "image/svg+xml") },
        { "lies about its type", () => Image(Encoding.UTF8.GetBytes("<html></html>"), "image/jpeg") },
        { "over 15 MB", () => Image(new byte[ImageFileStore.MaxDownloadBytes + 1], "image/jpeg") }
    };

    // Protects against: a bad download leaving files behind or failing the import.
    [Theory]
    [MemberData(nameof(FailedResponses))]
    public async Task Save_ReturnsNull_WhenTheDownloadIsUnusable(string scenario, Func<HttpResponseMessage> response)
    {
        var stored = await Store(StubHttpMessageHandler.Returning(response)).SaveAsync(Guid.NewGuid(), Guid.NewGuid(), Accepted(), CancellationToken.None);

        Assert.True(stored is null, scenario);
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    // Protects against: image import using more than its budget. The worker waits on this exception.
    [Fact]
    public async Task Save_Throws_WhenTheImageBudgetIsUsedUp()
    {
        var budget = new ImageCallBudget(new ManualTimeProvider());
        while (budget.TryAcquire())
        {
        }

        var handler = new ImageCallBudgetHandler(budget) { InnerHandler = StubHttpMessageHandler.Returning(() => Image(Jpeg(2000, 1500), "image/jpeg")) };

        await Assert.ThrowsAsync<ImageCallBudgetExhaustedException>(() =>
            Store(handler).SaveAsync(Guid.NewGuid(), Guid.NewGuid(), Accepted(), CancellationToken.None));
    }

    [Fact]
    public void ImageBudget_Allows40PerMinute()
    {
        var budget = new ImageCallBudget(new ManualTimeProvider());

        Assert.Equal(40, Enumerable.Range(0, 100).Count(_ => budget.TryAcquire()));
    }

    private ImageFileStore Store(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        ImageFileStore.ConfigureClient(client, GbifFixtures.Options());
        var storage = new LocalImageStorage(new PlantImageOptions { Enabled = true, StoragePath = _root });
        return new ImageFileStore(client, storage, NullLogger<ImageFileStore>.Instance);
    }

    internal static AcceptedImage Accepted(string url = "https://inaturalist-open-data.s3.amazonaws.com/photos/1/original.jpg") =>
        new(PlantImageSelectorTests.Candidate(url: url), "cc-by", "CC BY", null, "A. Photographer");

    private static HttpResponseMessage Image(byte[] bytes, string contentType) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes) { Headers = { ContentType = new(contentType) } }
    };

    internal static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    private static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}

/// <summary>
/// PlantImageImportService with the real EF image store on the disposable test database; sources and file
/// storage are fakes. Nothing goes over the network.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PlantImageImportServiceTests(PostgresDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    // Protects against: losing the licence, author or source of a stored image, or a misleading alt text.
    [Fact]
    public async Task Import_StoresAPhoto_WithProvenance_AndNoIllustration()
    {
        var plant = await InsertPlantAsync("Dog rose", "Rosa canina");
        var sources = new FakeSources();

        var result = await ImportAsync(plant.Id, sources);

        Assert.Equal(new PlantImageImportResult(PlantImageImportStatus.Done, 1), result);
        Assert.Equal([PlantImageKind.Photo], sources.Asked);
        var photo = Assert.Single(await LoadImagesAsync(plant.Id));
        Assert.Equal(
            (PlantImageKind.Photo, "photo-1", "test", "cc-by", "A. Photographer", $"Dog rose ({plant.ScientificName})", 1600, 1200, Now),
            (photo.Kind, photo.SourceId, photo.Source, photo.License, photo.Attribution, photo.AltText, photo.Width, photo.Height, photo.RetrievedAt));
        Assert.Equal(($"/media/{photo.Id:N}-large.webp", $"/media/{photo.Id:N}-medium.webp", $"/media/{photo.Id:N}-thumb.webp"),
            (photo.Url, photo.MediumUrl, photo.ThumbnailUrl));
    }

    // Protects against: a plant without any usable photo staying blank when a drawing exists.
    [Fact]
    public async Task Import_UsesAnIllustration_WhenNoPhotoIsFound()
    {
        var plant = await InsertPlantAsync("Dog rose", "Rosa canina");
        var sources = new FakeSources(photo: false);

        var result = await ImportAsync(plant.Id, sources);

        Assert.Equal(1, result.Added);
        Assert.Equal([PlantImageKind.Photo, PlantImageKind.Illustration], sources.Asked);
        var drawing = Assert.Single(await LoadImagesAsync(plant.Id));
        Assert.Equal((PlantImageKind.Illustration, $"Botanical illustration of Dog rose ({plant.ScientificName})"), (drawing.Kind, drawing.AltText));
    }

    // Protects against: replacing a plant's photo, or wasted requests for a plant that already has one. A plant
    // with only a drawing keeps looking for a photo, without fetching another drawing.
    [Fact]
    public async Task Import_LooksForAPhotoOnlyWhenThePlantHasNone()
    {
        var withPhoto = await InsertPlantAsync("Dog rose", "Rosa canina");
        await ImportAsync(withPhoto.Id, new FakeSources());
        var askedAgain = new FakeSources();
        Assert.Equal(0, (await ImportAsync(withPhoto.Id, askedAgain)).Added);
        Assert.Empty(askedAgain.Asked);

        var drawingOnly = await InsertPlantAsync("Dog rose", "Rosa canina");
        await ImportAsync(drawingOnly.Id, new FakeSources(photo: false));
        var stillNoPhoto = new FakeSources(photo: false);
        Assert.Equal(0, (await ImportAsync(drawingOnly.Id, stillNoPhoto)).Added);
        Assert.Equal([PlantImageKind.Photo], stillNoPhoto.Asked);

        Assert.Equal(1, (await ImportAsync(drawingOnly.Id, new FakeSources())).Added);
        Assert.Equal([PlantImageKind.Photo, PlantImageKind.Illustration], (await LoadImagesAsync(drawingOnly.Id)).Select(i => i.Kind).Order());
    }

    // Protects against: a species photo shown for a cultivar ("Rosa 'Peace'" is not every Rosa).
    [Fact]
    public async Task Import_SkipsCultivars()
    {
        var plant = await InsertPlantAsync("Peace", TestData.NewScientificName(), cultivar: "Peace");
        var sources = new FakeSources();

        Assert.Equal(PlantImageImportStatus.Skipped, (await ImportAsync(plant.Id, sources)).Status);
        Assert.Empty(sources.Asked);
    }

    [Fact]
    public async Task Import_UnknownPlant_IsNotFound()
    {
        Assert.Equal(PlantImageImportStatus.NotFound, (await ImportAsync(Guid.NewGuid(), new FakeSources())).Status);
    }

    // Protects against: an image row pointing at files that were never stored.
    [Fact]
    public async Task Import_SavesNothing_WhenFilesCannotBeStored()
    {
        var plant = await InsertPlantAsync("Dog rose", "Rosa canina");

        var result = await ImportAsync(plant.Id, new FakeSources(), files: new FakeFiles(fail: true));

        Assert.Equal(0, result.Added);
        Assert.Empty(await LoadImagesAsync(plant.Id));
    }

    // Protects against: the same external image stored twice for a plant (e.g. two imports at once).
    [Fact]
    public async Task Store_RefusesTheSameSourceImageTwice()
    {
        var plant = await InsertPlantAsync("Dog rose", "Rosa canina");
        await using var db = fixture.CreateDbContext();
        var store = new PlantImageStore(db);

        Assert.True(await store.AddAsync(Row(plant.Id), CancellationToken.None));
        Assert.False(await store.AddAsync(Row(plant.Id), CancellationToken.None));
        Assert.Single(await LoadImagesAsync(plant.Id));

        static PlantImage Row(Guid plantId) => new()
        {
            PlantId = plantId, Url = "/media/a.webp", Source = "inaturalist", SourceId = "1", RetrievedAt = DateTimeOffset.UtcNow
        };
    }

    // Protects against: the backfill fetching cultivars, Archived plants or plants that already have a photo,
    // or skipping a plant that only has a drawing.
    [Fact]
    public async Task Backfill_ListsOnlySpeciesWithoutAPhoto()
    {
        var wanted = await InsertPlantAsync("Wanted", TestData.NewScientificName());
        var drawingOnly = await InsertPlantAsync("Drawing only", TestData.NewScientificName());
        await ImportAsync(drawingOnly.Id, new FakeSources(photo: false));
        var cultivar = await InsertPlantAsync("Cultivar", TestData.NewScientificName(), cultivar: "Peace");
        var archived = await InsertPlantAsync("Archived", TestData.NewScientificName(), status: PlantStatus.Archived);
        var imaged = await InsertPlantAsync("Imaged", TestData.NewScientificName());
        await ImportAsync(imaged.Id, new FakeSources());

        await using var db = fixture.CreateDbContext();
        var listed = await new PlantImageStore(db).FindPlantsWithoutImagesAsync(int.MaxValue, CancellationToken.None);

        Assert.Contains(wanted.Id, listed);
        Assert.Contains(drawingOnly.Id, listed);
        Assert.DoesNotContain(cultivar.Id, listed);
        Assert.DoesNotContain(archived.Id, listed);
        Assert.DoesNotContain(imaged.Id, listed);
    }

    // Protects against: one failing plant stopping the worker, or the worker importing nothing.
    [Fact]
    public async Task Worker_ImportsQueuedPlants_AndSurvivesFailures()
    {
        var plant = await InsertPlantAsync("Dog rose", "Rosa canina");
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection()
            .AddLogging(b => b.AddProvider(logs))
            .AddScoped(_ => fixture.CreateDbContext())
            .AddScoped<IPlantImageStore, PlantImageStore>()
            .AddScoped(_ => new PlantImageImportService(
                new PlantImageSelector(new FakeSources().AsSources()), _.GetRequiredService<IPlantImageStore>(), new FakeFiles(), new FixedTimeProvider(Now)))
            .BuildServiceProvider();
        var worker = new PlantImageWorker(
            new PlantImageQueue(), services.GetRequiredService<IServiceScopeFactory>(), new PlantImageOptions(), TimeProvider.System,
            services.GetRequiredService<ILogger<PlantImageWorker>>());

        await worker.ProcessAsync(plant.Id, CancellationToken.None);
        await using (var broken = new ServiceCollection().AddLogging(b => b.AddProvider(logs)).BuildServiceProvider())
        {
            var failing = new PlantImageWorker(new PlantImageQueue(), broken.GetRequiredService<IServiceScopeFactory>(),
                new PlantImageOptions(), TimeProvider.System, broken.GetRequiredService<ILogger<PlantImageWorker>>());
            await failing.ProcessAsync(plant.Id, CancellationToken.None); // no import service registered: logged, not thrown
        }

        Assert.Single(await LoadImagesAsync(plant.Id));
        Assert.Contains(logs.Entries, e => e.Contains($"Image import for plant {plant.Id} failed"));
    }

    private async Task<PlantImageImportResult> ImportAsync(Guid plantId, FakeSources sources, FakeFiles? files = null)
    {
        await using var db = fixture.CreateDbContext();
        var service = new PlantImageImportService(
            new PlantImageSelector(sources.AsSources()), new PlantImageStore(db), files ?? new FakeFiles(), new FixedTimeProvider(Now));
        return await service.ImportAsync(plantId, CancellationToken.None);
    }

    private async Task<Plant> InsertPlantAsync(string name, string scientificName, string? cultivar = null, PlantStatus status = PlantStatus.Draft)
    {
        await using var db = fixture.CreateDbContext();
        // Each test needs its own plant, and the identity key allows one plant per name, so "Rosa canina" gets a suffix.
        if (scientificName == "Rosa canina") scientificName = $"Rosa canina {Guid.NewGuid():N}";
        var plant = new Plant { DisplayName = name, ScientificName = scientificName, CultivarName = cultivar, Status = status };

        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    private async Task<List<PlantImage>> LoadImagesAsync(Guid plantId)
    {
        await using var db = fixture.CreateDbContext();
        return db.PlantImages.Where(i => i.PlantId == plantId).ToList();
    }

    /// <summary>One acceptable photo and one acceptable illustration; records which kinds were asked for.</summary>
    private sealed class FakeSources(bool photo = true, bool illustration = true)
    {
        public List<PlantImageKind> Asked { get; } = [];

        public IEnumerable<IPlantImageSource> AsSources() =>
        [
            new Source(this, PlantImageKind.Photo, photo),
            new Source(this, PlantImageKind.Illustration, illustration)
        ];

        private sealed class Source(FakeSources owner, PlantImageKind kind, bool offers) : IPlantImageSource
        {
            public PlantImageKind Kind => kind;

            public Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken)
            {
                owner.Asked.Add(kind);
                IReadOnlyList<ExternalImageCandidate> candidates = offers
                    ? [PlantImageSelectorTests.Candidate(kind == PlantImageKind.Photo ? "photo-1" : "drawing-1", kind)]
                    : [];
                return Task.FromResult(candidates);
            }
        }
    }

    /// <summary>Pretends to store the image under /media/{imageId}-{size}.webp, or fails.</summary>
    private sealed class FakeFiles(bool fail = false) : IImageFileStore
    {
        public Task<StoredImageFiles?> SaveAsync(Guid plantId, Guid imageId, AcceptedImage image, CancellationToken cancellationToken) =>
            Task.FromResult(fail
                ? null
                : new StoredImageFiles($"/media/{imageId:N}-thumb.webp", $"/media/{imageId:N}-medium.webp", $"/media/{imageId:N}-large.webp", 1600, 1200));
    }
}
