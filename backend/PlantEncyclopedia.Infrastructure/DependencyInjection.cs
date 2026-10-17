using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlantEncyclopedia.Application.Categories;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Application.PlantGroups;
using PlantEncyclopedia.Application.Plants;
using PlantEncyclopedia.Application.Search;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using PlantEncyclopedia.Infrastructure.Persistence;
using PlantEncyclopedia.Infrastructure.Persistence.Queries;

namespace PlantEncyclopedia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PlantEncyclopediaDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IPlantQueries, PlantQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();
        services.AddScoped<IPlantGroupQueries, PlantGroupQueries>();
        services.AddScoped<ISearchQueries, SearchQueries>();

        return services;
    }

    /// <summary>
    /// External plant import (ARCHITECTURE §21–23). When GBIF is disabled, lookups return Unavailable
    /// and no HTTP client is registered. Invalid settings fail here, at startup.
    /// </summary>
    public static IServiceCollection AddExternalPlantData(
        this IServiceCollection services,
        GbifOptions gbif,
        GenusPlantGroupMap genusPlantGroups)
    {
        gbif.Validate();

        services.AddSingleton(genusPlantGroups);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPlantImportStore, PlantImportStore>();
        services.AddScoped<PlantIngestionService>();
        services.AddScoped<ExternalPlantLookupService>();

        if (!gbif.Enabled)
        {
            services.AddSingleton<IPlantDataProvider, UnavailablePlantDataProvider>();
            return services;
        }

        services.AddSingleton(gbif);
        services.AddHttpClient<GbifPlantDataProvider>(client =>
                GbifPlantDataProvider.ConfigureClient(client, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            // The default handlers log request URLs, which contain the searched name. Their query-string
            // redaction can be switched off by configuration, so they are removed rather than relied on.
            .RemoveAllLoggers();

        // Shared by every request: the cache and the app-wide budget must outlive a single lookup.
        services.AddSingleton<NegativeLookupCache>();
        services.AddSingleton<ExternalCallBudget>();
        services.AddTransient<IPlantDataProvider, GuardedPlantDataProvider>();

        return services;
    }
}
