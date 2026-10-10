using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Plants;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

internal sealed class PlantQueries(PlantEncyclopediaDbContext db) : IPlantQueries
{
    // A single read-only query that selects only the columns the Plant Detail page needs.
    public Task<PlantDetail?> GetPublishedPlantAsync(Guid plantId, CancellationToken cancellationToken) =>
        db.Plants
            .AsNoTracking()
            .Where(p => p.Id == plantId && p.Status == PlantStatus.Published)
            .Select(p => new PlantDetail(
                p.Id,
                p.DisplayName,
                p.PlantGroup != null ? p.PlantGroup.Name : null,
                p.ScientificName,
                p.Description,
                p.Care != null && p.Care.VerificationStatus == CareVerificationStatus.Verified
                    ? new PlantCareDetail(
                        p.Care.LightSummary,
                        p.Care.LightDetails,
                        p.Care.WaterSummary,
                        p.Care.WaterDetails,
                        p.Care.TemperatureMinC,
                        p.Care.TemperatureMaxC,
                        p.Care.TemperatureDetails,
                        p.Care.SoilSummary,
                        p.Care.SoilDetails)
                    : null))
            .SingleOrDefaultAsync(cancellationToken);
}
