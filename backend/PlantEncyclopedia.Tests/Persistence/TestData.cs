using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

internal static class TestData
{
    /// <summary>A minimal Draft plant with a unique name, so tests never share rows.</summary>
    public static Plant NewPlant() => new()
    {
        DisplayName = $"Test plant {Guid.NewGuid():N}",
        ScientificName = "Testus plantus"
    };

    /// <summary>A category with a unique name and slug; <paramref name="label"/> controls name ordering.</summary>
    public static Category NewCategory(string label = "")
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new Category { Name = $"Test Category {label} {suffix}", Slug = $"test-category-{suffix}" };
    }

    /// <summary>A plant group with a unique slug; <paramref name="name"/> controls name ordering.</summary>
    public static PlantGroup NewPlantGroup(Category category, string name = "Test Group") => new()
    {
        Name = name,
        Slug = $"test-group-{Guid.NewGuid():N}",
        Category = category
    };

    /// <summary>A plant in <paramref name="group"/> with the given name and status.</summary>
    public static Plant NewPlant(PlantGroup group, string name, PlantStatus status = PlantStatus.Published) => new()
    {
        DisplayName = name,
        ScientificName = "Testus plantus",
        PlantGroup = group,
        Status = status
    };
}
