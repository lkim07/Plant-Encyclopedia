using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlantEncyclopedia.Application.Categories;
using PlantEncyclopedia.Application.Explore;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Application.PlantGroups;
using PlantEncyclopedia.Application.Plants;
using PlantEncyclopedia.Application.Search;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.ChecklistBank;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using PlantEncyclopedia.Infrastructure.ExternalData.INaturalist;
using PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;
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
        services.AddScoped<IExploreQueries, ExploreQueries>();

        return services;
    }

    /// <summary>
    /// External plant import (ARCHITECTURE §21–23). When GBIF is disabled, lookups return Unavailable
    /// and no HTTP client is registered. Image import needs GBIF enabled (it reuses its contact URL); when it
    /// is disabled, nothing is queued. Invalid settings fail here, at startup.
    /// </summary>
    public static IServiceCollection AddExternalPlantData(
        this IServiceCollection services,
        GbifOptions gbif,
        GenusPlantGroupMap genusPlantGroups,
        PlantImageOptions images)
    {
        gbif.Validate();
        images.Validate();
        if (images.Enabled && !gbif.Enabled)
        {
            throw new InvalidOperationException($"{PlantImageOptions.SectionName}:Enabled requires {GbifOptions.SectionName}:Enabled.");
        }

        services.AddSingleton(genusPlantGroups);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPlantImportStore, PlantImportStore>();
        services.AddScoped<PlantIngestionService>();
        services.AddScoped<ExternalPlantLookupService>();

        if (!gbif.Enabled)
        {
            services.AddSingleton<IPlantDataProvider, UnavailablePlantDataProvider>();
            services.AddSingleton<IPlantImageQueue, DisabledPlantImageQueue>();
            return services;
        }

        services.AddSingleton(gbif);
        services.AddHttpClient<GbifPlantDataProvider>(client =>
                GbifPlantDataProvider.ConfigureClient(client, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            // The default handlers log request URLs, which contain the searched name. Their query-string
            // redaction can be switched off by configuration, so they are removed rather than relied on.
            .RemoveAllLoggers();
        services.AddHttpClient<ChecklistBankCommonNameClient>(client =>
                ChecklistBankCommonNameClient.ConfigureClient(client, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();

        // Shared by every request: the cache and the app-wide budget must outlive a single lookup.
        services.AddSingleton<NegativeLookupCache>();
        services.AddSingleton(sp => new ExternalCallBudget(sp.GetRequiredService<TimeProvider>()));
        services.AddTransient<IPlantDataProvider, GuardedPlantDataProvider>();

        if (images.Enabled)
        {
            AddPlantImages(services, gbif, images);
        }
        else
        {
            services.AddSingleton<IPlantImageQueue, DisabledPlantImageQueue>();
        }

        return services;
    }

    // Image import (ARCHITECTURE §21–23): sources in the order they are asked, a separate app-wide budget taken
    // by every image request, local storage, and a background worker fed by the queue.
    private static void AddPlantImages(IServiceCollection services, GbifOptions gbif, PlantImageOptions images)
    {
        services.AddSingleton(images);
        services.AddSingleton<ImageCallBudget>();
        services.AddTransient<ImageCallBudgetHandler>();

        services.AddHttpClient<INaturalistPhotoSource>(client =>
                ExternalJsonClient.Configure(client, INaturalistPhotoSource.BaseAddress, INaturalistPhotoSource.Timeout, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<ImageCallBudgetHandler>()
            .RemoveAllLoggers();
        services.AddHttpClient<WikimediaClient>(client =>
                ExternalJsonClient.Configure(client, null, WikimediaClient.Timeout, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<ImageCallBudgetHandler>()
            .RemoveAllLoggers();
        services.AddHttpClient<IImageFileStore, ImageFileStore>(client => ImageFileStore.ConfigureClient(client, gbif))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<ImageCallBudgetHandler>()
            .RemoveAllLoggers();

        // Registration order is the order sources are asked: iNaturalist, Wikipedia, then the Commons species
        // category, for photos.
        services.AddTransient<IPlantImageSource>(sp => sp.GetRequiredService<INaturalistPhotoSource>());
        services.AddTransient<IPlantImageSource, WikipediaPhotoSource>();
        services.AddTransient<IPlantImageSource, CommonsCategoryPhotoSource>();
        services.AddTransient<IPlantImageSource, CommonsIllustrationSource>();
        services.AddTransient<PlantImageSelector>();

        services.AddSingleton<IImageStorage, LocalImageStorage>();
        services.AddScoped<IPlantImageStore, PlantImageStore>();
        services.AddScoped<PlantImageImportService>();

        services.AddSingleton<PlantImageQueue>();
        services.AddSingleton<IPlantImageQueue>(sp => sp.GetRequiredService<PlantImageQueue>());
        services.AddHostedService<PlantImageWorker>();
    }
}
