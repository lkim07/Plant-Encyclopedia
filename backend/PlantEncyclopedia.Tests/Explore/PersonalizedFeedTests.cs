using PlantEncyclopedia.Application.Explore;

namespace PlantEncyclopedia.Tests.Explore;

/// <summary>
/// The rule-based personalized order (PRD §10–12, D-020–D-023), without a database: interest in plant groups,
/// the strong / adjacent / novel mix, turn-taking, and each plant once.
/// </summary>
public class PersonalizedFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    // Two categories with interest (Flowers: Roses, Tulips; Trees: Maples) and two without (Herbs, Ferns).
    private static readonly Guid Flowers = Guid.NewGuid(), Trees = Guid.NewGuid(), Herbs = Guid.NewGuid(), Ferns = Guid.NewGuid();
    private static readonly Guid Roses = Guid.NewGuid(), Tulips = Guid.NewGuid(), Maples = Guid.NewGuid(), Oaks = Guid.NewGuid(),
        Mints = Guid.NewGuid(), Basils = Guid.NewGuid(), FernGroup = Guid.NewGuid();

    private static readonly Dictionary<Guid, Guid> CategoryOf = new()
    {
        [Roses] = Flowers, [Tulips] = Flowers, [Maples] = Trees, [Oaks] = Trees,
        [Mints] = Herbs, [Basils] = Herbs, [FernGroup] = Ferns
    };

    private static List<FeedPlant> Catalogue(int perGroup = 6) =>
        CategoryOf.Keys
            .SelectMany(group => Enumerable.Range(1, perGroup)
                .Select(i => new FeedPlant(Guid.NewGuid(), group, CategoryOf[group], $"{Name(group)} {i}", HasImage: true, Seen: false)))
            .ToList();

    private static string Name(Guid group) =>
        group == Roses ? "Rose" : group == Tulips ? "Tulip" : group == Maples ? "Maple" : group == Oaks ? "Oak"
        : group == Mints ? "Mint" : group == Basils ? "Basil" : "Fern";

    private static FeedSignal View(Guid group, double daysAgo = 0) => new(group, PersonalizedFeed.ViewWeight, Now.AddDays(-daysAgo));

    private static List<Guid> GroupsInOrder(IReadOnlyList<FeedPlant> plants, IReadOnlyList<Guid> order)
    {
        var byId = plants.ToDictionary(p => p.Id);
        return order.Select(id => byId[id].GroupId).ToList();
    }

    // Protects against: personalizing on one stray click (PRD §10: "users with meaningful behavioral data").
    [Fact]
    public void TooLittleActivity_UsesTheDefaultFeed()
    {
        Assert.Null(PersonalizedFeed.Order(Catalogue(), [View(Roses), View(Roses)], Now));
        Assert.NotNull(PersonalizedFeed.Order(Catalogue(), [View(Roses), View(Roses), View(Maples)], Now));
    }

    // Protects against: a feed that only shows what the user already likes, or ignores it (D-020: 60/20/20).
    [Fact]
    public void EveryTenCards_AreSixStrongTwoAdjacentTwoNovel()
    {
        var plants = Catalogue();
        var order = PersonalizedFeed.Order(plants, [View(Roses), View(Roses), View(Maples)], Now)!;

        var groups = GroupsInOrder(plants, order);
        foreach (var ten in groups.Take(20).Chunk(10))
        {
            Assert.Equal(6, ten.Count(g => g == Roses || g == Maples));
            Assert.Equal(2, ten.Count(g => g == Tulips || g == Oaks));
            Assert.Equal(2, ten.Count(g => CategoryOf[g] == Herbs || CategoryOf[g] == Ferns));
        }

        Assert.Equal(Roses, groups[0]); // the strongest interest comes first
    }

    // Protects against: one favourite group filling the whole strong share (UX §14: "maintain diversity").
    [Fact]
    public void StrongGroups_TakeTurns()
    {
        var plants = Catalogue();
        var order = PersonalizedFeed.Order(plants, [View(Roses), View(Roses), View(Roses), View(Maples)], Now)!;

        var strong = GroupsInOrder(plants, order).Where(g => g == Roses || g == Maples).Take(4);
        Assert.Equal([Roses, Maples, Roses, Maples], strong);
    }

    // Protects against: the same plant twice (D-021) or plants dropped from the feed.
    [Fact]
    public void EveryPlantAppearsOnce()
    {
        var plants = Catalogue();
        var order = PersonalizedFeed.Order(plants, [View(Roses), View(Tulips), View(Maples), View(Mints)], Now)!;

        Assert.Equal(plants.Count, order.Count);
        Assert.Equal(plants.Select(p => p.Id).Order(), order.Order());
    }

    // Protects against: an empty bucket stopping the feed or leaving gaps; with every category touched there is
    // nothing novel, so adjacent groups take those turns.
    [Fact]
    public void AnEmptyBucket_GivesItsTurnsToTheNext()
    {
        var plants = Catalogue();
        var signals = new[] { Roses, Maples, Mints, FernGroup }.Select(g => View(g)).ToList();
        var order = PersonalizedFeed.Order(plants, signals, Now)!;

        var firstTen = GroupsInOrder(plants, order).Take(10).ToList();
        Assert.Equal(6, firstTen.Count(g => g == Roses || g == Maples || g == Mints || g == FernGroup));
        Assert.Equal(4, firstTen.Count(g => g == Tulips || g == Oaks || g == Basils));
        Assert.Equal(plants.Count, order.Count);
    }

    // Protects against: old interests outweighing recent ones (PRD §12: "recent interactions").
    [Fact]
    public void OlderActivity_CountsLess()
    {
        var plants = Catalogue();
        var signals = new[] { View(Roses, daysAgo: 60), View(Roses, daysAgo: 60), View(Maples) };

        var order = PersonalizedFeed.Order(plants, signals, Now)!;

        Assert.Equal(Maples, GroupsInOrder(plants, order)[0]);
    }

    // Protects against: the feed opening on plants the user has just looked at.
    [Fact]
    public void SeenPlants_ComeAfterUnseenOnesOfTheirGroup()
    {
        var plants = Catalogue(perGroup: 2);
        var seen = plants.First(p => p.GroupId == Roses && p.Name == "Rose 1");
        plants[plants.IndexOf(seen)] = seen with { Seen = true };

        var order = PersonalizedFeed.Order(plants, [View(Roses), View(Roses), View(Roses)], Now)!;

        var roses = order.Where(id => plants.Single(p => p.Id == id).GroupId == Roses).ToList();
        Assert.Equal(seen.Id, roses[^1]);
    }

    // Protects against: infinite scroll repeating or skipping plants (the same input must give the same order).
    [Fact]
    public void TheSameSignals_GiveTheSameOrder()
    {
        var plants = Catalogue();
        var signals = new[] { View(Roses), View(Maples, daysAgo: 3), View(Mints, daysAgo: 1) };

        Assert.Equal(PersonalizedFeed.Order(plants, signals, Now), PersonalizedFeed.Order(plants.AsEnumerable().Reverse().ToList(), signals, Now));
    }
}
