using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>How the API wires external plant import from configuration (appsettings.json).</summary>
[Collection(PostgresCollection.Name)]
public class ExternalDataStartupTests(PostgresDatabaseFixture fixture)
{
    // Protects against: tests (or a misconfigured test host) calling GBIF over the network.
    [Fact]
    public async Task Testing_NeverEnablesExternalProvider_EvenWhenConfigured()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        await using var configured = factory.WithWebHostBuilder(b => b.UseSetting("ExternalPlantData:Gbif:Enabled", "true"));

        using var scope = configured.Services.CreateScope();

        Assert.IsType<UnavailablePlantDataProvider>(scope.ServiceProvider.GetRequiredService<IPlantDataProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PlantIngestionService>());
    }

    // Protects against: the committed genus list drifting from the approved one (ambiguous Solanum left out).
    [Fact]
    public async Task GenusMap_IsLoadedFromAppSettings()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);

        var map = factory.Services.GetRequiredService<GenusPlantGroupMap>();

        Assert.Equal(11, map.Count);
        Assert.Equal("roses", map.FindSlug("Rosa"));
        Assert.Equal("mint", map.FindSlug("Mentha"));
        Assert.Null(map.FindSlug("Solanum"));
    }

    // Protects against: a typo in the genus list being ignored until an import publishes into the wrong place.
    [Fact]
    public async Task InvalidGenusMap_FailsStartup()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        await using var invalid = factory.WithWebHostBuilder(b => b.UseSetting("ExternalPlantData:GenusPlantGroups:solanum", "tomatoes"));

        var error = Record.Exception(() => invalid.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(ExceptionChain(error), e => e is ArgumentException && e.Message.Contains("'solanum'"));
    }

    private static IEnumerable<Exception> ExceptionChain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
