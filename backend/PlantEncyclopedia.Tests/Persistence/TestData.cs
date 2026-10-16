using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

internal static class TestData
{
    /// <summary>
    /// A minimal Draft plant with unique names, so tests never share rows. The scientific name is unique too,
    /// because one plant per scientific name + cultivar is enforced by ux_plants_identity.
    /// </summary>
    public static Plant NewPlant() => new()
    {
        DisplayName = $"Test plant {Guid.NewGuid():N}",
        ScientificName = NewScientificName()
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
        ScientificName = NewScientificName(),
        PlantGroup = group,
        Status = status
    };

    /// <summary>A unique scientific name, so each test plant has its own identity (ux_plants_identity).</summary>
    public static string NewScientificName() => $"Testus plantus {Guid.NewGuid():N}";

    /// <summary>A unique, valid genus name (one capitalized word of letters), e.g. "Testgenusmqkt...".</summary>
    public static string NewGenus() =>
        "Testgenus" + string.Concat(Guid.NewGuid().ToString("N").Select(c => (char)('k' + (c % 10))));
}
