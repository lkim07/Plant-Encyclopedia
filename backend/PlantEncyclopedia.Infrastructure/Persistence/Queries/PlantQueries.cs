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
                p.PlantGroup != null ? p.PlantGroup.Slug : null,
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
                    : null,
                p.Sources
                    .OrderBy(s => s.SourceType)
                    .ThenBy(s => s.Provider)
                    .Select(s => new PlantSourceInfo(s.Provider, s.SourceType))
                    .ToList(),
                p.Images
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => new PlantImageInfo(
                        i.Kind, i.Url, i.ThumbnailUrl, i.MediumUrl, i.LargeUrl, i.AltText, i.Width, i.Height,
                        i.Source, i.SourceUrl, i.License, i.Attribution))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PlantSummary?> GetPlantSummaryAsync(Guid plantId, CancellationToken cancellationToken) =>
        db.Plants
            .AsNoTracking()
            .Where(p => p.Id == plantId)
            .Select(p => new PlantSummary(
                p.Id,
                p.DisplayName,
                p.PlantGroup != null ? p.PlantGroup.Name : null,
                p.ScientificName,
                p.Status,
                p.Images
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => i.MediumUrl ?? i.Url)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
}
