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
}
