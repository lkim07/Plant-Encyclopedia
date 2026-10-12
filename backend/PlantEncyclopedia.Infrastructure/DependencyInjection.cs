using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.Categories;
using PlantEncyclopedia.Application.PlantGroups;
using PlantEncyclopedia.Application.Plants;
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

        return services;
    }
}
