using PlantEncyclopedia.Domain.Retail;

namespace PlantEncyclopedia.Application.Retail;

/// <summary>
/// Which kinds of shop suit a plant, most suitable first, from its category (a rule, not stock information, D-033).
/// Florists are listed for flowers and succulents only (cut flowers, small potted plants); trees, fruits, vegetables
/// and herbs go to garden centres and nurseries. A plant without a category gets every kind.
/// </summary>
public static class RetailerRelevance
{
    private static readonly RetailerKind[] Flowers = [RetailerKind.FlowerShop, RetailerKind.GardenCentre, RetailerKind.Nursery];
    private static readonly RetailerKind[] Succulents = [RetailerKind.GardenCentre, RetailerKind.Nursery, RetailerKind.FlowerShop];
    private static readonly RetailerKind[] Trees = [RetailerKind.Nursery, RetailerKind.GardenCentre];
    private static readonly RetailerKind[] Edibles = [RetailerKind.GardenCentre, RetailerKind.Nursery];
    private static readonly RetailerKind[] Any = [RetailerKind.GardenCentre, RetailerKind.Nursery, RetailerKind.FlowerShop];

    public static IReadOnlyList<RetailerKind> KindsFor(string? categorySlug) => categorySlug switch
    {
        "flowers" => Flowers,
        "succulents" => Succulents,
        "trees" => Trees,
        "fruits" or "vegetables" or "herbs" => Edibles,
        _ => Any
    };
}
