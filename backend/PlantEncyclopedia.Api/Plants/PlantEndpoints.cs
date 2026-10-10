using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Plants;

namespace PlantEncyclopedia.Api.Plants;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plants/{plantId}", GetPlantAsync);
        return app;
    }

    // GET /api/plants/{plantId} — anonymous Plant Detail (API_SPEC §14).
    private static async Task<IResult> GetPlantAsync(
        string plantId,
        IPlantQueries plants,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(plantId, out var id))
        {
            return ApiErrors.Validation("plantId", "Plant ID must be a valid UUID.");
        }

        var plant = await plants.GetPublishedPlantAsync(id, cancellationToken);

        // Missing and non-Published plants look the same, so drafts are never revealed.
        return plant is null
            ? ApiErrors.NotFound("PLANT_NOT_FOUND", "The requested plant could not be found.")
            : Results.Ok(new { data = PlantDetailResponse.From(plant) });
    }
}
