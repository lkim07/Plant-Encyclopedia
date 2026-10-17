using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.Persistence;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// PlantIngestionService with the real EF import store on the disposable test database.
/// Providers are fakes or the GBIF adapter on recorded responses; nothing goes over the network.
/// Every test uses its own genus, so tests never see each other's plants.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PlantIngestionTests(PostgresDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    // Protects against: an importable plant not being published in its mapped group, or losing provenance.
    [Fact]
    public async Task Import_PublishesInMappedGroup_WithTaxonomyAndSource()
    {
        var genus = TestData.NewGenus();
        var group = await NewGroupAsync();
        var taxon = ExternalTaxa.Species(genus);
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(taxon));

        var result = await ImportAsync(provider, $"{genus} vulgaris", Map(genus, group.Slug));

        Assert.Equal(PlantImportStatus.Created, result.Status);
        Assert.Equal(PlantStatus.Published, result.PlantStatus);

        var plant = await LoadAsync(result.PlantId!.Value);
        Assert.Equal($"{genus} vulgaris", plant.ScientificName);
        Assert.Equal(plant.ScientificName, plant.DisplayName);
        Assert.Null(plant.CultivarName);
        Assert.Null(plant.Description);
        Assert.Equal(group.Id, plant.PlantGroupId);
        Assert.Equal(PlantStatus.Published, plant.Status);
        Assert.Empty(plant.Names);
        Assert.Null(plant.Care);

        var taxonomy = plant.Taxonomy!;
        Assert.Equal(("Plantae", "Tracheophyta", "Magnoliopsida", "Rosales", "Rosaceae", genus, $"{genus} vulgaris"),
            (taxonomy.Kingdom, taxonomy.Phylum, taxonomy.Class, taxonomy.TaxonOrder, taxonomy.Family, taxonomy.Genus, taxonomy.Species));
        Assert.Null(taxonomy.Subspecies);
        Assert.Null(taxonomy.Variety);

        var source = Assert.Single(plant.Sources);
        Assert.Equal(("test-provider", taxon.ExternalId, PlantSourceType.Taxonomy, (string?)null, Now),
            (source.Provider, source.ExternalId, source.SourceType, source.SourceUrl, source.RetrievedAt));
    }

    // Protects against: losing the provider's common names, or showing the scientific name when an
    // English name was selected.
    [Fact]
    public async Task Import_StoresCommonNames_AndShowsTheEnglishName()
    {
        var genus = TestData.NewGenus();
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)))
        {
            CommonNames = [new("hedge plant", "eng", true), new("Heckenpflanze", "deu", false), new("haie", "spa", false)]
        };

        var result = await ImportAsync(provider, $"{genus} vulgaris");

        var plant = await LoadAsync(result.PlantId!.Value);
        Assert.Equal("Hedge plant", plant.DisplayName);
        Assert.Equal($"{genus} vulgaris", plant.ScientificName);
        Assert.Equal(
            [("Heckenpflanze", PlantNameType.Common, "de"), ("hedge plant", PlantNameType.Common, "en")],
            plant.Names.Select(n => (n.Name, n.NameType, n.LanguageCode)).OrderBy(n => n.LanguageCode));
    }

    // Protects against: a plant with no usable common names failing to import.
    [Fact]
    public async Task Import_UsesScientificName_WhenThereIsNoEnglishName()
    {
        var genus = TestData.NewGenus();
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)))
        {
            CommonNames = [new("Heckenpflanze", "deu", false)]
        };

        var plant = await LoadAsync((await ImportAsync(provider, $"{genus} vulgaris")).PlantId!.Value);

        Assert.Equal($"{genus} vulgaris", plant.DisplayName);
        Assert.Equal("Heckenpflanze", Assert.Single(plant.Names).Name);
    }

    // Protects against: auto-publishing without an explicit, existing group mapping (ARCHITECTURE §22).
    [Fact]
    public async Task Import_NeedsReview_WhenGenusIsNotMapped()
    {
        var genus = TestData.NewGenus();

        var result = await ImportAsync(new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus))), $"{genus} vulgaris");

        Assert.Equal((PlantImportStatus.Created, PlantStatus.NeedsReview), (result.Status, result.PlantStatus));
        Assert.Null((await LoadAsync(result.PlantId!.Value)).PlantGroupId);
    }

    [Fact]
    public async Task Import_NeedsReview_WhenMappedGroupDoesNotExist()
    {
        var genus = TestData.NewGenus();
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)));

        var result = await ImportAsync(provider, $"{genus} vulgaris", Map(genus, $"missing-{Guid.NewGuid():N}"));

        Assert.Equal((PlantImportStatus.Created, PlantStatus.NeedsReview), (result.Status, result.PlantStatus));
        Assert.Null((await LoadAsync(result.PlantId!.Value)).PlantGroupId);
    }

    // Protects against: publishing a probable duplicate whose name differs only by a hybrid sign,
    // punctuation or accents ("G vulgaris" next to "G × vulgaris"). It is saved for review instead.
    [Fact]
    public async Task Import_NeedsReview_WhenNearDuplicateExists()
    {
        var genus = TestData.NewGenus();
        var group = await NewGroupAsync();
        await InsertPlantAsync($"{genus} × vulgaris");
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)));

        var result = await ImportAsync(provider, $"{genus} vulgaris", Map(genus, group.Slug));

        Assert.Equal((PlantImportStatus.Created, PlantStatus.NeedsReview), (result.Status, result.PlantStatus));
        Assert.Equal(group.Id, (await LoadAsync(result.PlantId!.Value)).PlantGroupId);
    }

    // Protects against: an external request (and a duplicate) for a plant that is already stored,
    // whatever its spelling or status.
    [Fact]
    public async Task Import_ReturnsExistingPlant_WithoutCallingProvider()
    {
        var genus = TestData.NewGenus();
        var existing = await InsertPlantAsync($"{genus} Vulgaris", PlantStatus.Archived);
        var provider = new FakePlantDataProvider(TaxonLookupResult.Found(ExternalTaxa.Species(genus)));

        var result = await ImportAsync(provider, $"  {genus.ToLowerInvariant()}   vulgaris ");

        Assert.Equal(new PlantImportResult(PlantImportStatus.AlreadyExists, existing.Id), result);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, provider.CommonNameCalls);
        await AssertUnchangedAsync(existing);
    }

    // Protects against: a duplicate when the provider's accepted spelling differs from the request
    // ("G vulgaris" requested, "G × vulgaris" returned and already stored).
    [Fact]
    public async Task Import_ReturnsExistingPlant_WhenProviderNameIsStored()
    {
        var genus = TestData.NewGenus();
        var existing = await InsertPlantAsync($"{genus} × vulgaris");
        var taxon = ExternalTaxa.Species(genus) with { ScientificName = $"{genus} × vulgaris" };

        var result = await ImportAsync(new FakePlantDataProvider(TaxonLookupResult.Found(taxon)), $"{genus} vulgaris");

        Assert.Equal(new PlantImportResult(PlantImportStatus.AlreadyExists, existing.Id), result);
        await AssertUnchangedAsync(existing);
    }

    // Protects against: importing a taxon again after its stored name was edited.
    [Fact]
    public async Task Import_ReturnsExistingPlant_WhenTaxonIsAlreadyLinked()
    {
        var genus = TestData.NewGenus();
        var taxon = ExternalTaxa.Species(genus);
        var existing = await InsertPlantAsync($"{genus} renamed", source: new PlantSource
        {
            Provider = taxon.Provider,
            ExternalId = taxon.ExternalId,
            SourceType = PlantSourceType.Taxonomy,
            RetrievedAt = Now
        });

        var result = await ImportAsync(new FakePlantDataProvider(TaxonLookupResult.Found(taxon)), $"{genus} vulgaris");

        Assert.Equal(new PlantImportResult(PlantImportStatus.AlreadyExists, existing.Id), result);
        await AssertUnchangedAsync(existing);
    }

    // Protects against: saving anything when the provider has no usable answer.
    [Theory]
    [InlineData(TaxonLookupStatus.NotFound, PlantImportStatus.NotFound)]
    [InlineData(TaxonLookupStatus.Unavailable, PlantImportStatus.Unavailable)]
    public async Task Import_SavesNothing_WhenProviderHasNoMatch(TaxonLookupStatus lookup, PlantImportStatus expected)
    {
        var genus = TestData.NewGenus();

        var result = await ImportAsync(new FakePlantDataProvider(new TaxonLookupResult(lookup, null)), $"{genus} vulgaris");

        Assert.Equal(new PlantImportResult(expected), result);
        Assert.Equal(0, await CountGenusAsync(genus));
    }

    [Fact]
    public async Task Import_SavesNothing_WhenMatchIsRejected()
    {
        var genus = TestData.NewGenus();
        var fuzzy = ExternalTaxa.Species(genus) with { IsExactMatch = false };

        var result = await ImportAsync(new FakePlantDataProvider(TaxonLookupResult.Found(fuzzy)), $"{genus} vulgaris");

        Assert.Equal(new PlantImportResult(PlantImportStatus.Rejected, Rejection: TaxonRejection.NotExactMatch), result);
        Assert.Equal(0, await CountGenusAsync(genus));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Rosa\u0000canina")]
    public async Task Import_RejectsInvalidName_WithoutCallingProvider(string name)
    {
        var provider = new FakePlantDataProvider(TaxonLookupResult.Unavailable);

        var result = await ImportAsync(provider, name);

        Assert.Equal(new PlantImportResult(PlantImportStatus.InvalidName), result);
        Assert.Equal(0, provider.Calls);
    }

    // Protects against: two simultaneous imports of the same plant creating two rows. Both requests pass
    // the existence checks before either inserts; the database index decides, and the loser returns the
    // winner's plant. The loser's taxonomy row is rolled back with it.
    [Fact]
    public async Task ConcurrentImports_CreateOnePlant()
    {
        var genus = TestData.NewGenus();
        var taxon = ExternalTaxa.Species(genus);
        var bothReadyToInsert = new Rendezvous(2);

        async Task<PlantImportResult> RunAsync()
        {
            await using var db = fixture.CreateDbContext();
            var service = new PlantIngestionService(
                new FakePlantDataProvider(TaxonLookupResult.Found(taxon)),
                new GatedImportStore(new PlantImportStore(db), bothReadyToInsert),
                new GenusPlantGroupMap(null),
                new FixedTimeProvider(Now));
            return await service.ImportAsync($"{genus} vulgaris", CancellationToken.None);
        }

        var results = await Task.WhenAll(Task.Run(RunAsync), Task.Run(RunAsync));

        Assert.Equal(
            [PlantImportStatus.Created, PlantImportStatus.AlreadyExists],
            results.Select(r => r.Status).Order());
        Assert.Equal(results[0].PlantId, results[1].PlantId);
        Assert.Equal(1, await CountGenusAsync(genus));

        await using var check = fixture.CreateDbContext();
        Assert.Equal(1, await check.Taxonomies.CountAsync(t => t.Genus == genus));
        Assert.Equal(1, await check.PlantSources.CountAsync(s => s.ExternalId == taxon.ExternalId));
    }

    // Protects against: the store's conflict handling depending on timing; a plain duplicate insert
    // must return the existing plant and save nothing.
    [Fact]
    public async Task Store_InsertDuplicate_ReturnsExistingPlant()
    {
        var genus = TestData.NewGenus();
        var existing = await InsertPlantAsync($"{genus} vulgaris");

        await using var db = fixture.CreateDbContext();
        var result = await new PlantImportStore(db).InsertAsync(
            new Plant { DisplayName = "Duplicate", ScientificName = $"{genus.ToUpperInvariant()} VULGARIS", Taxonomy = new Taxonomy { Genus = genus } },
            CancellationToken.None);

        Assert.Equal(new PlantInsertResult(existing.Id, Created: false), result);
        Assert.Equal(1, await CountGenusAsync(genus));
        await using var check = fixture.CreateDbContext();
        Assert.Equal(0, await check.Taxonomies.CountAsync(t => t.Genus == genus));
    }

    // End to end on recorded GBIF responses: the adapter, policy, ingestion and store together.
    // Protects against: the provider spelling ("Mentha × piperita") not being the one stored, or a
    // second import of the same plant calling GBIF again.
    [Fact]
    public async Task RecordedGbifMatch_IsImportedOnce()
    {
        var group = await NewGroupAsync();
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Response(GbifFixtures.MenthaHybrid));
        var gbif = GbifFixtures.Provider(handler);
        var map = Map("Mentha", group.Slug);

        var first = await ImportAsync(gbif, "Mentha piperita", map);
        var second = await ImportAsync(gbif, "Mentha × piperita", map);

        Assert.Equal((PlantImportStatus.Created, PlantStatus.Published), (first.Status, first.PlantStatus));
        Assert.Equal(new PlantImportResult(PlantImportStatus.AlreadyExists, first.PlantId), second);
        Assert.Single(handler.Requests);

        var plant = await LoadAsync(first.PlantId!.Value);
        Assert.Equal("Mentha × piperita", plant.ScientificName);
        Assert.Equal(("Lamiaceae", "Mentha", "Mentha × piperita"), (plant.Taxonomy!.Family, plant.Taxonomy.Genus, plant.Taxonomy.Species));
        var source = Assert.Single(plant.Sources);
        Assert.Equal(("gbif-col-xr", "72ZGR"), (source.Provider, source.ExternalId));
    }

    // End to end on recorded GBIF and ChecklistBank responses, through the guarded provider used in production.
    // Protects against: the COL XR taxon ID not being the one looked up for names, or the names not
    // reaching the database.
    [Fact]
    public async Task RecordedGbifMatchAndNames_ImportDogRose()
    {
        var group = await NewGroupAsync();
        var handler = new StubHttpMessageHandler((request, _) => Task.FromResult(request.RequestUri!.Host switch
        {
            "api.gbif.org" => GbifFixtures.Response(GbifFixtures.RosaCanina),
            "api.checklistbank.org" => ChecklistBankFixtures.Response(ChecklistBankFixtures.RosaCanina),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
        }));
        var time = new ManualTimeProvider();
        var provider = new GuardedPlantDataProvider(
            GbifFixtures.Provider(handler),
            ChecklistBankFixtures.Client(handler),
            new NegativeLookupCache(time),
            new ExternalCallBudget(time),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GuardedPlantDataProvider>.Instance);

        var result = await ImportAsync(provider, "Rosa canina", Map("Rosa", group.Slug));

        Assert.Equal((PlantImportStatus.Created, PlantStatus.Published), (result.Status, result.PlantStatus));
        Assert.Equal(
            ["https://api.gbif.org/", "https://api.checklistbank.org/dataset/3LXR/taxon/4TCZM/vernacular"],
            handler.Requests.Select(r => r.RequestUri!.Host == "api.gbif.org" ? "https://api.gbif.org/" : r.RequestUri.AbsoluteUri));

        var plant = await LoadAsync(result.PlantId!.Value);
        Assert.Equal(("Dog rose", "Rosa canina"), (plant.DisplayName, plant.ScientificName));
        Assert.Equal(17, plant.Names.Count);
        Assert.Contains(plant.Names, n => (n.Name, n.LanguageCode) == ("Hundsrose", "de"));
    }

    [Fact]
    public async Task RecordedGbifSynonym_IsRejected()
    {
        var gbif = GbifFixtures.Provider(StubHttpMessageHandler.Returning(() => GbifFixtures.Response(GbifFixtures.LycopersiconSynonym)));

        var result = await ImportAsync(gbif, "Lycopersicon esculentum");

        Assert.Equal(new PlantImportResult(PlantImportStatus.Rejected, Rejection: TaxonRejection.NotAccepted), result);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Plants.AnyAsync(p => p.ScientificName == "Lycopersicon esculentum"));
    }

    private async Task<PlantImportResult> ImportAsync(IPlantDataProvider provider, string name, GenusPlantGroupMap? map = null)
    {
        await using var db = fixture.CreateDbContext();
        var service = new PlantIngestionService(provider, new PlantImportStore(db), map ?? new GenusPlantGroupMap(null), new FixedTimeProvider(Now));
        return await service.ImportAsync(name, CancellationToken.None);
    }

    private static GenusPlantGroupMap Map(string genus, string slug) => new(new Dictionary<string, string> { [genus] = slug });

    private async Task<PlantGroup> NewGroupAsync()
    {
        await using var db = fixture.CreateDbContext();
        var group = TestData.NewPlantGroup(TestData.NewCategory());
        db.PlantGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    private async Task<Plant> InsertPlantAsync(string scientificName, PlantStatus status = PlantStatus.Draft, PlantSource? source = null)
    {
        await using var db = fixture.CreateDbContext();
        var plant = new Plant { DisplayName = "Existing plant", ScientificName = scientificName, Status = status };
        if (source is not null) plant.Sources.Add(source);
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return await LoadAsync(plant.Id); // as stored: PostgreSQL keeps microseconds, not 100 ns ticks
    }

    private async Task<Plant> LoadAsync(Guid id)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Plants
            .Include(p => p.Taxonomy)
            .Include(p => p.Sources)
            .Include(p => p.Names)
            .Include(p => p.Care)
            .AsSplitQuery()
            .SingleAsync(p => p.Id == id);
    }

    private async Task AssertUnchangedAsync(Plant expected)
    {
        var actual = await LoadAsync(expected.Id);
        Assert.Equal(
            (expected.DisplayName, expected.ScientificName, expected.Status, expected.UpdatedAt, expected.TaxonomyId, expected.Sources.Count),
            (actual.DisplayName, actual.ScientificName, actual.Status, actual.UpdatedAt, actual.TaxonomyId, actual.Sources.Count));
    }

    private async Task<int> CountGenusAsync(string genus)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Plants.CountAsync(p => p.ScientificName.StartsWith(genus));
    }

    /// <summary>Lets <paramref name="parties"/> callers continue only once all of them have arrived.</summary>
    private sealed class Rendezvous(int parties)
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public Task ArriveAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrived) == parties) _allArrived.SetResult();
            return _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Holds each insert until every concurrent import has passed its existence checks.</summary>
    private sealed class GatedImportStore(IPlantImportStore inner, Rendezvous gate) : IPlantImportStore
    {
        public Task<Guid?> FindByIdentityAsync(string scientificName, CancellationToken ct) => inner.FindByIdentityAsync(scientificName, ct);
        public Task<Guid?> FindByTaxonSourceAsync(string provider, string externalId, CancellationToken ct) => inner.FindByTaxonSourceAsync(provider, externalId, ct);
        public Task<Guid?> FindPlantGroupIdAsync(string slug, CancellationToken ct) => inner.FindPlantGroupIdAsync(slug, ct);
        public Task<bool> HasNearDuplicateAsync(string scientificName, CancellationToken ct) => inner.HasNearDuplicateAsync(scientificName, ct);

        public async Task<PlantInsertResult> InsertAsync(Plant plant, CancellationToken ct)
        {
            await gate.ArriveAndWaitAsync();
            return await inner.InsertAsync(plant, ct);
        }
    }
}
