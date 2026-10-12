using PlantEncyclopedia.Api.Common;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.PlantGroups;

namespace PlantEncyclopedia.Api.PlantGroups;

public static class PlantGroupEndpoints
{
    public static IEndpointRouteBuilder MapPlantGroupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plant-groups/{plantGroupId}", GetPlantGroupAsync);
        return app;
    }

    // GET /api/plant-groups/{plantGroupId}?page=&pageSize= — plantGroupId is the group slug (API_SPEC §19).
    private static async Task<IResult> GetPlantGroupAsync(
        string plantGroupId,
        string? page,
        string? pageSize,
        IPlantGroupQueries plantGroups,
        CancellationToken cancellationToken)
    {
        var (request, error) = Pagination.Parse(page, pageSize);
        if (error is not null)
        {
            return error;
        }

        var group = await plantGroups.GetPlantGroupAsync(
            plantGroupId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return group is null
            ? ApiErrors.NotFound("PLANT_GROUP_NOT_FOUND", "The requested plant group could not be found.")
            : Results.Ok(new
            {
                data = PlantGroupDetailResponse.From(group),
                pagination = new PaginationResponse(request.Page, request.PageSize, group.HasNext)
            });
    }
}
