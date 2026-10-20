using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.ExternalData.Images;

namespace PlantEncyclopedia.Api.Dev;

/// <summary>
/// Development tools with no authentication. Program.cs maps them only in the Development and Testing environments;
/// they must never be reachable elsewhere until an authorized review tool replaces them.
/// </summary>
public static class DevImageEndpoints
{
    public static IEndpointRouteBuilder MapDevImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/plants/{plantId:guid}/hero-image/reject", RejectHeroImageAsync);
        return app;
    }

    // Rejects the image the plant shows (e.g. a can instead of the plant) and queues the plant, so the next acceptable
    // candidate is fetched in the background. The rejected image is never chosen again for this plant.
    private static async Task<IResult> RejectHeroImageAsync(
        Guid plantId,
        IPlantImageStore images,
        IPlantImageQueue queue,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await images.RejectHeroImageAsync(plantId, timeProvider.GetUtcNow(), cancellationToken))
        {
            return ApiErrors.NotFound("PLANT_IMAGE_NOT_FOUND", "The plant has no image to reject.");
        }

        queue.Enqueue(plantId);
        return Results.Accepted(value: new { data = new { status = "rejected" } });
    }
}
