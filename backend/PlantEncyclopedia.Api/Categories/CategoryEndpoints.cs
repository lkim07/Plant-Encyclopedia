using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Application.Categories;

namespace PlantEncyclopedia.Api.Categories;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", GetCategoriesAsync);
        app.MapGet("/api/categories/{categoryId}", GetCategoryAsync);
        return app;
    }

    // GET /api/categories (API_SPEC §17).
    private static async Task<IResult> GetCategoriesAsync(
        ICategoryQueries categories,
        CancellationToken cancellationToken)
    {
        var list = await categories.GetCategoriesAsync(cancellationToken);
        return Results.Ok(new { data = list.Select(CategoryResponse.From).ToList() });
    }

    // GET /api/categories/{categoryId} — categoryId is the category slug (API_SPEC §18).
    private static async Task<IResult> GetCategoryAsync(
        string categoryId,
        ICategoryQueries categories,
        CancellationToken cancellationToken)
    {
        var category = await categories.GetCategoryAsync(categoryId, cancellationToken);

        return category is null
            ? ApiErrors.NotFound("CATEGORY_NOT_FOUND", "The requested category could not be found.")
            : Results.Ok(new { data = CategoryDetailResponse.From(category) });
    }
}
